namespace Aprs.Mapping;

public sealed class AprsSymbolLookupService : IAprsSymbolLookupService
{
    private static readonly IReadOnlyDictionary<(char Table, char Code), AprsSymbol> KnownSymbols =
        CreateKnownSymbols().ToDictionary(s => (s.SymbolTableIdentifier, s.SymbolCode));

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
        var t = table ?? '?'; var c = code ?? '?';
        return new AprsSymbol(t, c, overlay, "Unknown APRS symbol", AprsSymbolCategory.Unknown,
            t == '/', t != '/', "unknown", "?", IsKnown: false);
    }

    private static IEnumerable<AprsSymbol> CreateKnownSymbols()
    {
        // ── Primary table '/' ─────────────────────────────────────────────────

        // Home / fixed stations
        yield return P('-', "House / home station",              AprsSymbolCategory.Home,           "home",       "H");
        yield return P('=', "Railroad / fixed point",            AprsSymbolCategory.Infrastructure, "railroad",   "RR");
        yield return P('l', "Laptop / portable station",         AprsSymbolCategory.Mobile,         "laptop",     "PC");
        yield return P('K', "School",                            AprsSymbolCategory.Infrastructure, "school",     "SC");
        yield return P('h', "Hotel / shelter",                   AprsSymbolCategory.Infrastructure, "hotel",      "SH");
        yield return P('+', "Hospital / first aid",              AprsSymbolCategory.Infrastructure, "hospital",   "H+");
        yield return P('c', "Incident command / EOC",            AprsSymbolCategory.Infrastructure, "eoc",        "IC");
        yield return P('F', "Fireplace / campfire",              AprsSymbolCategory.Infrastructure, "fire",       "FP");

        // Ground vehicles
        yield return P('>', "Car / mobile station",              AprsSymbolCategory.Mobile, "car",        "C");
        yield return P('k', "Truck",                             AprsSymbolCategory.Mobile, "truck",      "TK");
        yield return P('u', "Bus",                               AprsSymbolCategory.Mobile, "bus",        "BU");
        yield return P('f', "Fire truck",                        AprsSymbolCategory.Mobile, "firetruck",  "FT");
        yield return P('v', "Van",                               AprsSymbolCategory.Mobile, "van",        "VN");
        yield return P('<', "Motorcycle",                        AprsSymbolCategory.Mobile, "moto",       "MC");
        yield return P('b', "Bicycle",                           AprsSymbolCategory.Mobile, "bike",       "BK");
        yield return P('[', "Human / person on foot",            AprsSymbolCategory.Mobile, "person",     "P");
        yield return P('y', "Handheld radio / HT",              AprsSymbolCategory.Mobile, "ht",         "HT");
        yield return P('a', "Ambulance",                         AprsSymbolCategory.Mobile, "ambulance",  "AM");
        yield return P('j', "Jeep / off-road vehicle",          AprsSymbolCategory.Mobile, "jeep",       "JP");
        yield return P('p', "Dog team / search & rescue",       AprsSymbolCategory.Mobile, "dog",        "K9");
        yield return P('O', "ARDF / fox hunting",               AprsSymbolCategory.Mobile, "ardf",       "FX");

        // Aircraft
        yield return P('\'', "Aircraft (small)",                 AprsSymbolCategory.Mobile, "plane-sm",   "A");
        yield return P('^', "Aircraft (large)",                  AprsSymbolCategory.Mobile, "plane-lg",   "AJ");
        yield return P('X', "Helicopter",                        AprsSymbolCategory.Mobile, "helo",       "HE");
        yield return P('g', "Glider / sailplane",               AprsSymbolCategory.Mobile, "glider",     "GL");

        // Marine
        yield return P('s', "Ship / powerboat",                  AprsSymbolCategory.Mobile, "ship",       "SH");
        yield return P('Y', "Sailboat",                          AprsSymbolCategory.Mobile, "sail",       "SB");
        yield return P('C', "Coast Guard",                       AprsSymbolCategory.Mobile, "coastguard", "CG");

        // Weather
        yield return P('_', "Weather station",                   AprsSymbolCategory.Weather, "weather",   "WX");
        yield return P('W', "NWS / weather site",               AprsSymbolCategory.Weather, "nws",        "NW");
        yield return P('@', "Hurricane / tropical storm",        AprsSymbolCategory.Weather, "hurricane",  "HU");
        yield return P('*', "Snow / winter storm",               AprsSymbolCategory.Weather, "snow",       "SN");

        // Digipeaters
        yield return P('#', "Digipeater",                        AprsSymbolCategory.Digipeater, "digipeater", "D");
        yield return P('&', "Gateway / iGate",                  AprsSymbolCategory.Digipeater, "igate",      "IG");

        // Repeaters / RF infrastructure
        yield return P('r', "Repeater",                          AprsSymbolCategory.Repeater, "repeater",  "R");
        yield return P('e', "Eyeball / net or event",           AprsSymbolCategory.Repeater, "eyeball",   "EY");
        yield return P('I', "IRLP / EchoLink / VoIP node",     AprsSymbolCategory.Repeater, "irlp",      "IR");

        // Emergency / ARES / EMCOMM
        yield return P('!', "Police / law enforcement",          AprsSymbolCategory.Object, "police",     "PD");
        yield return P('E', "ARES / RACES / EmComm",            AprsSymbolCategory.Object, "ares",       "AR");
        yield return P('d', "DX cluster node",                   AprsSymbolCategory.Object, "dx",         "DX");
        yield return P('N', "NTS / message node",               AprsSymbolCategory.Object, "nts",        "MS");
        yield return P('i', "IGATE / internet station",         AprsSymbolCategory.Object, "inet",       "IN");
        yield return P('T', "APRS-connected telephone",         AprsSymbolCategory.Infrastructure, "phone","PH");
        yield return P('0', "Circle / general",                 AprsSymbolCategory.Object, "circle",     "O");

        // Objects / items
        yield return P(';', "Object / item",                     AprsSymbolCategory.Object, "object",     "OB");
        yield return P('m', "Mic-E repeater",                   AprsSymbolCategory.Object, "mice",       "ME");

        // ── Alternate table '\\' ─────────────────────────────────────────────

        yield return A('-', "House (alternate)",                  AprsSymbolCategory.Home,       "home",       "H");
        yield return A('>', "Alternate table car / mobile station", AprsSymbolCategory.Mobile,     "car",        "C");
        yield return A('#', "Overlay-capable digipeater",        AprsSymbolCategory.Digipeater, "digipeater", "D");
        yield return A('_', "Weather station (alternate)",       AprsSymbolCategory.Weather,    "weather",    "WX");
        yield return A('r', "Repeater (alternate)",              AprsSymbolCategory.Repeater,   "repeater",   "R");
        yield return A(';', "Object / item (alternate)",         AprsSymbolCategory.Object,     "object",     "OB");
        yield return A('k', "SUV / 4WD",                        AprsSymbolCategory.Mobile,     "suv",        "4W");
        yield return A('u', "Snowmobile",                        AprsSymbolCategory.Mobile,     "snowmobile", "SM");
        yield return A('E', "Eyeball (alternate)",               AprsSymbolCategory.Repeater,   "eyeball",    "EY");
        yield return A('&', "Alternate iGate",                   AprsSymbolCategory.Digipeater, "igate",      "IG");
        yield return A('a', "ARES / EmComm (alternate)",        AprsSymbolCategory.Object,     "ares",       "AR");
        yield return A('[', "Person (alternate)",                 AprsSymbolCategory.Mobile,     "person",     "P");
        yield return A('^', "Aircraft (alternate)",              AprsSymbolCategory.Mobile,     "plane-lg",   "AJ");
        yield return A('s', "Ship (alternate)",                  AprsSymbolCategory.Mobile,     "ship",       "SH");
        yield return A('0', "Circle / overlay",                  AprsSymbolCategory.Object,     "circle",     "O");
    }

    private static AprsSymbol P(char code, string desc, AprsSymbolCategory cat, string icon, string fallback)
        => new('/', code, null, desc, cat, true, false, icon, fallback, IsKnown: true);

    private static AprsSymbol A(char code, string desc, AprsSymbolCategory cat, string icon, string fallback)
        => new('\\', code, null, desc, cat, false, true, icon, fallback, IsKnown: true);
}
