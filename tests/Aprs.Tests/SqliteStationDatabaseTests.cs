using Aprs.Core;
using Aprs.Desktop.Persistence;
using Aprs.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Deep-audit closed the gap that the persistence layer (which took the C4 concurrency fix) had zero tests.
/// Covers snapshot round-trip, corrupt-row tolerance, and concurrent write + dispose (no crash).
/// </summary>
public sealed class SqliteStationDatabaseTests : IDisposable
{
    private readonly string dbPath = Path.Combine(Path.GetTempPath(), $"aprs-sqlite-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        try { if (File.Exists(dbPath)) File.Delete(dbPath); } catch { /* best effort */ }
    }

    private static AprsPacket Position(string callsign) =>
        new AprsParser().Parse($"{callsign}>APRS,TCPIP*:=3903.50N/07201.75W-Test", DateTimeOffset.UtcNow);

    // Writes the packet, lets the single async persist settle, then disposes the writer and returns a fresh
    // reader instance. No concurrent connections — RunWrite swallows lock-contention errors, so competing
    // readers during the pending write would non-deterministically drop it.
    private void PersistThenClose(string callsign)
    {
        var db = new SqliteStationDatabase(new StationDatabase(), dbPath);
        db.ProcessPacket(Position(callsign));
        Thread.Sleep(400); // the lone write, uncontended, completes in a few ms
        db.Dispose();
    }

    [Fact]
    public void Snapshot_PersistsAndReloadsAcrossInstances()
    {
        PersistThenClose("KE4CON");

        using var reopened = new SqliteStationDatabase(new StationDatabase(), dbPath);
        var station = reopened.GetStation("KE4CON");
        Assert.NotNull(station);
        Assert.Equal(39.0583, station!.Latitude!.Value, 3);
        Assert.Equal(-72.0292, station.Longitude!.Value, 3);
    }

    [Fact]
    public void Load_WithACorruptSnapshotRow_SkipsItAndLoadsTheGoodOnes()
    {
        PersistThenClose("GOODCALL");

        using (var raw = new SqliteConnection($"Data Source={dbPath}"))
        {
            raw.Open();
            using var cmd = raw.CreateCommand();
            cmd.CommandText = "INSERT INTO Stations (Callsign, SnapshotJson, LastHeardUtc) VALUES ('BADCALL', '{ this is not valid json ', '2026-08-10T00:00:00.0000000+00:00');";
            cmd.ExecuteNonQuery();
        }

        using var reopened = new SqliteStationDatabase(new StationDatabase(), dbPath);
        Assert.NotNull(reopened.GetStation("GOODCALL")); // good row survived
        Assert.Null(reopened.GetStation("BADCALL"));      // corrupt row skipped, no crash
    }

    [Fact]
    public async Task ConcurrentProcessPacketAndDispose_DoesNotThrow()
    {
        var db = new SqliteStationDatabase(new StationDatabase(), dbPath);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var exception = await Record.ExceptionAsync(async () =>
        {
            var writer = Task.Run(() =>
            {
                var n = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    try { db.ProcessPacket(Position($"N{n % 40}CALL")); } catch (ObjectDisposedException) { break; }
                    n++;
                }
            });

            await Task.Delay(300);
            db.Dispose(); // dispose while writes are in flight — RunWrite must guard use-after-dispose
            await writer;
        });

        Assert.Null(exception);
    }
}
