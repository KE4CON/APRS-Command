using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsBeaconFormatterTests
{
    [Fact]
    public void FormatFixedPositionBeacon_FormatsExpectedPacket()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatFixedPositionBeacon(CreateInput(
            source: "N0CALL",
            latitude: 39.058333,
            longitude: -84.508333,
            comment: "Test beacon"));

        Assert.True(result.IsSuccess);
        Assert.Equal("N0CALL>APRS,WIDE1-1:!3903.50N/08430.50W-Test beacon", result.Packet);
    }

    [Fact]
    public void FormatFixedPositionBeacon_WithNoPathFormatsAprsIsStylePacket()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatFixedPositionBeacon(CreateInput(
            source: "N0CALL",
            path: [],
            latitude: 39.058333,
            longitude: -84.508333,
            comment: "APRS-IS beacon"));

        Assert.True(result.IsSuccess);
        Assert.Equal("N0CALL>APRS:!3903.50N/08430.50W-APRS-IS beacon", result.Packet);
    }

    [Theory]
    [InlineData(39.058333, "3903.50N")]
    [InlineData(-39.058333, "3903.50S")]
    public void FormatLatitude_FormatsHemispheres(double latitude, string expected)
    {
        Assert.Equal(expected, AprsCoordinateFormatter.FormatLatitude(latitude));
    }

    [Theory]
    [InlineData(-84.508333, "08430.50W")]
    [InlineData(84.508333, "08430.50E")]
    public void FormatLongitude_FormatsHemispheres(double longitude, string expected)
    {
        Assert.Equal(expected, AprsCoordinateFormatter.FormatLongitude(longitude));
    }

    [Fact]
    public void FormatCoordinates_FormatsW1AwExample()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatFixedPositionBeacon(CreateInput(
            source: "W1AW-9",
            path: ["WIDE1-1", "WIDE2-1"],
            latitude: 41.390833,
            longitude: -72.576000,
            symbolCode: '>',
            comment: "Mobile test"));

        Assert.True(result.IsSuccess);
        Assert.Equal("W1AW-9>APRS,WIDE1-1,WIDE2-1:!4123.45N/07234.56W>Mobile test", result.Packet);
    }

    [Fact]
    public void FormatStatusBeacon_FormatsExpectedPacket()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatStatusBeacon("N0CALL", "APRS", [], "Net control station online");

        Assert.True(result.IsSuccess);
        Assert.Equal("N0CALL>APRS:>Net control station online", result.Packet);
    }

    [Fact]
    public void FormatFixedPositionBeacon_InvalidCallsignFailsValidation()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatFixedPositionBeacon(CreateInput(source: "TOOLONG7"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Packet);
        Assert.Contains(result.ValidationErrors, error => error.Contains("Source station identifier", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormatFixedPositionBeacon_InvalidCoordinatesFailValidation()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatFixedPositionBeacon(CreateInput(latitude: 91, longitude: -181));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, error => error.Contains("Latitude", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ValidationErrors, error => error.Contains("Longitude", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormatFixedPositionBeacon_MissingSymbolFailsValidation()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatFixedPositionBeacon(CreateInput(symbolTable: null, symbolCode: null));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, error => error.Contains("Symbol table", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.ValidationErrors, error => error.Contains("Symbol code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormatFixedPositionBeacon_CommentsWithLineBreaksAreRejected()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatFixedPositionBeacon(CreateInput(comment: "Bad\ncomment"));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, error => error.Contains("line breaks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormatFixedPositionBeacon_IncludesAltitudeCourseSpeedAndPhgWhenPresent()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatFixedPositionBeacon(CreateInput(
            phgData: "PHG5130",
            altitudeFeet: 789,
            courseDegrees: 123,
            speedKnots: 45,
            comment: " Moving test"));

        Assert.True(result.IsSuccess);
        Assert.Equal("N0CALL>APRS,WIDE1-1:!3903.50N/08430.50W-PHG5130123/045/A=000789Moving test", result.Packet);
    }

    [Fact]
    public void FormatFixedPositionBeacon_RejectsRfRequiredPathWhenMissing()
    {
        var formatter = new AprsBeaconFormatter();

        var result = formatter.FormatFixedPositionBeacon(CreateInput(path: [], rfPathRequired: true));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.ValidationErrors, error => error.Contains("RF path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CreateInputFromProfile_UsesLocalStationProfileFields()
    {
        var formatter = new AprsBeaconFormatter();
        var profile = LocalStationProfile.CreateDefault(DateTimeOffset.UtcNow) with
        {
            Callsign = "KD8ABC",
            Ssid = 7,
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333,
            SymbolTableIdentifier = '/',
            SymbolCode = '-',
            StationComment = "Profile beacon",
            BeaconPath = "WIDE1-1,WIDE2-1"
        };

        var input = formatter.CreateInputFromProfile(profile);
        var result = formatter.FormatFixedPositionBeacon(input);

        Assert.True(result.IsSuccess);
        Assert.Equal("KD8ABC-7>APRS,WIDE1-1,WIDE2-1:!3903.50N/08430.50W-Profile beacon", result.Packet);
    }

    private static AprsBeaconInput CreateInput(
        string source = "N0CALL",
        string destination = "APRS",
        IReadOnlyList<string>? path = null,
        double? latitude = 39.058333,
        double? longitude = -84.508333,
        char? symbolTable = '/',
        char? symbolCode = '-',
        string? comment = "-Test beacon",
        int? altitudeFeet = null,
        int? courseDegrees = null,
        int? speedKnots = null,
        string? phgData = null,
        bool rfPathRequired = false)
    {
        return new AprsBeaconInput(
            source,
            destination,
            path ?? ["WIDE1-1"],
            latitude,
            longitude,
            symbolTable,
            symbolCode,
            comment,
            altitudeFeet,
            courseDegrees,
            speedKnots,
            phgData,
            UseTimestamp: false,
            UseCompressedPosition: false,
            rfPathRequired);
    }
}
