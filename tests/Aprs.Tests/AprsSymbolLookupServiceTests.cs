using System.Linq;
using Aprs.Mapping;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Locks the APRS symbol tables to the authoritative aprs.fi symbol index (hessu/aprs-symbol-index,
/// CC BY-SA 4.0). The complete primary ('/') and alternate ('\') tables must stay present so the
/// object symbol picker and station tooltips are spec-complete. Descriptions are verbatim from the
/// index; the short letter designation is derived from the description.
/// </summary>
public sealed class AprsSymbolLookupServiceTests
{
    [Theory]
    // table, code, description, iconKey, fallback letter, category
    [InlineData('/', '-', "House", "home", "H", AprsSymbolCategory.Home)]
    [InlineData('/', '>', "Car", "car", "C", AprsSymbolCategory.Mobile)]
    [InlineData('/', '_', "Weather station", "weather", "WS", AprsSymbolCategory.Weather)]
    [InlineData('/', '#', "Digipeater", "digipeater", "D", AprsSymbolCategory.Digipeater)]
    [InlineData('/', 'r', "Repeater tower", "repeater", "RT", AprsSymbolCategory.Repeater)]
    [InlineData('/', 'C', "Canoe", "object", "C", AprsSymbolCategory.Mobile)]   // /C is Canoe, not Coast Guard
    public void Resolve_KnownPrimarySymbol_ReturnsDescription(
        char table,
        char code,
        string description,
        string iconKey,
        string fallbackText,
        AprsSymbolCategory category)
    {
        var lookup = new AprsSymbolLookupService();

        var symbol = lookup.Resolve(table, code);

        Assert.True(symbol.IsKnown);
        Assert.True(symbol.IsPrimaryTable);
        Assert.False(symbol.IsAlternateTable);
        Assert.Equal(description, symbol.Description);
        Assert.Equal(iconKey, symbol.MarkerIconKey);
        Assert.Equal(fallbackText, symbol.FallbackDisplayText);
        Assert.Equal(category, symbol.Category);
    }

    [Theory]
    [InlineData('\\', '!', "Emergency")]
    [InlineData('\\', 'C', "Coast Guard")]   // \C is Coast Guard
    [InlineData('\\', 'J', "Lightning")]
    [InlineData('\\', 't', "Tornado")]
    [InlineData('\\', '>', "Red car")]
    public void Resolve_KnownAlternateSymbol_ReturnsDescription(char table, char code, string description)
    {
        var lookup = new AprsSymbolLookupService();

        var symbol = lookup.Resolve(table, code);

        Assert.True(symbol.IsKnown);
        Assert.False(symbol.IsPrimaryTable);
        Assert.True(symbol.IsAlternateTable);
        Assert.Equal(description, symbol.Description);
    }

    [Fact]
    public void GetKnownSymbols_CoversBothTables_WithFullDefinedSet()
    {
        var lookup = new AprsSymbolLookupService();

        var all = lookup.GetKnownSymbols();

        // 86 defined primary-table + 73 defined alternate-table = 159 (reserved codes omitted).
        Assert.Equal(159, all.Count);
        Assert.Equal(86, all.Count(s => s.IsPrimaryTable));
        Assert.Equal(73, all.Count(s => s.IsAlternateTable));
        Assert.All(all, s => Assert.True(s.IsKnown));
        Assert.All(all, s => Assert.False(string.IsNullOrWhiteSpace(s.FallbackDisplayText)));
    }

    [Fact]
    public void Resolve_UnknownSymbol_ReturnsSafeFallback()
    {
        var lookup = new AprsSymbolLookupService();

        // 0x22 (") is a reserved/undefined primary code.
        var symbol = lookup.Resolve('/', '"');

        Assert.False(symbol.IsKnown);
        Assert.Equal("Unknown APRS symbol", symbol.Description);
        Assert.Equal("unknown", symbol.MarkerIconKey);
        Assert.Equal("?", symbol.FallbackDisplayText);
    }

    [Fact]
    public void Resolve_OverlaySymbol_UsesAlternateTableAndPreservesOverlay()
    {
        var lookup = new AprsSymbolLookupService();

        var symbol = lookup.Resolve('1', '#');

        Assert.True(symbol.IsKnown);
        Assert.Equal('1', symbol.SymbolTableIdentifier);
        Assert.Equal('1', symbol.Overlay);
        Assert.True(symbol.IsAlternateTable);
        Assert.Equal("Digipeater, green star", symbol.Description);
        Assert.Equal("digipeater", symbol.MarkerIconKey);
    }

    [Fact]
    public void GetKnownSymbols_ReturnsSelectorReadySymbols()
    {
        var lookup = new AprsSymbolLookupService();

        var symbols = lookup.GetKnownSymbols();

        Assert.Contains(symbols, symbol => symbol.SymbolTableIdentifier == '/' && symbol.SymbolCode == '>');
        Assert.Contains(symbols, symbol => symbol.SymbolTableIdentifier == '\\' && symbol.SymbolCode == '>');
    }

    [Fact]
    public void StationMarker_CreateIncludesSymbolMetadata()
    {
        var marker = StationMarker.Create(
            "WX9XYZ",
            "Weather WX9XYZ",
            38.6270,
            -90.1994,
            '/',
            '_',
            DateTimeOffset.UtcNow,
            StationLifecycleState.Active,
            AprsPacketSource.Simulation,
            CourseDegrees: null,
            SpeedKnots: null);

        Assert.Equal("Weather station", marker.SymbolDescription);
        Assert.Equal(AprsSymbolCategory.Weather, marker.SymbolCategory);
        Assert.Equal("weather", marker.MarkerIconKey);
        Assert.Equal("WS", marker.FallbackMarkerText);
    }
}
