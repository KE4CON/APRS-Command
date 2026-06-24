using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class GpsStatusViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidGpsFix_DisplaysAsValid()
    {
        var viewModel = new GpsStatusViewModel(CreatePosition(lastUpdateUtc: Now), Now);

        Assert.Equal(GpsFixDisplayState.Valid, viewModel.FixState);
        Assert.Equal("Valid fix", viewModel.FixStatus);
        Assert.Equal("gpsd", viewModel.Source);
        Assert.Equal("gpsd:/dev/ttyUSB0", viewModel.SourceName);
    }

    [Fact]
    public void InvalidGpsFix_DisplaysAsNoFix()
    {
        var viewModel = new GpsStatusViewModel(CreatePosition(fixValid: false, latitude: null, longitude: null), Now);

        Assert.Equal(GpsFixDisplayState.NoFix, viewModel.FixState);
        Assert.Equal("No fix", viewModel.FixStatus);
        Assert.Equal("Unknown", viewModel.Latitude);
        Assert.Equal("Unknown", viewModel.Longitude);
    }

    [Fact]
    public void StaleGpsFix_DisplaysAsStale()
    {
        var viewModel = new GpsStatusViewModel(CreatePosition(lastUpdateUtc: Now.AddSeconds(-11)), Now);

        Assert.Equal(GpsFixDisplayState.Stale, viewModel.FixState);
        Assert.Equal("Stale fix", viewModel.FixStatus);
        Assert.Equal("11 sec ago", viewModel.Age);
    }

    [Fact]
    public void MissingOptionalFields_DisplaySafely()
    {
        var viewModel = new GpsStatusViewModel(CreatePosition(
            altitudeMeters: null,
            speedKnots: null,
            courseDegrees: null,
            satelliteCount: null,
            usedSatelliteCount: null,
            hdop: null), Now);

        Assert.Equal("Unknown", viewModel.Altitude);
        Assert.Equal("Unknown", viewModel.Speed);
        Assert.Equal("Unknown", viewModel.Course);
        Assert.Equal("Unknown", viewModel.SatelliteCount);
        Assert.Equal("Unknown", viewModel.UsedSatelliteCount);
        Assert.Equal("Unknown", viewModel.Hdop);
    }

    [Fact]
    public void LatitudeLongitude_FormatWithHemispheres()
    {
        var viewModel = new GpsStatusViewModel(CreatePosition(latitude: -39.058333, longitude: 84.508333), Now);

        Assert.Equal("39.05833 S", viewModel.Latitude);
        Assert.Equal("84.50833 E", viewModel.Longitude);
    }

    [Fact]
    public void SpeedCourse_FormatCorrectly()
    {
        var viewModel = new GpsStatusViewModel(CreatePosition(speedKnots: 12.5, courseDegrees: 45), Now);

        Assert.Equal("12.5 kt", viewModel.Speed);
        Assert.Equal("045 deg", viewModel.Course);
    }

    [Fact]
    public void UnknownPosition_DisplaysUnknownStatus()
    {
        var viewModel = new GpsStatusViewModel(null, Now);

        Assert.Equal(GpsFixDisplayState.Unknown, viewModel.FixState);
        Assert.Equal("Unknown", viewModel.FixStatus);
        Assert.Equal("Unknown", viewModel.Source);
        Assert.Equal("Unknown", viewModel.Age);
    }

    [Fact]
    public void DisconnectedFlag_DisplaysDisconnectedStatus()
    {
        var viewModel = new GpsStatusViewModel(CreatePosition(), Now, disconnected: true);

        Assert.Equal(GpsFixDisplayState.Disconnected, viewModel.FixState);
        Assert.Equal("Disconnected", viewModel.FixStatus);
    }

    [Fact]
    public void FromGpsService_ReflectsUpdatedGpsPosition()
    {
        var service = new GpsService();
        var parser = new GpsdJsonParser();
        service.AcceptGpsdReport(parser.Parse(
            "{\"class\":\"TPV\",\"mode\":3,\"lat\":39.058333,\"lon\":-84.508333,\"speed\":6.430,\"track\":45.0}",
            receivedAtUtc: Now),
            receivedAtUtc: Now);

        var viewModel = GpsStatusViewModel.FromGpsService(service, Now);

        Assert.Equal(GpsFixDisplayState.Valid, viewModel.FixState);
        Assert.Equal("39.05833 N", viewModel.Latitude);
        Assert.Equal("84.50833 W", viewModel.Longitude);
        Assert.Equal("12.5 kt", viewModel.Speed);
        Assert.Equal("045 deg", viewModel.Course);
    }

    private static GpsPosition CreatePosition(
        double? latitude = 39.058333,
        double? longitude = -84.508333,
        double? altitudeMeters = 250,
        double? speedKnots = 12.5,
        double? courseDegrees = 45,
        bool fixValid = true,
        int? satelliteCount = 8,
        int? usedSatelliteCount = 6,
        double? hdop = 0.9,
        DateTimeOffset? lastUpdateUtc = null,
        string sourceName = "gpsd:/dev/ttyUSB0")
    {
        return new GpsPosition(
            latitude,
            longitude,
            altitudeMeters,
            speedKnots,
            courseDegrees,
            TimestampUtc: lastUpdateUtc ?? Now,
            fixValid,
            FixQuality: 3,
            satelliteCount,
            hdop,
            sourceName,
            RawNmeaSentence: "gps sample",
            LastUpdateUtc: lastUpdateUtc ?? Now)
        {
            UsedSatelliteCount = usedSatelliteCount
        };
    }
}
