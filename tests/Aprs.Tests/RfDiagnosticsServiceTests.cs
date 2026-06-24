using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class RfDiagnosticsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
    private readonly AprsParser parser = new();

    [Fact]
    public void RfPacketIsAnalyzed()
    {
        var service = CreateService();

        var diagnostic = service.AcceptPacket(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF", Now);

        Assert.True(diagnostic.IsReceivedFromRf);
        Assert.Equal(AprsPacketSource.Rf, diagnostic.PacketSource);
        Assert.Equal(RfDiagnosticLinkState.RfOnly, diagnostic.LinkState);
        Assert.Equal("Position", diagnostic.ParsedPacketType);
        Assert.Equal("RF", diagnostic.ReceivedPortOrSource);
    }

    [Fact]
    public void AprsIsPacketIsAnalyzed()
    {
        var service = CreateService();

        var diagnostic = service.AcceptPacket(Parse("MOBILE1>APRS,TCPIP*,qAC,T2TEST:!3903.50N/08430.50W>Mobile"), AprsPacketSource.AprsIs, "APRS-IS", Now);

        Assert.False(diagnostic.IsReceivedFromRf);
        Assert.True(diagnostic.WasAlsoSeenOnAprsIs);
        Assert.Equal(RfDiagnosticLinkState.AprsIsOnly, diagnostic.LinkState);
        Assert.Equal("qAC", diagnostic.QConstruct);
        Assert.Contains(diagnostic.ValidationWarnings, warning => warning.Contains("q construct", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DuplicatePacketIsDetected()
    {
        var service = CreateService();
        service.AcceptPacket(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF", Now);

        var duplicate = service.AcceptPacket(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", Now.AddSeconds(30)), AprsPacketSource.Rf, "RF", Now.AddSeconds(30));

        Assert.Equal(RfDiagnosticDuplicateState.ConfirmedDuplicate, duplicate.DuplicateState);
        Assert.Equal(2, duplicate.DuplicateCount);
    }

    [Fact]
    public void DuplicateDetectionRespectsTimeWindow()
    {
        var service = CreateService(new RfDiagnosticsConfiguration(
            true,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(1),
            100,
            10,
            100,
            4,
            null));
        service.AcceptPacket(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF", Now);

        var later = service.AcceptPacket(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", Now.AddSeconds(11)), AprsPacketSource.Rf, "RF", Now.AddSeconds(11));

        Assert.Equal(RfDiagnosticDuplicateState.NotDuplicate, later.DuplicateState);
    }

    [Fact]
    public void PacketRateByCallsignIsCalculated()
    {
        var service = CreateService();
        service.AcceptPacket(Parse("MOBILE1>APRS:>One"), AprsPacketSource.Rf, "RF", Now);
        service.AcceptPacket(Parse("MOBILE1>APRS:>Two", Now.AddSeconds(10)), AprsPacketSource.Rf, "RF", Now.AddSeconds(10));
        service.AcceptPacket(Parse("WX9XYZ>APRS:>Other", Now.AddSeconds(20)), AprsPacketSource.Rf, "RF", Now.AddSeconds(20));

        var rates = service.GetPacketRateByCallsign();

        Assert.Equal(2, rates["MOBILE1"]);
        Assert.Equal(1, rates["WX9XYZ"]);
    }

    [Fact]
    public void PacketRateBySourcePortIsCalculated()
    {
        var service = CreateService();
        service.AcceptPacket(Parse("MOBILE1>APRS:>One"), AprsPacketSource.Rf, "RF", Now);
        service.AcceptPacket(Parse("WX9XYZ>APRS:>Other", Now.AddSeconds(20)), AprsPacketSource.AprsIs, "APRS-IS", Now.AddSeconds(20));

        var rates = service.GetPacketRateBySourcePort();

        Assert.Equal(1, rates["RF"]);
        Assert.Equal(1, rates["APRS-IS"]);
    }

    [Fact]
    public void ExcessiveBeaconWarningIsGenerated()
    {
        var service = CreateService(new RfDiagnosticsConfiguration(
            true,
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(10),
            TimeSpan.FromMinutes(1),
            100,
            2,
            100,
            4,
            null));

        service.AcceptPacket(Parse("MOBILE1>APRS:>One"), AprsPacketSource.Rf, "RF", Now);
        service.AcceptPacket(Parse("MOBILE1>APRS:>Two", Now.AddSeconds(10)), AprsPacketSource.Rf, "RF", Now.AddSeconds(10));
        var third = service.AcceptPacket(Parse("MOBILE1>APRS:>Three", Now.AddSeconds(20)), AprsPacketSource.Rf, "RF", Now.AddSeconds(20));

        Assert.Contains(third.ValidationWarnings, warning => warning.Contains("exceeded", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(service.GetSummary().ExcessiveBeaconWarnings);
    }

    [Fact]
    public void OverlyLongPathWarningIsGenerated()
    {
        var service = CreateService();

        var diagnostic = service.AcceptPacket(Parse("MOBILE1>APRS,WIDE1-1,WIDE2-1,WIDE3-1,WIDE4-1,WIDE5-1:>Long path"), AprsPacketSource.Rf, "RF", Now);

        Assert.Contains(diagnostic.ValidationWarnings, warning => warning.Contains("longer", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(service.GetSummary().PathWarnings);
    }

    [Fact]
    public void UsedDigipeaterPathComponentIsDetected()
    {
        var service = CreateService();

        var diagnostic = service.AcceptPacket(Parse("MOBILE1>APRS,WIDE1-1*,WIDE2-1:>Used path"), AprsPacketSource.Rf, "RF", Now);

        Assert.Contains("WIDE1-1*", diagnostic.HeardViaPathComponents);
        Assert.Contains(diagnostic.ValidationWarnings, warning => warning.Contains("used digipeater", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TcpipAndQConstructAreRecognized()
    {
        var service = CreateService();

        var diagnostic = service.AcceptPacket(Parse("MOBILE1>APRS,TCPIP*,qAC,T2TEST:>Internet"), AprsPacketSource.AprsIs, "APRS-IS", Now);

        Assert.Equal("qAC", diagnostic.QConstruct);
        Assert.Contains(diagnostic.ValidationWarnings, warning => warning.Contains("TCPIP", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RfOnlyAprsIsOnlyAndBothAreClassified()
    {
        var service = CreateService();
        service.AcceptPacket(Parse("RFONLY>APRS:>Rf"), AprsPacketSource.Rf, "RF", Now);
        service.AcceptPacket(Parse("ISONLY>APRS,TCPIP*,qAC,T2TEST:>Is"), AprsPacketSource.AprsIs, "APRS-IS", Now.AddSeconds(1));
        service.AcceptPacket(Parse("BOTH>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", Now.AddSeconds(2)), AprsPacketSource.Rf, "RF", Now.AddSeconds(2));

        var both = service.AcceptPacket(Parse("BOTH>APRS,TCPIP*,qAC,T2TEST:!3903.50N/08430.50W>Mobile", Now.AddSeconds(3)), AprsPacketSource.AprsIs, "APRS-IS", Now.AddSeconds(3));
        var summary = service.GetSummary();

        Assert.Equal(RfDiagnosticLinkState.SeenOnBothRfAndAprsIs, both.LinkState);
        Assert.Equal(1, summary.RfOnlyPacketCount);
        Assert.Equal(1, summary.AprsIsOnlyPacketCount);
        Assert.Equal(2, summary.SeenOnBothRfAndAprsIsPacketCount);
    }

    [Fact]
    public void SummaryCountsAreCorrect()
    {
        var service = CreateService();
        service.AcceptPacket(Parse("MOBILE1>APRS:>One"), AprsPacketSource.Rf, "RF", Now);
        service.AcceptPacket(Parse("MOBILE1>APRS:>One", Now.AddSeconds(1)), AprsPacketSource.Rf, "RF", Now.AddSeconds(1));
        service.AcceptPacket(Parse("WX9XYZ>APRS,TCPIP*,qAC,T2TEST:>Weather", Now.AddSeconds(2)), AprsPacketSource.AprsIs, "APRS-IS", Now.AddSeconds(2));

        var summary = service.GetSummary();

        Assert.Equal(3, summary.TotalPacketsAnalyzed);
        Assert.Equal(2, summary.RfPackets);
        Assert.Equal(1, summary.AprsIsPackets);
        Assert.Equal(1, summary.DuplicatePackets);
        Assert.Equal(2, summary.UniqueStations);
        Assert.NotEmpty(summary.TopPacketSources);
        Assert.NotNull(summary.LastUpdatedTimestampUtc);
    }

    [Fact]
    public void ClearingDiagnosticsWorks()
    {
        var service = CreateService();
        service.AcceptPacket(Parse("MOBILE1>APRS:>One"), AprsPacketSource.Rf, "RF", Now);

        service.ClearDiagnostics();

        Assert.Empty(service.GetRecentPackets());
        Assert.Equal(0, service.GetSummary().TotalPacketsAnalyzed);
    }

    private AprsPacket Parse(string rawPacket)
    {
        return Parse(rawPacket, Now);
    }

    private AprsPacket Parse(string rawPacket, DateTimeOffset timestamp)
    {
        parser.TryParse(rawPacket, timestamp, out var packet, out _);
        return packet!;
    }

    private static RfDiagnosticsService CreateService(RfDiagnosticsConfiguration? configuration = null)
    {
        return new RfDiagnosticsService(configuration);
    }
}
