using System;
using Aprs.Desktop.Services;
using AlertSeverity = Aprs.Desktop.Services.AlertSeverity;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Tests for NwsAlertRecord — the parsed alert model including severity mapping,
/// color coding, expiry logic, and display formatting. NwsAlertService itself
/// makes HTTP calls to api.weather.gov so its HTTP behaviour is integration-level;
/// the record logic is fully unit-testable.
/// </summary>
public sealed class NwsAlertRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    private static NwsAlertRecord Make(
        string severity = "Severe",
        DateTimeOffset? expires = null,
        string id = "urn:oid:2.49.0.1.840.0.TEST") =>
        new(id, "Tornado Warning", "Tornado Warning in effect",
            "A tornado was spotted near town.", severity, "Immediate", "Observed",
            "Hamilton County, OH", Now, expires, "NWS Cincinnati OH");

    // ── SeverityLevel ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("Extreme",  AlertSeverity.Extreme)]
    [InlineData("Severe",   AlertSeverity.Severe)]
    [InlineData("Moderate", AlertSeverity.Moderate)]
    [InlineData("Minor",    AlertSeverity.Minor)]
    [InlineData("Unknown",  AlertSeverity.Unknown)]
    [InlineData("",         AlertSeverity.Unknown)]
    [InlineData("extreme",  AlertSeverity.Unknown)]  // case-sensitive
    public void SeverityLevel_MapsCorrectly(string severity, AlertSeverity expected)
    {
        var record = Make(severity: severity);
        Assert.Equal(expected, record.SeverityLevel);
    }

    // ── SeverityColor (foreground) ────────────────────────────────────────

    [Theory]
    [InlineData("Extreme",  "#7C0000")]
    [InlineData("Severe",   "#CC0000")]
    [InlineData("Moderate", "#FF6600")]
    [InlineData("Minor",    "#FFCC00")]
    [InlineData("Unknown",  "#888888")]
    public void SeverityColor_IsCorrectForEachLevel(string severity, string expectedColor)
    {
        var record = Make(severity: severity);
        Assert.Equal(expectedColor, record.SeverityColor);
    }

    // ── SeverityBg (background) ───────────────────────────────────────────

    [Theory]
    [InlineData("Extreme",  "#FFE0E0")]
    [InlineData("Severe",   "#FFF0F0")]
    [InlineData("Moderate", "#FFF5E0")]
    [InlineData("Minor",    "#FFFDE0")]
    [InlineData("Unknown",  "#F8F8F8")]
    public void SeverityBg_IsCorrectForEachLevel(string severity, string expectedBg)
    {
        var record = Make(severity: severity);
        Assert.Equal(expectedBg, record.SeverityBg);
    }

    // ── IsExpired ─────────────────────────────────────────────────────────

    [Fact]
    public void IsExpired_False_WhenExpiresIsNull()
    {
        var record = Make(expires: null);
        Assert.False(record.IsExpired);
    }

    [Fact]
    public void IsExpired_False_WhenExpiresInFuture()
    {
        var record = Make(expires: DateTimeOffset.UtcNow.AddHours(2));
        Assert.False(record.IsExpired);
    }

    [Fact]
    public void IsExpired_True_WhenExpiresInPast()
    {
        var record = Make(expires: DateTimeOffset.UtcNow.AddHours(-1));
        Assert.True(record.IsExpired);
    }

    // ── ExpiresText ───────────────────────────────────────────────────────

    [Fact]
    public void ExpiresText_ReportsNoExpiration_WhenNull()
    {
        var record = Make(expires: null);
        Assert.Equal("No expiration", record.ExpiresText);
    }

    [Fact]
    public void ExpiresText_ContainsFormattedTime_WhenSet()
    {
        var expires = new DateTimeOffset(2026, 7, 2, 18, 0, 0, TimeSpan.Zero);
        var record = Make(expires: expires);
        Assert.Contains("Until", record.ExpiresText);
        // The exact format is locale-dependent; just check it's not empty/null
        Assert.False(string.IsNullOrWhiteSpace(record.ExpiresText));
    }

    // ── Record fields ─────────────────────────────────────────────────────

    [Fact]
    public void Record_PreservesAllFields()
    {
        var effective = Now;
        var expires   = Now.AddHours(3);
        var record = new NwsAlertRecord(
            "TEST-ID", "Flood Watch", "Flood Watch in effect",
            "Heavy rain expected.", "Moderate", "Future", "Possible",
            "Butler County, OH", effective, expires, "NWS Cincinnati OH");

        Assert.Equal("TEST-ID",             record.Id);
        Assert.Equal("Flood Watch",          record.Event);
        Assert.Equal("Flood Watch in effect", record.Headline);
        Assert.Equal("Heavy rain expected.", record.Description);
        Assert.Equal("Moderate",            record.Severity);
        Assert.Equal("Future",              record.Urgency);
        Assert.Equal("Possible",            record.Certainty);
        Assert.Equal("Butler County, OH",   record.AreaDescription);
        Assert.Equal(effective,             record.Effective);
        Assert.Equal(expires,               record.Expires);
        Assert.Equal("NWS Cincinnati OH",   record.Sender);
    }

    // ── AlertSeverity ordering ────────────────────────────────────────────

    [Fact]
    public void AlertSeverity_IsOrderedLeastToMost()
    {
        // Enum values should be ordered ascending by danger
        Assert.True(AlertSeverity.Unknown  < AlertSeverity.Minor);
        Assert.True(AlertSeverity.Minor    < AlertSeverity.Moderate);
        Assert.True(AlertSeverity.Moderate < AlertSeverity.Severe);
        Assert.True(AlertSeverity.Severe   < AlertSeverity.Extreme);
    }
}
