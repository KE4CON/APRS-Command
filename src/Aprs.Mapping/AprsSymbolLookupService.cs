namespace Aprs.Mapping;

public sealed class AprsSymbolLookupService : IAprsSymbolLookupService
{
    private static readonly IReadOnlyDictionary<(char Table, char Code), AprsSymbol> KnownSymbols =
        CreateKnownSymbols().ToDictionary(symbol => (symbol.SymbolTableIdentifier, symbol.SymbolCode));

    public static AprsSymbolLookupService Default { get; } = new();

    public AprsSymbol Resolve(char? symbolTableIdentifier, char? symbolCode)
    {
        if (symbolTableIdentifier is null || symbolCode is null)
            return CreateUnknown(symbolTableIdentifier, symbolCode, overlay: null);

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
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Description, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static (char NormalizedTable, char? Overlay, bool IsAlternateTable) NormalizeSymbolTable(char t)
    {
        if (t == '/') return ('/', null, false);
        if (t == '\\') return ('\\', null, true);
        return ('\\', t, true);
    }

    private static AprsSymbol CreateUnknown(char? table, char? code, char? overlay)
    {
        var t = table ?? '?';
        var c = code ?? '?';
        return new AprsSymbol(t, c, overlay, "Unknown APRS symbol", AprsSymbolCategory.Unknown,
            IsPrimaryTable: t == '/', IsAlternateTable: t != '/', "unknown", "?", IsKnown: false);
    }

    private static IEnumerable<AprsSymbol> CreateKnownSymbols()
    {
        // ── Primary table '/' ────────────────────────────────────────────────
        // Home / fixed
        yield return P('-', "House / home station",          AprsSymbolCategory.Home,           "home",        "H");
        yield return P('=', "Railroad station / fixed point", AprsSymbolCategory.Infrastructure, "railroad",    "RR");
        yield return P('l', "Laptop / portable station",     AprsSymbolCategory.Infrastructure, "laptop",      "PC");

        // Mobile
        yield return P('>', "Car / mobile station",                 AprsSymbolCategory.Mobile, "car",        "C");
        yield return P('k', "Truck",                         AprsSymbolCategory.Mobile, "truck",      "T");
        yield return P('u', "Bus",                           AprsSymbolCategory.Mobile, "bus",        "B");
        yield return P('f', "Fire truck",                    AprsSymbolCategory.Mobile, "firetruck",  "FT");
        yield return P('v', "Van",                           AprsSymbolCategory.Mobile, "van",        "V");
        yield return P('<', "Motorcycle",                    AprsSymbolCategory.Mobile, "moto",       "M");
        yield return P('[', "Human / person on foot",        AprsSymbolCategory.Mobile, "person",     "P");

        // Weather
        yield return P('_', "Weather station",               AprsSymbolCategory.Weather, "weather",   "WX");
        yield return P('W', "NWS / weather service",         AprsSymbolCategory.Weather, "nws",       "NW");
        yield return P('@', "Hurricane / tropical storm",    AprsSymbolCategory.Weather, "hurricane",  "HU");

        // Digipeaters
        yield return P('#', "Digipeater",                    AprsSymbolCategory.Digipeater, "digipeater", "D");
        yield return P('&', "Gateway (iGate)",               AprsSymbolCategory.Digipeater, "igate",   "IG");

        // Repeaters
        yield return P('r', "Repeater",                AprsSymbolCategory.Repeater, "repeater", "R");
        yield return P('e', "Eyeball (event / net)",         AprsSymbolCategory.Repeater, "eyeball",  "EY");

        // Objects
        yield return P(';', "Object / item",                 AprsSymbolCategory.Object, "object",     "O");
        yield return P('!', "Police / law enforcement",      AprsSymbolCategory.Object, "police",     "PD");
        yield return P('+', "Hospital / first aid",          AprsSymbolCategory.Object, "hospital",   "H+");
        yield return P('h', "Hotel / shelter",               AprsSymbolCategory.Object, "hotel",      "HT");
        yield return P('K', "School",                        AprsSymbolCategory.Object, "school",     "SC");
        yield return P('c', "Incident command post",         AprsSymbolCategory.Object, "icp",        "IC");
        yield return P('F', "Fireplace / campfire",          AprsSymbolCategory.Object, "fire",       "FP");
        yield return P('g', "Glider",                        AprsSymbolCategory.Object, "glider",     "GL");

        // Infrastructure
        yield return P('T', "APRS-connected telephone",      AprsSymbolCategory.Infrastructure, "phone",   "PH");
        yield return P('I', "IRLP / EchoLink node",          AprsSymbolCategory.Infrastructure, "irlp",    "IR");
        yield return P('0', "Circle / undefined",            AprsSymbolCategory.Infrastructure, "circle",  "O");

        // ── Alternate table '\\' ─────────────────────────────────────────────
        yield return A('>', "Alternate table car / mobile station",       AprsSymbolCategory.Mobile,     "car",       "C");
        yield return A('#', "Overlay-capable digipeater",            AprsSymbolCategory.Digipeater, "digipeater","D");
        yield return A('_', "Alternate weather station",     AprsSymbolCategory.Weather,    "weather",   "WX");
        yield return A('r', "Alternate repeater",            AprsSymbolCategory.Repeater,   "repeater",  "R");
        yield return A(';', "Object / item (alternate)",     AprsSymbolCategory.Object,     "object",    "O");
        yield return A('-', "House (alternate table)",       AprsSymbolCategory.Home,       "home",      "H");
        yield return A('k', "SUV / 4WD",                    AprsSymbolCategory.Mobile,     "suv",       "4W");
        yield return A('u', "Snowmobile",                   AprsSymbolCategory.Mobile,     "snowmobile","SM");
    }

    private static AprsSymbol P(char code, string desc, AprsSymbolCategory cat, string icon, string fallback)
        => new('/', code, null, desc, cat, IsPrimaryTable: true, IsAlternateTable: false, icon, fallback, IsKnown: true);

    private static AprsSymbol A(char code, string desc, AprsSymbolCategory cat, string icon, string fallback)
        => new('\\', code, null, desc, cat, IsPrimaryTable: false, IsAlternateTable: true, icon, fallback, IsKnown: true);
}
