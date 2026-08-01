using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class DeviceIdentificationServiceTests
{
    // Synthetic dataset exercising the matching rules independent of the (updatable) bundled snapshot.
    private const string SampleJson = """
    {
      "classes": { "software": {"shown":"Desktop software"}, "tracker": {"shown":"Tracker"},
                   "rig": {"shown":"Mobile radio"}, "ht": {"shown":"Handheld"} },
      "tocalls": {
        "APZ???": {"class":"software","vendor":"Experimental","model":"Generic Z"},
        "APZABC": {"class":"tracker","vendor":"Acme","model":"Specific Tracker","os":"embedded"},
        "APX???": {"class":"software","vendor":"X Co","model":"XSoft"}
      },
      "mice": {
        "_\"": {"class":"rig","vendor":"Yaesu","model":"FTM-350"},
        "(8":  {"class":"ht","vendor":"Anytone","model":"D878UV"}
      },
      "micelegacy": {
        ">":  {"prefix":">","class":"ht","vendor":"Kenwood","model":"TH-D7A"},
        ">^": {"prefix":">","suffix":"^","class":"ht","vendor":"Kenwood","model":"TH-D74"},
        "]":  {"prefix":"]","class":"rig","vendor":"Kenwood","model":"TM-D700"},
        "]=": {"prefix":"]","suffix":"=","class":"rig","vendor":"Kenwood","model":"TM-D710"}
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

    // ── MIC-E (comment-based) identification ────────────────────────────────────

    [Theory]
    [InlineData("]", "TM-D700")]     // bare prefix, no operator text
    [InlineData("]=", "TM-D710")]    // prefix + suffix
    [InlineData(">", "TH-D7A")]      // prefix-only HT
    [InlineData(">^", "TH-D74")]     // prefix + suffix HT
    public void LegacyMicE_ResolvesFromPrefixAndSuffix(string comment, string expectedModel)
    {
        Assert.Equal(expectedModel, Sample().IdentifyMicE(comment)!.Model);
    }

    [Theory]
    [InlineData("]hello", "TM-D700")]   // prefix with trailing operator text
    [InlineData("]op text=", "TM-D710")] // prefix + operator text + suffix
    public void LegacyMicE_ToleratesOperatorText(string comment, string expectedModel)
    {
        Assert.Equal(expectedModel, Sample().IdentifyMicE(comment)!.Model);
    }

    [Theory]
    [InlineData("[En Route] ]", "TM-D700")]   // decoder's status prefix is stripped first
    [InlineData("[Emergency] ]=", "TM-D710")]
    public void LegacyMicE_StripsDecoderStatusPrefix(string comment, string expectedModel)
    {
        Assert.Equal(expectedModel, Sample().IdentifyMicE(comment)!.Model);
    }

    [Theory]
    [InlineData("_\"", "FTM-350")]           // modern two-char code alone
    [InlineData("hello world_\"", "FTM-350")] // code at the end of operator text
    [InlineData("anytone(8", "D878UV")]
    public void ModernMicE_ResolvesFromTrailingCode(string comment, string expectedModel)
    {
        Assert.Equal(expectedModel, Sample().IdentifyMicE(comment)!.Model);
    }

    [Theory]
    [InlineData("just a plain comment")] // no device code
    [InlineData("")]
    [InlineData(null)]
    public void MicE_UnknownOrEmpty_ReturnsNull(string? comment)
    {
        Assert.Null(Sample().IdentifyMicE(comment));
    }

    [Fact]
    public void CombinedIdentify_PrefersTocall_ThenFallsBackToMicE()
    {
        var s = Sample();

        // A real tocall wins even when a comment is present.
        Assert.Equal("XSoft", s.Identify("APX999", "]")!.Model);

        // A MIC-E position destination isn't a tocall, so the comment resolves the radio.
        Assert.Equal("TM-D710", s.Identify("T7SYPU", "]=")!.Model);

        // Neither matches.
        Assert.Null(s.Identify("T7SYPU", "plain comment"));
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

    [Fact]
    public void BundledDatabase_IdentifiesMicERadios()
    {
        var service = new DeviceIdentificationService(); // loads the embedded snapshot

        Assert.Equal("TM-D700", service.IdentifyMicE("]")!.Model);   // legacy Kenwood mobile
        Assert.Equal("TM-D710", service.IdentifyMicE("]=")!.Model);  // legacy prefix + suffix
        Assert.Equal("FTM-350", service.IdentifyMicE("comment_\"")!.Model); // modern trailing code
    }
}
