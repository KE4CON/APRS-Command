using System;
using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Additional Peet Bros parser tests covering paths not exercised by the
/// existing PeetBrosWeatherInputDriverTests: full APRS-framed packets with
/// callsign prefix, model name propagation, timestamp parsing, and
/// multi-delimiter payloads.
/// </summary>
public sealed class PeetBrosParserAdditionalTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    private static PeetBrosConfiguration Config(string? model = null) =>
        PeetBrosConfiguration.Default with
        {
            Enabled = true,
            SerialPortName = "TEST",
            ModelName = model
        };

    // ── APRS-framed payloads ──────────────────────────────────────────────

    [Fact]
    public void FullAprsFrame_WithCallsignPrefix_IsParsedCorrectly()
    {
        // Full APRS packet as received over serial (some stations send complete frames)
        const string payload = "KE4CON-13>APRS:_111111c270s012g018t068r000p000P000h45b10150";
        var result = new PeetBrosWeatherParser().Parse(payload, Config(), ReceivedAt);

        Assert.True(result.IsHandled);
        var obs = result.Observation!;
        Assert.Equal(270, obs.WindDirectionDegrees);
        Assert.Equal(12, obs.WindSpeedMph);
        Assert.Equal(68, obs.TemperatureFahrenheit);
        Assert.Equal(45, obs.HumidityPercent);
        Assert.Equal("aprs-weather", obs.Diagnostics["format"]);
    }

    [Fact]
    public void AprsWeather_WithModelName_IncludesModelInDiagnostics()
    {
        const string payload = "_111111c090s008g015t074r000p000P000h60b10200";
        var result = new PeetBrosWeatherParser().Parse(payload, Config("ULTIMETER2100"), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal("ULTIMETER2100", result.Diagnostics["model"]);
    }

    [Fact]
    public void AprsWeather_WithoutModelName_OmitsModelFromDiagnostics()
    {
        const string payload = "_111111c090s008g015t074r000p000P000h60b10200";
        var result = new PeetBrosWeatherParser().Parse(payload, Config(model: null), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.False(result.Diagnostics.ContainsKey("model"));
    }

    // ── Key-value model name propagation ──────────────────────────────────

    [Fact]
    public void KeyValue_DeviceField_TakesPriorityOverConfigModelName()
    {
        const string payload = "DEVICE=MyStation2000,WD=180,WS=5,T=72,H=50,B=1013.2";
        var result = new PeetBrosWeatherParser().Parse(payload, Config("ULTIMETER2100"), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal("MyStation2000", result.Observation!.StationDeviceId);
    }

    [Fact]
    public void KeyValue_NoDeviceField_FallsBackToConfigModelName()
    {
        const string payload = "WD=180,WS=5,T=72,H=50,B=1013.2";
        var result = new PeetBrosWeatherParser().Parse(payload, Config("ULTIMETER800"), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal("ULTIMETER800", result.Observation!.StationDeviceId);
    }

    [Fact]
    public void KeyValue_NoDeviceOrModel_UsesDefaultStationName()
    {
        const string payload = "WD=180,WS=5,T=72,H=50,B=1013.2";
        var result = new PeetBrosWeatherParser().Parse(payload, Config(model: null), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal("Peet Bros ULTIMETER", result.Observation!.StationDeviceId);
    }

    // ── Timestamp parsing ─────────────────────────────────────────────────

    [Fact]
    public void KeyValue_UnixTimestamp_IsUsedAsObservationTime()
    {
        var expectedTime = new DateTimeOffset(2026, 6, 1, 10, 30, 0, TimeSpan.Zero);
        var ts = expectedTime.ToUnixTimeSeconds();
        var payload = $"MODEL=ULTIMETER2100,WD=180,WS=5,T=72,H=50,B=1013.2,TS={ts}";

        var result = new PeetBrosWeatherParser().Parse(payload, Config(), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal(expectedTime, result.Observation!.TimestampUtc);
    }

    [Fact]
    public void KeyValue_NoTimestamp_UsesReceivedAtTime()
    {
        const string payload = "MODEL=ULTIMETER2100,WD=180,WS=5,T=72,H=50,B=1013.2";
        var result = new PeetBrosWeatherParser().Parse(payload, Config(), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal(ReceivedAt, result.Observation!.TimestampUtc);
    }

    // ── Delimiter variants ────────────────────────────────────────────────

    [Fact]
    public void SemicolonDelimitedPayload_IsParsedCorrectly()
    {
        const string payload = "MODEL=ULTIMETER2100;WD=90;WS=10;T=68;H=55;B=1015.0";
        var result = new PeetBrosWeatherParser().Parse(payload, Config(), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal(90, result.Observation!.WindDirectionDegrees);
        Assert.Equal(10.0, result.Observation.WindSpeedMph);
    }

    [Fact]
    public void SpaceDelimitedPayload_IsParsedCorrectly()
    {
        const string payload = "MODEL=ULTIMETER2100 WD=270 WS=15 T=65 H=70 B=1010.0";
        var result = new PeetBrosWeatherParser().Parse(payload, Config(), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal(270, result.Observation!.WindDirectionDegrees);
        Assert.Equal(15.0, result.Observation.WindSpeedMph);
    }

    // ── Native ULTIMETER block format (documented gap) ────────────────────

    [Fact]
    public void NativeUltimeterBlock_IsNotRecognizedYet()
    {
        // The native Peet Bros binary/hex block format (e.g. "!!0054---00BE...")
        // is not implemented — the exact packet structure varies by model and
        // requires hardware-confirmed byte mappings. The parser returns a clear
        // failure rather than corrupted data.
        const string nativeBlock = "!!0054---00BE00640000----00000000";
        var result = new PeetBrosWeatherParser().Parse(nativeBlock, Config(), ReceivedAt);

        Assert.False(result.IsHandled);
        Assert.NotNull(result.Error);
        Assert.Null(result.Observation);
        // Confirm it doesn't crash
    }

    [Fact]
    public void EmptyPayload_FailsWithClearError()
    {
        var result = new PeetBrosWeatherParser().Parse("", Config(), ReceivedAt);
        Assert.False(result.IsHandled);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhitespaceOnlyPayload_FailsWithClearError()
    {
        var result = new PeetBrosWeatherParser().Parse("   \t  ", Config(), ReceivedAt);
        Assert.False(result.IsHandled);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
