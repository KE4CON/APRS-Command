using System.Text.Json;
using Xunit;

namespace Aprs.Tests;

public sealed class DeveloperDocumentationExamplesTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void ExampleStationImportJsonParses()
    {
        using var document = ParseExample("examples/file-hooks/station-import.example.json");

        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("SIM001", document.RootElement.GetProperty("callsign").GetString());
    }

    [Fact]
    public void ExampleWeatherImportJsonParses()
    {
        using var document = ParseExample("examples/file-hooks/weather-import.example.json");

        Assert.Equal("TESTWX", document.RootElement.GetProperty("stationId").GetString());
        Assert.Equal("FileImport", document.RootElement.GetProperty("sourceMetadata").GetProperty("sourceType").GetString());
    }

    [Fact]
    public void ExamplePluginManifestJsonParses()
    {
        using var document = ParseExample("examples/plugins/plugin-manifest.example.json");

        Assert.Equal("example.weather.driver", document.RootElement.GetProperty("pluginId").GetString());
        Assert.False(document.RootElement.GetProperty("transmitEnabled").GetBoolean());
    }

    [Fact]
    public void WebSocketEnvelopeExampleParses()
    {
        using var document = ParseExample("examples/websocket/event-message-envelope.example.json");

        Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("StationUpdateDto", document.RootElement.GetProperty("payloadType").GetString());
    }

    [Fact]
    public void BlockedTransmitExampleDoesNotEnableTransmit()
    {
        using var document = ParseExample("examples/rest/blocked-transmit-request.example.json");

        Assert.False(document.RootElement.GetProperty("transmitEnabled").GetBoolean());
        Assert.Contains("Blocked", document.RootElement.GetProperty("notes").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ObjectGeoJsonExampleParses()
    {
        using var document = ParseExample("examples/file-hooks/object-import.example.geojson");

        Assert.Equal("FeatureCollection", document.RootElement.GetProperty("type").GetString());
        var feature = document.RootElement.GetProperty("features")[0];
        Assert.Equal("OBJTEST", feature.GetProperty("properties").GetProperty("objectName").GetString());
    }

    private static JsonDocument ParseExample(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot, relativePath);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.EnumerateFiles(current, "*.sln").Any())
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        throw new DirectoryNotFoundException("Could not find repository root for example validation tests.");
    }
}
