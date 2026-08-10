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

    // Audit H1: negative temperatures were read as 4 chars (should be 3), so every sub-freezing report
    // failed to parse temperature and dumped humidity/pressure into the comment. Round-trip must survive.
    [Theory]
    [InlineData(-5)]
    [InlineData(-20)]
    [InlineData(0)]
    [InlineData(99)]
    public void WeatherObservation_NegativeTemperature_RoundTrips(int temperatureFahrenheit)
    {
        var result = new AprsWeatherFormatter().FormatPreview(Observation(temperatureFahrenheit));
        Assert.True(result.IsSuccess);

        var wx = Assert.IsType<WeatherAprsPacket>(new AprsParser().Parse(result.Packet!, Now));
        Assert.Equal(temperatureFahrenheit, wx.TemperatureFahrenheit);
        // The fields AFTER temperature must still parse (they were previously lost into the comment).
        Assert.Equal(50, wx.HumidityPercent);
        Assert.Equal(1013.2, wx.BarometricPressureMillibars!.Value, 1);
    }

    // Audit H2: snow ('s' when wind speed is already known) was misread as wind speed and dropped.
    [Fact]
    public void WeatherObservation_Snow_RoundTripsAndDoesNotOverwriteWindSpeed()
    {
        var result = new AprsWeatherFormatter().FormatPreview(Observation(30, snowInches: 0.5));
        Assert.True(result.IsSuccess);

        var wx = Assert.IsType<WeatherAprsPacket>(new AprsParser().Parse(result.Packet!, Now));
        Assert.Equal(50, wx.SnowHundredthsInch); // 0.5 in = 50 hundredths
        Assert.Equal(5, wx.WindSpeedMph);        // wind speed must NOT be overwritten by the snow field
    }

    // Deep-audit HIGH: positionless weather (_) emitted a 6-digit HHMMSS timestamp, but the spec (and our
    // own parser, which tries MDHM first) require an 8-digit MMDDHHMM. The 6-digit form made the parser eat
    // two wind-direction digits as timestamp and lose every weather field. Positionless must round-trip.
    [Fact]
    public void WeatherObservation_Positionless_RoundTrips()
    {
        var options = AprsWeatherFormatterOptions.Default with { UsePosition = false };
        var result = new AprsWeatherFormatter().FormatPreview(Observation(72), localStationProfile: null, options);
        Assert.True(result.IsSuccess);
        Assert.StartsWith("_", result.Packet!.Split(':', 2)[1]); // positionless body starts with '_'

        var wx = Assert.IsType<WeatherAprsPacket>(new AprsParser().Parse(result.Packet!, Now));
        Assert.Equal(180, wx.WindDirectionDegrees);
        Assert.Equal(5, wx.WindSpeedMph);
        Assert.Equal(72, wx.TemperatureFahrenheit);
        Assert.Equal(50, wx.HumidityPercent);
        Assert.Equal(1013.2, wx.BarometricPressureMillibars!.Value, 1);
    }

    private static CommonWeatherObservation Observation(int temperatureFahrenheit, double? snowInches = null)
        => new(
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
            TemperatureFahrenheit: temperatureFahrenheit,
            RainLastHourInches: 0,
            RainLast24HoursInches: 0,
            RainSinceMidnightInches: 0,
            HumidityPercent: 50,
            BarometricPressureMillibars: 1013.2,
            LuminosityWattsPerSquareMeter: null,
            UvIndex: null,
            SnowInches: snowInches,
            LightningCount: null,
            LightningDistanceMiles: null,
            Diagnostics: new Dictionary<string, string>(),
            RawSourcePayload: "{}",
            StaleDataState: WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);
}
