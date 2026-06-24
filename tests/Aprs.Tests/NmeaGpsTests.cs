using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class NmeaGpsTests
{
    private static readonly DateTimeOffset ReceivedAtUtc = new(2026, 6, 10, 9, 27, 50, TimeSpan.Zero);
    private const string GgaSentence = "$GPGGA,092750.000,3903.5000,N,08430.5000,W,1,08,1.0,250.0,M,-34.0,M,,*76";
    private const string RmcSentence = "$GPRMC,092751.000,A,3903.5000,N,08430.5000,W,12.5,045.0,100626,,,A*68";
    private const string InvalidRmcSentence = "$GPRMC,092752.000,V,3903.5000,N,08430.5000,W,0.0,000.0,100626,,,N*75";
    private const string NoFixGgaSentence = "$GPGGA,,,,,,0,00,99.9,,,,,,*48";

    [Fact]
    public void ParseGga_ParsesLatitudeLongitude()
    {
        var parser = new NmeaParser();

        var result = parser.Parse(GgaSentence, receivedAtUtc: ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.Equal("GGA", result.SentenceType);
        Assert.NotNull(result.Position);
        Assert.Equal(39.058333, result.Position.Latitude.GetValueOrDefault(), precision: 6);
        Assert.Equal(-84.508333, result.Position.Longitude.GetValueOrDefault(), precision: 6);
        Assert.True(result.Position.FixValid);
    }

    [Fact]
    public void ParseGga_ParsesAltitudeSatelliteCountAndHdop()
    {
        var parser = new NmeaParser();

        var result = parser.Parse(GgaSentence, receivedAtUtc: ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.Equal(250.0, result.Position?.AltitudeMeters);
        Assert.Equal(8, result.Position?.SatelliteCount);
        Assert.Equal(1.0, result.Position?.Hdop);
        Assert.Equal(1, result.Position?.FixQuality);
        Assert.Equal(new DateTimeOffset(2026, 6, 10, 9, 27, 50, TimeSpan.Zero), result.Position?.TimestampUtc);
    }

    [Fact]
    public void ParseRmc_ParsesLatitudeLongitude()
    {
        var parser = new NmeaParser();

        var result = parser.Parse(RmcSentence, receivedAtUtc: ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.Equal("RMC", result.SentenceType);
        Assert.Equal(39.058333, result.Position!.Latitude.GetValueOrDefault(), precision: 6);
        Assert.Equal(-84.508333, result.Position.Longitude.GetValueOrDefault(), precision: 6);
        Assert.True(result.Position?.FixValid);
    }

    [Fact]
    public void ParseRmc_ParsesSpeedCourseAndTimestamp()
    {
        var parser = new NmeaParser();

        var result = parser.Parse(RmcSentence, receivedAtUtc: ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.Equal(12.5, result.Position?.SpeedKnots);
        Assert.Equal(45.0, result.Position?.CourseDegrees);
        Assert.Equal(new DateTimeOffset(2026, 6, 10, 9, 27, 51, TimeSpan.Zero), result.Position?.TimestampUtc);
    }

    [Fact]
    public void ParseRmc_InvalidStatusMarksFixInvalid()
    {
        var parser = new NmeaParser();

        var result = parser.Parse(InvalidRmcSentence, receivedAtUtc: ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.False(result.Position?.FixValid);
        Assert.Equal(0.0, result.Position?.SpeedKnots);
        Assert.Equal(0.0, result.Position?.CourseDegrees);
    }

    [Fact]
    public void ParseMalformedSentence_DoesNotCrash()
    {
        var parser = new NmeaParser();

        var result = parser.Parse("not a sentence", receivedAtUtc: ReceivedAtUtc);

        Assert.False(result.IsParsed);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ParseChecksumMismatch_IsHandledSafely()
    {
        var parser = new NmeaParser();

        var result = parser.Parse(GgaSentence, receivedAtUtc: ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.False(result.ChecksumValid);
        Assert.Contains(result.Warnings, warning => warning.Contains("checksum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseNoFixGga_ReturnsInvalidFixWithoutCrashing()
    {
        var parser = new NmeaParser();

        var result = parser.Parse(NoFixGgaSentence, receivedAtUtc: ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.False(result.Position?.FixValid);
        Assert.Equal(0, result.Position?.FixQuality);
        Assert.Equal(0, result.Position?.SatelliteCount);
        Assert.Equal(99.9, result.Position?.Hdop);
    }

    [Fact]
    public void GpsService_UpdatesCurrentPositionFromNmea()
    {
        var service = new GpsService();

        var result = service.AcceptSentence(GgaSentence, "Test GPS", ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.True(service.HasValidFix);
        Assert.Equal(39.058333, service.CurrentPosition!.Latitude.GetValueOrDefault(), precision: 6);
        Assert.Equal(-84.508333, service.CurrentPosition.Longitude.GetValueOrDefault(), precision: 6);
        Assert.Equal("Test GPS", service.CurrentPosition?.SourceName);
    }

    [Fact]
    public void GpsService_PreservesCombinedDataFromGgaAndRmc()
    {
        var service = new GpsService();

        service.AcceptSentence(GgaSentence, "Test GPS", ReceivedAtUtc);
        service.AcceptSentence(RmcSentence, "Test GPS", ReceivedAtUtc.AddSeconds(1));

        var position = service.CurrentPosition;
        Assert.NotNull(position);
        Assert.True(service.HasValidFix);
        Assert.Equal(250.0, position.AltitudeMeters);
        Assert.Equal(8, position.SatelliteCount);
        Assert.Equal(1.0, position.Hdop);
        Assert.Equal(12.5, position.SpeedKnots);
        Assert.Equal(45.0, position.CourseDegrees);
        Assert.Equal(new DateTimeOffset(2026, 6, 10, 9, 27, 51, TimeSpan.Zero), position.TimestampUtc);
    }

    [Fact]
    public void GpsService_MalformedSentenceDoesNotReplaceCurrentPosition()
    {
        var service = new GpsService();
        service.AcceptSentence(GgaSentence, "Test GPS", ReceivedAtUtc);
        var before = service.CurrentPosition;

        var result = service.AcceptSentence("$BAD", "Test GPS", ReceivedAtUtc.AddSeconds(2));

        Assert.False(result.IsParsed);
        Assert.Same(before, service.CurrentPosition);
    }

    [Fact]
    public void GpsService_ResetClearsGpsState()
    {
        var service = new GpsService();
        service.AcceptSentence(GgaSentence, "Test GPS", ReceivedAtUtc);

        service.Reset();

        Assert.Null(service.CurrentPosition);
        Assert.False(service.HasValidFix);
    }

    [Fact]
    public void GpsPosition_CanCreateMobilePositionInputForFutureBeaconing()
    {
        var parser = new NmeaParser();
        var result = parser.Parse(RmcSentence, receivedAtUtc: ReceivedAtUtc);

        var mobile = result.Position!.ToMobilePositionInput();

        Assert.True(mobile.FixValid);
        Assert.Equal(MobilePositionSource.Gps, mobile.Source);
        Assert.Equal(39.058333, mobile.Latitude, precision: 6);
        Assert.Equal(-84.508333, mobile.Longitude, precision: 6);
        Assert.Equal(12.5, mobile.SpeedKnots);
        Assert.Equal(45.0, mobile.CourseDegrees);
    }
}
