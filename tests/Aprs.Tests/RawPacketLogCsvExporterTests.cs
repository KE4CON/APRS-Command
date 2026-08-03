using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class RawPacketLogCsvExporterTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstLineIsTheCsvHeaderTheReplayLoaderRecognizes()
    {
        var lines = RawPacketLogCsvExporter.ToCsvLines([]);

        var header = Assert.Single(lines);
        Assert.Equal(RawPacketLogCsvExporter.HeaderLine, header);
        // The loader routes to CSV mode only when the first line contains "RawPacketText".
        Assert.Contains("RawPacketText", header);
    }

    [Fact]
    public void FieldsContainingCommasOrQuotesAreCsvEscaped()
    {
        var entry = CreateEntry("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-With \"quotes\"", notes: "note,with,commas");

        var row = RawPacketLogCsvExporter.ToCsvLines([entry])[1];

        Assert.Contains("\"N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-With \"\"quotes\"\"\"", row);
        Assert.Contains("\"note,with,commas\"", row);
    }

    [Fact]
    public async Task ExportedLogRoundTripsBackThroughTheReplayLoader()
    {
        var entries = new[]
        {
            CreateEntry("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Comma, in body", AprsPacketSource.AprsIs, "captured"),
            CreateEntry("W1AW>APRS:>Plain status", AprsPacketSource.Rf)
        };

        var path = Path.Combine(Path.GetTempPath(), $"aprs-export-{Guid.NewGuid():N}.aprslog");
        await File.WriteAllLinesAsync(path, RawPacketLogCsvExporter.ToCsvLines(entries));

        try
        {
            var service = new ReplayService(new NoOpReplayPacketSink());

            var loaded = await service.LoadFromFileAsync(path);

            Assert.Equal(2, loaded.Count);
            Assert.Equal(ReplaySessionState.Ready, service.GetStatus().State);
            // Order is by timestamp; both entries survive with their raw text and original source intact.
            var aprsIs = Assert.Single(loaded, e => e.OriginalPacketSource == AprsPacketSource.AprsIs);
            Assert.Equal("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Comma, in body", aprsIs.RawPacketText);
            Assert.Equal("captured", aprsIs.Notes);
            Assert.Contains(loaded, e => e.OriginalPacketSource == AprsPacketSource.Rf);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static RawPacketLogEntry CreateEntry(
        string rawPacket,
        AprsPacketSource source = AprsPacketSource.AprsIs,
        string? notes = null)
    {
        return new RawPacketLogEntry(
            Guid.NewGuid(),
            Now,
            rawPacket,
            null,
            null,
            null,
            [],
            source,
            RawPacketLogDirection.Received,
            null,
            null,
            RawPacketValidationStatus.Valid,
            [],
            [],
            true,
            null,
            notes);
    }
}
