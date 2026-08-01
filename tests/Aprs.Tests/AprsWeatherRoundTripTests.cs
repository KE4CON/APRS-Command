using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Weather generate ↔ parse round-trip: a weather observation formatted for transmit must parse back,
/// with our own parser, to the same meteorological values. Weather has the most fields of any APRS
/// format, so this is the most valuable round-trip to guard.
/// </summary>
public sealed class AprsWeatherRoundTripTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WeatherObservation_FormatThenParse_PreservesValues()
    {
        var observation = new CommonWeatherObservation(
            SourceName: "Round-trip WX",
            SourceType: WeatherObservationSourceType.WeatherFlowTempest,
            StationDeviceId: "dev-1",
            Callsign: "N0CALL",
            TimestampUtc: Now,
            Latitude: 39.058333,
            Longitude: -84.508333,
            WindDirectionDegrees: 180,
            WindSpeedMph: 5,
            WindGustMph: 10,
            TemperatureFahrenheit: 72,
            RainLastHourInches: 0,
            RainLast24HoursInches: 0,
            RainSinceMidnightInches: 0,
            HumidityPercent: 50,
            BarometricPressureMillibars: 1013.2,
            LuminosityWattsPerSquareMeter: null,
            UvIndex: null,
            SnowInches: null,
            LightningCount: null,
            LightningDistanceMiles: null,
            Diagnostics: new Dictionary<string, string>(),
            RawSourcePayload: "{}",
            StaleDataState: WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);

        var result = new AprsWeatherFormatter().FormatPreview(observation);
        Assert.True(result.IsSuccess);

        var parsed = new AprsParser().Parse(result.Packet!, Now);

        var wx = Assert.IsType<WeatherAprsPacket>(parsed);
        Assert.Equal(180, wx.WindDirectionDegrees);
        Assert.Equal(5, wx.WindSpeedMph);
        Assert.Equal(10, wx.WindGustMph);
        Assert.Equal(72, wx.TemperatureFahrenheit);
        Assert.Equal(50, wx.HumidityPercent);
        Assert.Equal(1013.2, wx.BarometricPressureMillibars!.Value, 1);
    }
}
