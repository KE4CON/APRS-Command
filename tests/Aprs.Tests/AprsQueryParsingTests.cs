using Aprs.Core;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// APRS query packets (DTI '?', spec §15) are now decomposed into a structured query type + keyword +
/// optional target, rather than only captured as raw text.
/// </summary>
public sealed class AprsQueryParsingTests
{
    private static readonly DateTimeOffset TestTime = new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

    private static QueryAprsPacket ParseQuery(string information)
    {
        var p = new AprsParser().Parse("N0CALL>APRS:" + information, TestTime);
        return Assert.IsType<QueryAprsPacket>(p);
    }

    [Theory]
    [InlineData("?APRS?", AprsQueryType.General, "APRS")]
    [InlineData("?APRSD", AprsQueryType.General, "APRSD")]
    [InlineData("?WX?", AprsQueryType.Weather, "WX")]
    [InlineData("?IGATE?", AprsQueryType.IGate, "IGATE")]
    [InlineData("?PING?", AprsQueryType.Ping, "PING")]
    [InlineData("?FOO?", AprsQueryType.Unknown, "FOO")]
    public void Query_IsClassifiedByKeyword(string info, AprsQueryType expectedType, string expectedKeyword)
    {
        var q = ParseQuery(info);

        Assert.Equal(expectedType, q.QueryType);
        Assert.Equal(expectedKeyword, q.QueryKeyword);
        Assert.Equal(info, q.QueryText); // raw text still preserved
    }

    [Fact]
    public void DirectedQuery_CapturesTarget()
    {
        var q = ParseQuery("?APRS?KE4CON-9");

        Assert.Equal(AprsQueryType.General, q.QueryType);
        Assert.Equal("APRS", q.QueryKeyword);
        Assert.Equal("KE4CON-9", q.QueryTarget);
    }

    [Fact]
    public void GeneralQuery_HasNoTarget()
    {
        var q = ParseQuery("?APRS?");

        Assert.Null(q.QueryTarget);
    }
}
