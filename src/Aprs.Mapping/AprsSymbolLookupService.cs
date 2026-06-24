namespace Aprs.Mapping;

public sealed class AprsSymbolLookupService : IAprsSymbolLookupService
{
    private static readonly IReadOnlyDictionary<(char Table, char Code), AprsSymbol> KnownSymbols =
        CreateKnownSymbols().ToDictionary(symbol => (symbol.SymbolTableIdentifier, symbol.SymbolCode));

    public static AprsSymbolLookupService Default { get; } = new();

    public AprsSymbol Resolve(char? symbolTableIdentifier, char? symbolCode)
    {
        if (symbolTableIdentifier is null || symbolCode is null)
        {
            return CreateUnknown(symbolTableIdentifier, symbolCode, overlay: null);
        }

        var (normalizedTable, overlay, isAlternateTable) = NormalizeSymbolTable(symbolTableIdentifier.Value);
        if (KnownSymbols.TryGetValue((normalizedTable, symbolCode.Value), out var symbol))
        {
            return symbol with
            {
                SymbolTableIdentifier = symbolTableIdentifier.Value,
                Overlay = overlay,
                IsPrimaryTable = normalizedTable == '/',
                IsAlternateTable = isAlternateTable
            };
        }

        return CreateUnknown(symbolTableIdentifier, symbolCode, overlay);
    }

    public IReadOnlyCollection<AprsSymbol> GetKnownSymbols()
    {
        return KnownSymbols.Values
            .OrderBy(symbol => symbol.Category)
            .ThenBy(symbol => symbol.Description, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static (char NormalizedTable, char? Overlay, bool IsAlternateTable) NormalizeSymbolTable(char symbolTableIdentifier)
    {
        if (symbolTableIdentifier == '/')
        {
            return ('/', null, false);
        }

        if (symbolTableIdentifier == '\\')
        {
            return ('\\', null, true);
        }

        return ('\\', symbolTableIdentifier, true);
    }

    private static AprsSymbol CreateUnknown(char? symbolTableIdentifier, char? symbolCode, char? overlay)
    {
        var table = symbolTableIdentifier ?? '?';
        var code = symbolCode ?? '?';
        return new AprsSymbol(
            table,
            code,
            overlay,
            "Unknown APRS symbol",
            AprsSymbolCategory.Unknown,
            IsPrimaryTable: table == '/',
            IsAlternateTable: table != '/',
            MarkerIconKey: "unknown",
            FallbackDisplayText: "?",
            IsKnown: false);
    }

    private static IEnumerable<AprsSymbol> CreateKnownSymbols()
    {
        yield return Create('/', '-', "House / home station", AprsSymbolCategory.Home, "home", "H");
        yield return Create('/', '>', "Car / mobile station", AprsSymbolCategory.Mobile, "car", "C");
        yield return Create('/', '_', "Weather station", AprsSymbolCategory.Weather, "weather", "WX");
        yield return Create('/', '#', "Digipeater", AprsSymbolCategory.Digipeater, "digipeater", "D");
        yield return Create('/', 'r', "Repeater", AprsSymbolCategory.Repeater, "repeater", "R");
        yield return Create('\\', '>', "Alternate table car / mobile station", AprsSymbolCategory.Mobile, "car", "C");
        yield return Create('\\', '#', "Overlay-capable digipeater", AprsSymbolCategory.Digipeater, "digipeater", "D");
        yield return Create('\\', '_', "Alternate table weather station", AprsSymbolCategory.Weather, "weather", "WX");
        yield return Create('\\', 'r', "Alternate table repeater", AprsSymbolCategory.Repeater, "repeater", "R");
        yield return Create('\\', ';', "Object / item", AprsSymbolCategory.Object, "object", "O");
    }

    private static AprsSymbol Create(
        char table,
        char code,
        string description,
        AprsSymbolCategory category,
        string markerIconKey,
        string fallbackDisplayText)
    {
        return new AprsSymbol(
            table,
            code,
            Overlay: null,
            description,
            category,
            IsPrimaryTable: table == '/',
            IsAlternateTable: table == '\\',
            markerIconKey,
            fallbackDisplayText,
            IsKnown: true);
    }
}
