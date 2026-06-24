using Aprs.Mapping;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsSymbolLookupServiceTests
{
    [Theory]
    [InlineData('/', '-', "House / home station", "home", "H", AprsSymbolCategory.Home)]
    [InlineData('/', '>', "Car / mobile station", "car", "C", AprsSymbolCategory.Mobile)]
    [InlineData('/', '_', "Weather station", "weather", "WX", AprsSymbolCategory.Weather)]
    [InlineData('/', '#', "Digipeater", "digipeater", "D", AprsSymbolCategory.Digipeater)]
    [InlineData('/', 'r', "Repeater", "repeater", "R", AprsSymbolCategory.Repeater)]
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

    [Fact]
    public void Resolve_KnownAlternateSymbol_ReturnsDescription()
    {
        var lookup = new AprsSymbolLookupService();

        var symbol = lookup.Resolve('\\', '>');

        Assert.True(symbol.IsKnown);
        Assert.False(symbol.IsPrimaryTable);
        Assert.True(symbol.IsAlternateTable);
        Assert.Equal("Alternate table car / mobile station", symbol.Description);
        Assert.Equal("car", symbol.MarkerIconKey);
        Assert.Equal("C", symbol.FallbackDisplayText);
    }

    [Fact]
    public void Resolve_UnknownSymbol_ReturnsSafeFallback()
    {
        var lookup = new AprsSymbolLookupService();

        var symbol = lookup.Resolve('/', '?');

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
        Assert.Equal("Overlay-capable digipeater", symbol.Description);
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
        Assert.Equal("WX", marker.FallbackMarkerText);
    }
}
