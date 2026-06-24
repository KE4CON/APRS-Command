using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class RawPacketLogServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReceivedPacketCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddReceivedRawPacket("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Test", AprsPacketSource.AprsIs, "aprs-is", "APRS-IS", Now);

        Assert.NotNull(entry);
        Assert.Equal(RawPacketLogDirection.Received, entry.Direction);
        Assert.Equal(AprsPacketSource.AprsIs, entry.PacketSource);
        Assert.Equal("N0CALL", entry.SourceCallsign);
        Assert.Equal("APRS", entry.Destination);
        Assert.Equal("Position", entry.ParsedPacketType);
        Assert.True(entry.ParsedSuccessfully);
        Assert.Equal(RawPacketValidationStatus.Valid, entry.ValidationStatus);
    }

    [Fact]
    public void TransmittedPacketCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddTransmittedPacket("N0CALL>APRS:>Online", AprsPacketSource.AprsIs, "aprs-is", "APRS-IS", Now, "Succeeded");

        Assert.NotNull(entry);
        Assert.Equal(RawPacketLogDirection.Transmitted, entry.Direction);
        Assert.Equal("Succeeded", entry.RelatedTransmitResult);
    }

    [Fact]
    public void BlockedPacketCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddBlockedPacket("N0CALL>APRS:>Blocked", AprsPacketSource.Rf, "rf", "RF", Now, "RF transmit disabled");

        Assert.NotNull(entry);
        Assert.Equal(RawPacketLogDirection.Blocked, entry.Direction);
        Assert.Equal("RF transmit disabled", entry.RelatedTransmitResult);
    }

    [Fact]
    public void GeneratedPacketCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddGeneratedPacket("N0CALL>APRS:>Generated", timestampUtc: Now);

        Assert.NotNull(entry);
        Assert.Equal(RawPacketLogDirection.Generated, entry.Direction);
        Assert.Equal(AprsPacketSource.LocalGenerated, entry.PacketSource);
    }

    [Fact]
    public void RecentEntriesReturnNewestFirst()
    {
        var service = CreateService();
        service.AddReceivedRawPacket("OLD1>APRS:>Old", AprsPacketSource.Replay, timestampUtc: Now);
        service.AddReceivedRawPacket("NEW1>APRS:>New", AprsPacketSource.Replay, timestampUtc: Now.AddMinutes(1));

        var entries = service.GetRecentEntries();

        Assert.Equal("NEW1", entries[0].SourceCallsign);
        Assert.Equal("OLD1", entries[1].SourceCallsign);
    }

    [Fact]
    public void FilteringByCallsignWorks()
    {
        var service = CreateService();
        service.AddReceivedRawPacket("N0CALL>APRS:>One", AprsPacketSource.AprsIs, timestampUtc: Now);
        service.AddReceivedRawPacket("W1AW>APRS:>Two", AprsPacketSource.AprsIs, timestampUtc: Now);

        var entries = service.GetEntriesBySourceCallsign("n0call");

        var entry = Assert.Single(entries);
        Assert.Equal("N0CALL", entry.SourceCallsign);
    }

    [Fact]
    public void FilteringByPacketSourceWorks()
    {
        var service = CreateService();
        service.AddReceivedRawPacket("N0CALL>APRS:>One", AprsPacketSource.AprsIs, timestampUtc: Now);
        service.AddReceivedRawPacket("W1AW>APRS:>Two", AprsPacketSource.TcpKiss, timestampUtc: Now);

        var entries = service.GetEntriesByPacketSource(AprsPacketSource.TcpKiss);

        var entry = Assert.Single(entries);
        Assert.Equal("W1AW", entry.SourceCallsign);
    }

    [Fact]
    public void FilteringByDirectionWorks()
    {
        var service = CreateService();
        service.AddReceivedRawPacket("N0CALL>APRS:>One", AprsPacketSource.AprsIs, timestampUtc: Now);
        service.AddBlockedPacket("W1AW>APRS:>Two", AprsPacketSource.Rf, timestampUtc: Now);

        var entries = service.GetEntriesByDirection(RawPacketLogDirection.Blocked);

        var entry = Assert.Single(entries);
        Assert.Equal("W1AW", entry.SourceCallsign);
    }

    [Fact]
    public void FilteringByPacketTypeWorks()
    {
        var service = CreateService();
        service.AddReceivedRawPacket("N0CALL>APRS:>Status", AprsPacketSource.AprsIs, timestampUtc: Now);
        service.AddReceivedRawPacket("WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", AprsPacketSource.AprsIs, timestampUtc: Now);

        var entries = service.GetEntriesByPacketType("Weather");

        var entry = Assert.Single(entries);
        Assert.Equal("WX9XYZ", entry.SourceCallsign);
    }

    [Fact]
    public void SearchByRawPacketTextWorks()
    {
        var service = CreateService();
        service.AddReceivedRawPacket("N0CALL>APRS:>Net control", AprsPacketSource.AprsIs, timestampUtc: Now);
        service.AddReceivedRawPacket("W1AW>APRS:>Bulletin", AprsPacketSource.AprsIs, timestampUtc: Now);

        var entries = service.SearchPacketText("net control");

        var entry = Assert.Single(entries);
        Assert.Equal("N0CALL", entry.SourceCallsign);
    }

    [Fact]
    public void MaximumInMemoryLogSizeIsEnforced()
    {
        var service = CreateService(RawPacketLogConfiguration.Default with { MaximumInMemoryEntries = 2 });

        service.AddReceivedRawPacket("ONE>APRS:>One", AprsPacketSource.Simulation, timestampUtc: Now);
        service.AddReceivedRawPacket("TWO>APRS:>Two", AprsPacketSource.Simulation, timestampUtc: Now.AddSeconds(1));
        service.AddReceivedRawPacket("THREE>APRS:>Three", AprsPacketSource.Simulation, timestampUtc: Now.AddSeconds(2));

        var entries = service.GetRecentEntries();

        Assert.Equal(2, entries.Count);
        Assert.DoesNotContain(entries, entry => entry.SourceCallsign == "ONE");
    }

    [Fact]
    public void ClearingLogWorks()
    {
        var service = CreateService();
        service.AddReceivedRawPacket("N0CALL>APRS:>One", AprsPacketSource.AprsIs, timestampUtc: Now);

        service.ClearLog();

        Assert.Empty(service.GetRecentEntries());
    }

    [Fact]
    public void CredentialLikeFieldsAreRedacted()
    {
        var service = CreateService();

        var entry = service.AddBlockedPacket(
            "user N0CALL pass 12345 vers APRSCommand 1.0",
            AprsPacketSource.External,
            timestampUtc: Now,
            relatedTransmitResult: "token=abcdef api_key=secret password:hunter2",
            notes: "passcode=99999");

        Assert.NotNull(entry);
        Assert.DoesNotContain("12345", entry.RawPacketText);
        Assert.DoesNotContain("abcdef", entry.RelatedTransmitResult);
        Assert.DoesNotContain("secret", entry.RelatedTransmitResult);
        Assert.DoesNotContain("hunter2", entry.RelatedTransmitResult);
        Assert.DoesNotContain("99999", entry.Notes);
        Assert.Contains("[REDACTED]", entry.RawPacketText);
    }

    private static RawPacketLogService CreateService(RawPacketLogConfiguration? configuration = null)
    {
        return new RawPacketLogService(configuration: configuration, clock: new FakeClock { UtcNow = Now });
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
