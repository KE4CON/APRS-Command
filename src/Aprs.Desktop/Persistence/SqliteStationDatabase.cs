using System.Text.Json;
using Aprs.Core;
using Aprs.Services;
using Microsoft.Data.Sqlite;

namespace Aprs.Desktop.Persistence;

/// <summary>
/// Wraps <see cref="StationDatabase"/> with SQLite persistence. Station snapshots and tactical
/// labels survive restarts. Trail points are not persisted — they rebuild from new packets.
///
/// <para>The database file lives in the same folder as the settings JSON, managed by
/// <see cref="DatabasePath"/>. Schema is created automatically on first run.</para>
/// </summary>
public sealed class SqliteStationDatabase : IStationDatabase, IDisposable
{
    private readonly StationDatabase inner;
    private readonly SqliteConnection connection;

    public static string DatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "APRSCommand",
            "stations.db");

    public SqliteStationDatabase() : this(new StationDatabase()) { }

    public SqliteStationDatabase(StationDatabase inner)
    {
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        connection = new SqliteConnection($"Data Source={DatabasePath}");
        connection.Open();

        EnsureSchema();
        LoadSnapshots();
    }

    // ── IStationDatabase pass-throughs ────────────────────────────────────

    public void ProcessPacket(AprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown)
    {
        inner.ProcessPacket(packet, packetSource);
        // Persist the updated snapshot asynchronously so the UI thread isn't blocked.
        var callsign = packet.SourceCallsign;
        if (!string.IsNullOrWhiteSpace(callsign))
        {
            var snapshot = inner.GetStation(callsign);
            if (snapshot is not null)
            {
                Task.Run(() => PersistSnapshot(snapshot));
            }
        }
    }

    public IReadOnlyCollection<StationSnapshot> GetAllStations()      => inner.GetAllStations();
    public IReadOnlyCollection<StationSnapshot> GetVisibleStations()  => inner.GetVisibleStations();
    public IReadOnlyCollection<StationSnapshot> GetActiveStations()   => inner.GetActiveStations();
    public IReadOnlyList<StationTrailPoint>      GetTrail(string c)   => inner.GetTrail(c);
    public StationSnapshot?                      GetStation(string c) => inner.GetStation(c);
    public void UpdateAgeStates(DateTimeOffset now)                    => inner.UpdateAgeStates(now);
    public bool HideStation(string callsign)                           => inner.HideStation(callsign);
    public bool UnhideStation(string callsign, DateTimeOffset now)     => inner.UnhideStation(callsign, now);
    public void ClearHiddenState(DateTimeOffset now)                   => inner.ClearHiddenState(now);
    public bool ClearTrail(string callsign)                            => inner.ClearTrail(callsign);
    public void ClearAllTrails()                                       => inner.ClearAllTrails();
    public void Clear()                                                => inner.Clear();

    public TacticalLabel SetTacticalLabel(string callsign, string label, string? notes, DateTimeOffset now)
    {
        var result = inner.SetTacticalLabel(callsign, label, notes, now);
        Task.Run(() => PersistTacticalLabel(result));
        return result;
    }

    public bool RemoveTacticalLabel(string callsign)
    {
        var removed = inner.RemoveTacticalLabel(callsign);
        if (removed)
        {
            Task.Run(() => DeleteTacticalLabel(callsign));
        }

        return removed;
    }

    public TacticalLabel?                        GetTacticalLabel(string c)    => inner.GetTacticalLabel(c);
    public IReadOnlyCollection<TacticalLabel>    GetAllTacticalLabels()         => inner.GetAllTacticalLabels();
    public void ClearTacticalLabels()
    {
        inner.ClearTacticalLabels();
        Task.Run(DeleteAllTacticalLabels);
    }

    public void Dispose()
    {
        connection.Close();
        connection.Dispose();
    }

    // ── Schema ────────────────────────────────────────────────────────────

    private void EnsureSchema()
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Stations (
                Callsign        TEXT    NOT NULL PRIMARY KEY,
                SnapshotJson    TEXT    NOT NULL,
                LastHeardUtc    TEXT    NOT NULL
            );

            CREATE TABLE IF NOT EXISTS TacticalLabels (
                RealCallsign    TEXT    NOT NULL PRIMARY KEY,
                Label           TEXT    NOT NULL,
                Notes           TEXT,
                CreatedAtUtc    TEXT    NOT NULL,
                UpdatedAtUtc    TEXT    NOT NULL
            );

            -- Keep only the 2000 most recently heard stations to cap database size.
            CREATE TRIGGER IF NOT EXISTS PruneOldStations
            AFTER INSERT ON Stations
            BEGIN
                DELETE FROM Stations
                WHERE Callsign NOT IN (
                    SELECT Callsign FROM Stations
                    ORDER BY LastHeardUtc DESC
                    LIMIT 2000
                );
            END;
            """;
        cmd.ExecuteNonQuery();
    }

    // ── Load on startup ───────────────────────────────────────────────────

    private void LoadSnapshots()
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT SnapshotJson FROM Stations ORDER BY LastHeardUtc DESC LIMIT 2000";

        int loaded = 0;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            try
            {
                var json = reader.GetString(0);
                var snapshot = JsonSerializer.Deserialize<StationSnapshot>(json, JsonOptions);
                if (snapshot is not null)
                {
                    inner.RestoreSnapshot(snapshot);
                    loaded++;
                }
            }
            catch
            {
                // Skip malformed rows — don't crash on corrupt data.
            }
        }

        // Load tactical labels
        using var labelCmd = connection.CreateCommand();
        labelCmd.CommandText = "SELECT RealCallsign, Label, Notes, CreatedAtUtc, UpdatedAtUtc FROM TacticalLabels";
        using var labelReader = labelCmd.ExecuteReader();
        while (labelReader.Read())
        {
            try
            {
                var label = new TacticalLabel(
                    RealCallsign: labelReader.GetString(0),
                    Label:        labelReader.GetString(1),
                    Notes:        labelReader.IsDBNull(2) ? null : labelReader.GetString(2),
                    CreatedAtUtc: DateTimeOffset.Parse(labelReader.GetString(3)),
                    UpdatedAtUtc: DateTimeOffset.Parse(labelReader.GetString(4)));
                inner.RestoreTacticalLabel(label);
            }
            catch { /* skip */ }
        }
    }

    // ── Persist helpers (called from background threads) ─────────────────

    private void PersistSnapshot(StationSnapshot snapshot)
    {
        try
        {
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Stations (Callsign, SnapshotJson, LastHeardUtc)
                VALUES ($c, $j, $t)
                ON CONFLICT(Callsign) DO UPDATE SET
                    SnapshotJson = excluded.SnapshotJson,
                    LastHeardUtc = excluded.LastHeardUtc;
                """;
            cmd.Parameters.AddWithValue("$c", snapshot.Callsign);
            cmd.Parameters.AddWithValue("$j", json);
            cmd.Parameters.AddWithValue("$t", snapshot.LastHeardUtc.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        catch { /* best-effort — never crash on persistence failure */ }
    }

    private void PersistTacticalLabel(TacticalLabel label)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO TacticalLabels (RealCallsign, Label, Notes, CreatedAtUtc, UpdatedAtUtc)
                VALUES ($c, $l, $n, $ca, $ua)
                ON CONFLICT(RealCallsign) DO UPDATE SET
                    Label = excluded.Label,
                    Notes = excluded.Notes,
                    UpdatedAtUtc = excluded.UpdatedAtUtc;
                """;
            cmd.Parameters.AddWithValue("$c",  label.RealCallsign);
            cmd.Parameters.AddWithValue("$l",  label.Label);
            cmd.Parameters.AddWithValue("$n",  (object?)label.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ca", label.CreatedAtUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$ua", label.UpdatedAtUtc.ToString("O"));
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private void DeleteTacticalLabel(string callsign)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM TacticalLabels WHERE RealCallsign = $c";
            cmd.Parameters.AddWithValue("$c", callsign);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private void DeleteAllTacticalLabels()
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM TacticalLabels";
            cmd.ExecuteNonQuery();
        }
        catch { }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}
