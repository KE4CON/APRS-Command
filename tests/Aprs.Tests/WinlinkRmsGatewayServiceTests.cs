using Aprs.Desktop.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Verifies the Winlink RMS gateway parser against the documented CMS Web Services response shape
/// (GatewayProximityResponse / GatewayPositionsRecord from github.com/ARSFI/winlink-webservices),
/// so the dormant feature is correct-by-construction before a live API key is available.
/// </summary>
public sealed class WinlinkRmsGatewayServiceTests
{
    private const string SampleResponse = """
    {
      "GatewayList": [
        {
          "Callsign": "W1AW",
          "Gridsquare": "FN31pr",
          "Frequency": 145030000,
          "Mode": 1,
          "Baud": "1200",
          "ServiceCode": "PUBLIC",
          "Distance": 5,
          "Heading": 270
        },
        {
          "Callsign": "K1XYZ",
          "Gridsquare": "FN42",
          "Frequency": 7103000,
          "Mode": 6,
          "Baud": "",
          "ServiceCode": "PUBLIC",
          "Distance": 40,
          "Heading": 90
        }
      ],
      "ResponseStatus": {}
    }
    """;

    [Fact]
    public void ParseResponse_ReadsGatewayListWithCorrectFields()
    {
        var gateways = WinlinkRmsGatewayService.ParseResponse(SampleResponse);

        Assert.Equal(2, gateways.Count);

        var first = gateways[0];
        Assert.Equal("W1AW", first.Callsign);
        Assert.Equal("145.030 MHz", first.Frequency);
        Assert.Equal("Packet (1200)", first.Mode);
        // Grid square FN31pr converts to a location in Connecticut (~41.7N, 72.7W).
        Assert.InRange(first.Latitude, 41.0, 42.5);
        Assert.InRange(first.Longitude, -73.5, -72.0);

        var second = gateways[1];
        Assert.Equal("K1XYZ", second.Callsign);
        Assert.Equal("7.103 MHz", second.Frequency);
        Assert.Equal("ARDOP", second.Mode);
    }

    [Fact]
    public void ParseResponse_ReturnsEmptyForBareArray()
    {
        // The real API wraps records in a GatewayList object; a bare array (the old wrong shape)
        // must yield nothing rather than silently mis-parsing.
        Assert.Empty(WinlinkRmsGatewayService.ParseResponse("[{\"Callsign\":\"W1AW\"}]"));
    }

    [Fact]
    public void ParseResponse_ReturnsEmptyForMissingGatewayList()
    {
        Assert.Empty(WinlinkRmsGatewayService.ParseResponse("""{"ResponseStatus":{}}"""));
    }

    [Fact]
    public void ParseResponse_SkipsRecordsWithoutCallsign()
    {
        var gateways = WinlinkRmsGatewayService.ParseResponse(
            """{"GatewayList":[{"Gridsquare":"FN31","Frequency":145030000}]}""");

        Assert.Empty(gateways);
    }
}
