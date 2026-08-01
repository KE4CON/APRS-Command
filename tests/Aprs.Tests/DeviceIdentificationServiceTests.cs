using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class DeviceIdentificationServiceTests
{
    // Synthetic dataset exercising the matching rules independent of the (updatable) bundled snapshot.
    private const string SampleJson = """
    {
      "classes": { "software": {"shown":"Desktop software"}, "tracker": {"shown":"Tracker"} },
      "tocalls": {
        "APZ???": {"class":"software","vendor":"Experimental","model":"Generic Z"},
        "APZABC": {"class":"tracker","vendor":"Acme","model":"Specific Tracker","os":"embedded"},
        "APX???": {"class":"software","vendor":"X Co","model":"XSoft"}
      }
    }
    """;

    private static DeviceIdentificationService Sample() => new(SampleJson);

    [Fact]
    public void MostSpecificPattern_Wins_OverWildcard()
    {
        // "APZABC" matches both the exact "APZABC" and the wildcard "APZ???"; the exact one wins.
        var id = Sample().Identify("APZABC");

        Assert.NotNull(id);
        Assert.Equal("Specific Tracker", id!.Model);
        Assert.Equal("Tracker", id.DeviceClassLabel); // class code resolved to its label
        Assert.Equal("embedded", id.Os);
    }

    [Fact]
    public void WildcardPattern_MatchesVariableSuffix()
    {
        var id = Sample().Identify("APZ123");

        Assert.NotNull(id);
        Assert.Equal("Generic Z", id!.Model);
        Assert.Equal("Desktop software", id.DeviceClassLabel);
    }

    [Theory]
    [InlineData("APQXYZ")] // no pattern matches
    [InlineData("")]
    [InlineData(null)]
    public void UnknownOrEmpty_ReturnsNull(string? tocall)
    {
        Assert.Null(Sample().Identify(tocall));
    }

    [Fact]
    public void Display_CombinesModelAndClass()
    {
        Assert.Equal("XSoft (Desktop software)", Sample().Identify("APX999")!.Display);
    }

    // ── Bundled snapshot (real data) ────────────────────────────────────────────

    [Fact]
    public void BundledDatabase_Loads_AndIdentifiesAprsCommand()
    {
        var service = new DeviceIdentificationService(); // loads the embedded snapshot

        Assert.True(service.PatternCount > 300, "expected the full tocalls dataset to load");

        // Our own registered allocation (APCMD?) resolves — the loop-closing test.
        var us = service.Identify("APCMD0");
        Assert.NotNull(us);
        Assert.Equal("APRS Command", us!.Model);
        Assert.Contains("KE4CON", us.Vendor);
    }
}
