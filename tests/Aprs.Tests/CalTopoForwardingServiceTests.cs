using Aprs.Core;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Services;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class CalTopoForwardingServiceTests
{
    private static readonly DateTimeOffset TestTime =
        new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static CalTopoSettings EnabledSettings(int intervalSeconds = 60) => new(
        Enabled:                true,
        MapId:                  "TEST1",
        ForwardAprsIsPackets:   true,
        ForwardRfPackets:       true,
        MinimumIntervalSeconds: intervalSeconds);

    private static CalTopoSettings DisabledSettings() => new(
        Enabled:                false,
        MapId:                  "TEST1",
        ForwardAprsIsPackets:   true,
        ForwardRfPackets:       true,
        MinimumIntervalSeconds: 60);

    private static PositionAprsPacket MakePosition(
        string callsign = "KE4CON",
        int? ssid = null,
        double lat = 41.975,
        double lon = -88.455) =>
        new(
            RawLine:            $"{callsign}>APRS:!4118.50N/08827.30W>",
            SourceCallsign:     callsign,
            SourceSsid:         ssid,
            Destination:        "APRS",
            Path:               [],
            Information:        "!4118.50N/08827.30W>",
            ReceivedAtUtc:      TestTime,
            IsValid:            true,
            ValidationErrors:   [],
            QConstruct:         null,
            PositionType:       '!',
            Timestamp:          null,
            Latitude:           lat,
            Longitude:          lon,
            SymbolTableIdentifier: '/',
            SymbolCode:         '>',
            Comment:            string.Empty,
            CourseDegrees:      null,
            SpeedKnots:         null,
            AltitudeFeet:       null,
            PositionAmbiguity:  0);

    private static ParsedPacketEventArgs MakeArgs(AprsPacket packet,
        AprsPacketSource source = AprsPacketSource.AprsIs) =>
        new(packet, source);

    // ── Basic filtering ───────────────────────────────────────────────────

    [Fact]
    public void OnPacketParsed_WhenDisabled_DoesNotForward()
    {
        var svc = new CalTopoForwardingService(DisabledSettings());
        svc.OnPacketParsed(null, MakeArgs(MakePosition()));
        Assert.Equal(0, svc.ForwardedStationCount);
    }

    [Fact]
    public void OnPacketParsed_WhenMapIdEmpty_DoesNotForward()
    {
        var settings = EnabledSettings() with { MapId = "" };
        var svc = new CalTopoForwardingService(settings);
        svc.OnPacketParsed(null, MakeArgs(MakePosition()));
        Assert.Equal(0, svc.ForwardedStationCount);
    }

    [Fact]
    public void OnPacketParsed_ValidPosition_IncrementsForwardedCount()
    {
        var svc = new CalTopoForwardingService(EnabledSettings());
        svc.OnPacketParsed(null, MakeArgs(MakePosition()));
        Assert.Equal(1, svc.ForwardedStationCount);
    }

    [Fact]
    public void OnPacketParsed_NullPacket_DoesNotForward()
    {
        var svc = new CalTopoForwardingService(EnabledSettings());
        svc.OnPacketParsed(null, new ParsedPacketEventArgs(null, AprsPacketSource.AprsIs));
        Assert.Equal(0, svc.ForwardedStationCount);
    }

    [Fact]
    public void OnPacketParsed_InvalidPosition_DoesNotForward()
    {
        var svc = new CalTopoForwardingService(EnabledSettings());
        var pos = MakePosition() with { IsValid = false };
        svc.OnPacketParsed(null, MakeArgs(pos));
        Assert.Equal(0, svc.ForwardedStationCount);
    }

    [Fact]
    public void OnPacketParsed_PositionWithNullLatitude_DoesNotForward()
    {
        var svc = new CalTopoForwardingService(EnabledSettings());
        var pos = MakePosition() with { Latitude = null };
        svc.OnPacketParsed(null, MakeArgs(pos));
        Assert.Equal(0, svc.ForwardedStationCount);
    }

    [Fact]
    public void OnPacketParsed_NonPositionPacket_DoesNotForward()
    {
        var svc = new CalTopoForwardingService(EnabledSettings());
        var msg = new MessageAprsPacket(
            "N0CALL>APRS::KE4CON   :Hello{001",
            "N0CALL", null, "APRS", [], ":KE4CON   :Hello{001",
            TestTime, true, [],
            null, "KE4CON", "Hello{001", "Hello", "001",
            null, null, false, null, false, false, null);
        svc.OnPacketParsed(null, MakeArgs(msg));
        Assert.Equal(0, svc.ForwardedStationCount);
    }

    // ── Source filtering ──────────────────────────────────────────────────

    [Fact]
    public void OnPacketParsed_AprsIsPacket_WhenAprsIsDisabled_DoesNotForward()
    {
        var settings = EnabledSettings() with { ForwardAprsIsPackets = false };
        var svc = new CalTopoForwardingService(settings);
        svc.OnPacketParsed(null, MakeArgs(MakePosition(), AprsPacketSource.AprsIs));
        Assert.Equal(0, svc.ForwardedStationCount);
    }

    [Fact]
    public void OnPacketParsed_RfPacket_WhenRfDisabled_DoesNotForward()
    {
        var settings = EnabledSettings() with { ForwardRfPackets = false };
        var svc = new CalTopoForwardingService(settings);
        svc.OnPacketParsed(null, MakeArgs(MakePosition(), AprsPacketSource.TcpKiss));
        Assert.Equal(0, svc.ForwardedStationCount);
    }

    [Fact]
    public void OnPacketParsed_RfPacket_WhenRfEnabled_Forwards()
    {
        var svc = new CalTopoForwardingService(EnabledSettings());
        svc.OnPacketParsed(null, MakeArgs(MakePosition(), AprsPacketSource.TcpKiss));
        Assert.Equal(1, svc.ForwardedStationCount);
    }

    // ── Rate limiting ─────────────────────────────────────────────────────

    [Fact]
    public void OnPacketParsed_SameCallsignTwice_SecondIsRateLimited()
    {
        var svc = new CalTopoForwardingService(EnabledSettings(intervalSeconds: 60));
        var pos = MakePosition("KE4CON");
        svc.OnPacketParsed(null, MakeArgs(pos));
        svc.OnPacketParsed(null, MakeArgs(pos)); // should be rate-limited
        // Still only 1 station tracked (same callsign)
        Assert.Equal(1, svc.ForwardedStationCount);
    }

    [Fact]
    public void OnPacketParsed_DifferentCallsigns_BothForwarded()
    {
        var svc = new CalTopoForwardingService(EnabledSettings());
        svc.OnPacketParsed(null, MakeArgs(MakePosition("KE4CON")));
        svc.OnPacketParsed(null, MakeArgs(MakePosition("W9ABC")));
        Assert.Equal(2, svc.ForwardedStationCount);
    }

    [Fact]
    public void OnPacketParsed_SameCallsignDifferentSSID_BothForwarded()
    {
        var svc = new CalTopoForwardingService(EnabledSettings());
        svc.OnPacketParsed(null, MakeArgs(MakePosition("KE4CON", ssid: null)));
        svc.OnPacketParsed(null, MakeArgs(MakePosition("KE4CON", ssid: 9)));
        Assert.Equal(2, svc.ForwardedStationCount);
    }

    // ── Settings management ───────────────────────────────────────────────

    [Fact]
    public void ApplySettings_DisablesForwarding_AfterEnable()
    {
        var svc = new CalTopoForwardingService(EnabledSettings());
        svc.OnPacketParsed(null, MakeArgs(MakePosition("KE4CON")));
        Assert.Equal(1, svc.ForwardedStationCount);

        svc.ApplySettings(DisabledSettings());
        svc.OnPacketParsed(null, MakeArgs(MakePosition("W9ABC")));
        // W9ABC should not be forwarded now that settings are disabled
        Assert.Equal(1, svc.ForwardedStationCount);
    }

    [Fact]
    public void ResetRateLimits_ClearsForwardedCount()
    {
        var svc = new CalTopoForwardingService(EnabledSettings());
        svc.OnPacketParsed(null, MakeArgs(MakePosition("KE4CON")));
        Assert.Equal(1, svc.ForwardedStationCount);

        svc.ResetRateLimits();
        Assert.Equal(0, svc.ForwardedStationCount);
    }

    // ── Default settings ──────────────────────────────────────────────────

    [Fact]
    public void DefaultSettings_AreDisabledWithEmptyMapId()
    {
        var d = CalTopoSettings.Default;
        Assert.False(d.Enabled);
        Assert.Equal(string.Empty, d.MapId);
        Assert.Equal(60, d.MinimumIntervalSeconds);
        Assert.True(d.ForwardAprsIsPackets);
        Assert.True(d.ForwardRfPackets);
    }
}
