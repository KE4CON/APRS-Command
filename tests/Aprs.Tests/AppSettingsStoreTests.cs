using System.Text.Json;
using System.Text.Json.Nodes;
using Aprs.Desktop.Configuration;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Tests for the unified settings-persistence store: round-trips, default fallbacks, atomic and
/// corruption-resilient loading, per-section salvage, schema stamping, and legacy import.
/// Each test uses a throwaway temp directory so nothing touches the real user profile.
/// </summary>
public sealed class AppSettingsStoreTests : IDisposable
{
    private readonly string tempDir;
    private readonly string settingsPath;

    public AppSettingsStoreTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "aprs-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        settingsPath = Path.Combine(tempDir, "config", "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { /* best effort */ }
    }

    private JsonAppSettingsStore NewStore(string? legacyPath = null) => new(settingsPath, legacyPath);

    [Fact]
    public void Load_WithNoFile_ReturnsDefaults()
    {
        var store = NewStore();

        var settings = store.Load();

        Assert.Equal(AppSettings.Default.Station.Callsign, settings.Station.Callsign);
        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.False(settings.Station.IsConfigured);
        Assert.False(File.Exists(settingsPath)); // defaults are not written until something is saved
    }

    [Fact]
    public void SaveThenLoad_RoundTripsStationAndConnections()
    {
        var store = NewStore();
        var saved = AppSettings.Default with
        {
            Station = new StationProfile("KE4CON", 40.0, -83.0, 100),
            Connections = new ConnectionSettings(
            [
                new ConnectionPort(
                    "radio-1", "Radio 1", ConnectionPortType.NetworkTncKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: true,
                    PortConfiguration.ForNetworkTncKiss(
                        TcpKissConfiguration.Default with { Host = "10.0.0.5", Port = 8002, Enabled = true }))
            ])
        };

        store.Save(saved);
        var loaded = NewStore().Load(); // fresh store, same path

        Assert.Equal("KE4CON", loaded.Station.Callsign);
        Assert.Equal(40.0, loaded.Station.Latitude);
        Assert.Equal(100, loaded.Station.FilterRadiusKm);

        var port = Assert.Single(loaded.Connections.Ports);
        Assert.Equal("radio-1", port.Id);
        Assert.Equal(ConnectionPortType.NetworkTncKiss, port.Type);
        Assert.True(port.Enabled);
        Assert.True(port.TransmitEnabled);
        Assert.Equal("10.0.0.5", port.Configuration.NetworkTncKiss!.Host);
        Assert.Equal(8002, port.Configuration.NetworkTncKiss!.Port);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEnumsAndTimeSpans()
    {
        var store = NewStore();
        var saved = AppSettings.Default with
        {
            Connections = new ConnectionSettings(
            [
                new ConnectionPort(
                    "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with
                    {
                        PortName = "/dev/tty.usbserial",
                        BaudRate = 1200,
                        Parity = SerialKissParity.Even,
                        ReconnectDelay = TimeSpan.FromSeconds(7)
                    }))
            ])
        };

        store.Save(saved);
        var loaded = NewStore().Load();

        var serial = Assert.Single(loaded.Connections.Ports).Configuration.SerialKiss!;
        Assert.Equal("/dev/tty.usbserial", serial.PortName);
        Assert.Equal(1200, serial.BaudRate);
        Assert.Equal(SerialKissParity.Even, serial.Parity);
        Assert.Equal(TimeSpan.FromSeconds(7), serial.ReconnectDelay);
    }

    [Fact]
    public void Save_StampsCurrentSchemaVersion()
    {
        var store = NewStore();

        store.Save(AppSettings.Default with { SchemaVersion = 0 });

        var loaded = NewStore().Load();
        Assert.Equal(AppSettings.CurrentSchemaVersion, loaded.SchemaVersion);
    }

    [Fact]
    public void Save_IsAtomic_NoLeftoverTempFile()
    {
        var store = NewStore();

        store.Save(AppSettings.Default with { Station = new StationProfile("W1AW", 41.7, -72.7, 50) });

        Assert.True(File.Exists(settingsPath));
        Assert.False(File.Exists(settingsPath + ".tmp"));
    }

    [Fact]
    public void Load_WithCorruptFile_ReturnsDefaultsAndDoesNotThrow()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, "{ this is not valid json ]]");

        var settings = NewStore().Load();

        Assert.Equal(AppSettings.Default.Station.Callsign, settings.Station.Callsign);
    }

    [Fact]
    public void Load_WithBrokenStationSection_SalvagesConnections()
    {
        // Save a good file, then corrupt ONLY the station section by hand.
        var store = NewStore();
        store.Save(AppSettings.Default with
        {
            Station = new StationProfile("KE4CON", 40.0, -83.0, 100),
            Connections = new ConnectionSettings(
            [
                new ConnectionPort(
                    "radio-1", "Radio 1", ConnectionPortType.NetworkTncKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForNetworkTncKiss(TcpKissConfiguration.Default with { Host = "192.168.1.50" }))
            ])
        });

        var root = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
        root["station"] = JsonValue.Create("not-an-object"); // break the station section
        File.WriteAllText(settingsPath, root.ToJsonString());

        var loaded = NewStore().Load();

        // Station falls back to default, but the connections section survives.
        Assert.Equal(AppSettings.Default.Station.Callsign, loaded.Station.Callsign);
        var port = Assert.Single(loaded.Connections.Ports);
        Assert.Equal("192.168.1.50", port.Configuration.NetworkTncKiss!.Host);
    }

    [Fact]
    public void Load_WithBlankCallsign_FallsBackToDefaultStation()
    {
        var store = NewStore();
        store.Save(AppSettings.Default with { Station = new StationProfile("   ", 1.0, 2.0, 10) });

        var loaded = NewStore().Load();

        Assert.Equal(AppSettings.Default.Station.Callsign, loaded.Station.Callsign);
    }

    [Fact]
    public void Load_MigratesPrePortListConnections_ToDefaultPort()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

        // A v1-style file: the old flat connections shape (no "ports" array), schemaVersion 1.
        var v1Json = """
        {
          "schemaVersion": 1,
          "station": { "callsign": "KE4CON", "latitude": 40.0, "longitude": -83.0, "filterRadiusKm": 100 },
          "connections": {
            "tcpKiss": { "host": "10.0.0.9", "port": 8001 },
            "aprsIs": { "callsign": "KE4CON", "passcode": "12345" }
          }
        }
        """;
        File.WriteAllText(settingsPath, v1Json);

        var loaded = NewStore().Load();

        Assert.Equal("KE4CON", loaded.Station.Callsign);      // station preserved
        Assert.Equal(2, loaded.SchemaVersion);                // migrated forward to v2
        var port = Assert.Single(loaded.Connections.Ports);   // default single APRS-IS port
        Assert.Equal(ConnectionPortType.AprsIs, port.Type);
        Assert.NotNull(port.Configuration.AprsIs);
    }

    [Fact]
    public void Update_ChangesOneSection_PreservesOthers()
    {
        var store = NewStore();
        store.Save(AppSettings.Default with
        {
            Station = new StationProfile("KE4CON", 40.0, -83.0, 100),
            Connections = new ConnectionSettings(
            [
                ConnectionPort.DefaultAprsIs(),
                new ConnectionPort(
                    "tnc", "Hardware TNC", ConnectionPortType.SerialKiss,
                    Enabled: false, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForSerialKiss(SerialKissConfiguration.Default with { PortName = "COM3" }))
            ])
        });

        // Change only the station; connections must remain intact.
        var result = NewStore().Update(s => s with
        {
            Station = s.Station with { FilterRadiusKm = 250 }
        });

        Assert.Equal(250, result.Station.FilterRadiusKm);
        Assert.Equal("KE4CON", result.Station.Callsign);
        Assert.Equal(2, result.Connections.Ports.Count);
        Assert.Contains(result.Connections.Ports, p => p.Configuration.SerialKiss?.PortName == "COM3");

        var reloaded = NewStore().Load();
        Assert.Equal(250, reloaded.Station.FilterRadiusKm);
        Assert.Equal("KE4CON", reloaded.Station.Callsign);
    }

    [Fact]
    public void StationProfileLoadSave_GoesThroughSameFile()
    {
        // Verifies the salvage/round-trip plumbing the way a caller would actually persist a profile.
        var store = NewStore();
        store.Save(AppSettings.Default with { Station = new StationProfile("N0CALL", 39.5, -98.35, 200) });

        var updated = store.Update(s => s with { Station = new StationProfile("KE4CON", 40.0, -83.0, 75) });

        Assert.Equal("KE4CON", updated.Station.Callsign);
        Assert.True(updated.Station.IsConfigured);
    }

    [Fact]
    public void Load_ImportsLegacyStationProfile_WhenNoSettingsFileExists()
    {
        // Simulate an old build's station-profile.json (PascalCase, as the previous serializer wrote it).
        var legacyPath = Path.Combine(tempDir, "legacy", "station-profile.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, JsonSerializer.Serialize(new StationProfile("KE4CON", 40.0, -83.0, 100)));

        var store = NewStore(legacyPath);
        var loaded = store.Load();

        Assert.Equal("KE4CON", loaded.Station.Callsign);
        Assert.True(File.Exists(settingsPath)); // legacy import is persisted to the new location
    }

    [Fact]
    public void Load_IgnoresMissingLegacyFile()
    {
        var store = NewStore(Path.Combine(tempDir, "nope", "station-profile.json"));

        var loaded = store.Load();

        Assert.Equal(AppSettings.Default.Station.Callsign, loaded.Station.Callsign);
    }
}
