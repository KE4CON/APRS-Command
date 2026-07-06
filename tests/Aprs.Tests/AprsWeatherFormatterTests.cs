using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsWeatherFormatterTests
{
    private static readonly DateTimeOffset TestTime = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CommonWeatherObservation_StoresNormalizedData()
    {
        var observation = CreateObservation(
            temperatureFahrenheit: WeatherUnitConverter.CelsiusToFahrenheit(20),
            windSpeedMph: WeatherUnitConverter.KilometersPerHourToMilesPerHour(16.09344),
            rainLastHourInches: WeatherUnitConverter.MillimetersToInches(2.54),
            pressureMillibars: WeatherUnitConverter.InchesOfMercuryToMillibars(29.92));

        Assert.Equal(68, observation.TemperatureFahrenheit!.Value, 3);
        Assert.Equal(10, observation.WindSpeedMph!.Value, 3);
        Assert.Equal(0.1, observation.RainLastHourInches!.Value, 3);
        Assert.Equal(1013.2, observation.BarometricPressureMillibars!.Value, 1);
        Assert.Equal(WeatherObservationSourceType.WeatherFlowTempest, observation.SourceType);
        Assert.Equal("hub-123", observation.StationDeviceId);
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(20, 68)]
    [InlineData(-10, 14)]
    public void CelsiusConvertsToFahrenheit(double celsius, double expected)
    {
        Assert.Equal(expected, WeatherUnitConverter.CelsiusToFahrenheit(celsius), 3);
    }

    [Fact]
    public void WindUnitsConvertToMilesPerHour()
    {
        Assert.Equal(10, WeatherUnitConverter.KilometersPerHourToMilesPerHour(16.09344), 3);
        Assert.Equal(11.508, WeatherUnitConverter.KnotsToMilesPerHour(10), 3);
        Assert.Equal(11.185, WeatherUnitConverter.MetersPerSecondToMilesPerHour(5), 3);
    }

    [Fact]
    public void RainAndPressureUnitsConvertToAprsFriendlyUnits()
    {
        Assert.Equal(1, WeatherUnitConverter.MillimetersToInches(25.4), 3);
        Assert.Equal(1013.25, WeatherUnitConverter.HectopascalsToMillibars(1013.25), 3);
        Assert.Equal(1013.2, WeatherUnitConverter.InchesOfMercuryToMillibars(29.92), 1);
    }

    [Fact]
    public void InvalidHumidityFailsValidation()
    {
        var validator = new WeatherObservationValidator();

        var result = validator.Validate(CreateObservation(humidity: 101));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Humidity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidPressureFailsValidation()
    {
        var validator = new WeatherObservationValidator();

        var result = validator.Validate(CreateObservation(pressureMillibars: 500));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("pressure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NegativeRainFailsValidation()
    {
        var validator = new WeatherObservationValidator();

        var result = validator.Validate(CreateObservation(rainLastHourInches: -0.01));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Rain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StaleWeatherObservationIsRejectedForAprsFormatting()
    {
        var formatter = new AprsWeatherFormatter();

        var result = formatter.FormatPreview(CreateObservation(staleDataState: WeatherDataState.Stale));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Packet);
        Assert.Contains(result.ValidationErrors, error => error.Contains("Stale weather data", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidObservationFormatsPositionWeatherPacketPreview()
    {
        var formatter = new AprsWeatherFormatter();
        var options = AprsWeatherFormatterOptions.Default with { Path = ["TCPIP*"] };

        var result = formatter.FormatPreview(CreateObservation(), options: options);

        Assert.True(result.IsSuccess);
        Assert.Equal("N0CALL>APCMD0,TCPIP*:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", result.Packet);
    }

    [Fact]
    public void FormatterUsesLocalStationProfileForMissingPositionAndCallsign()
    {
        var formatter = new AprsWeatherFormatter();
        var profile = LocalStationProfile.CreateDefault(TestTime) with
        {
            Callsign = "KD8ABC",
            Ssid = 7,
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333
        };

        var result = formatter.FormatPreview(
            CreateObservation(callsign: null, latitude: null, longitude: null),
            profile,
            AprsWeatherFormatterOptions.Default with { Path = ["TCPIP*"] });

        Assert.True(result.IsSuccess);
        Assert.StartsWith("KD8ABC-7>APCMD0,TCPIP*:!3903.50N/08430.50W_", result.Packet, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRequiredWeatherFieldsReturnValidationErrors()
    {
        var formatter = new AprsWeatherFormatter();

        var result = formatter.FormatPreview(CreateObservation(windDirection: null, humidity: null));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, error => error.Contains("Wind direction", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ValidationErrors, error => error.Contains("Humidity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LineBreaksInCommentsAreRejected()
    {
        var formatter = new AprsWeatherFormatter();

        var result = formatter.FormatPreview(
            CreateObservation(),
            options: AprsWeatherFormatterOptions.Default with { Comment = "bad\ncomment" });

        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, error => error.Contains("line breaks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WeatherReportWithoutPositionCanBeFormatted()
    {
        var formatter = new AprsWeatherFormatter();

        var result = formatter.FormatPreview(
            CreateObservation(latitude: null, longitude: null),
            options: AprsWeatherFormatterOptions.Default with { UsePosition = false });

        Assert.True(result.IsSuccess);
        Assert.StartsWith("N0CALL>APCMD0:_120000180/005g010t072r000p000P000h50b10132", result.Packet, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidCallsignIsRejected()
    {
        var formatter = new AprsWeatherFormatter();

        var result = formatter.FormatPreview(CreateObservation(callsign: "TOOLONG7"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, error => error.Contains("Source station identifier", StringComparison.OrdinalIgnoreCase));
    }

    private static CommonWeatherObservation CreateObservation(
        string? callsign = "N0CALL",
        double? latitude = 39.058333,
        double? longitude = -84.508333,
        int? windDirection = 180,
        double? windSpeedMph = 5,
        double? windGustMph = 10,
        double? temperatureFahrenheit = 72,
        double? rainLastHourInches = 0,
        double? rainLast24HoursInches = 0,
        double? rainSinceMidnightInches = 0,
        int? humidity = 50,
        double? pressureMillibars = 1013.2,
        WeatherDataState staleDataState = WeatherDataState.Current)
    {
        return new CommonWeatherObservation(
            SourceName: "Tempest local UDP",
            SourceType: WeatherObservationSourceType.WeatherFlowTempest,
            StationDeviceId: "hub-123",
            callsign,
            TestTime,
            latitude,
            longitude,
            windDirection,
            windSpeedMph,
            windGustMph,
            temperatureFahrenheit,
            rainLastHourInches,
            rainLast24HoursInches,
            rainSinceMidnightInches,
            humidity,
            pressureMillibars,
            LuminosityWattsPerSquareMeter: null,
            UvIndex: null,
            SnowInches: null,
            LightningCount: null,
            LightningDistanceMiles: null,
            Diagnostics: new Dictionary<string, string> { ["battery"] = "ok" },
            RawSourcePayload: "{\"obs\":true}",
            staleDataState,
            ValidationErrors: [],
            ValidationWarnings: []);
    }
}
