using System.Text.RegularExpressions;

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

    // The complete APRS primary ('/') and alternate ('\') symbol tables. Descriptions are taken
    // verbatim from the authoritative aprs.fi symbol index (hessu/aprs-symbol-index, CC BY-SA 4.0),
    // which is also the source of the bundled icon sheets. Reserved/undefined code positions are
    // intentionally omitted. Category, marker-dot key, and the short letter designation are derived
    // from the description so this list stays a single, spec-faithful source of truth.
    private static IEnumerable<AprsSymbol> CreateKnownSymbols()
    {
        // ── Primary table '/' ─────────────────────────────────────────────────
        yield return Sym('/', '!', "Police station");
        yield return Sym('/', '#', "Digipeater");
        yield return Sym('/', '$', "Telephone");
        yield return Sym('/', '%', "DX cluster");
        yield return Sym('/', '&', "HF gateway");
        yield return Sym('/', '\'', "Small aircraft");
        yield return Sym('/', '(', "Mobile satellite station");
        yield return Sym('/', ')', "Wheelchair, handicapped");
        yield return Sym('/', '*', "Snowmobile");
        yield return Sym('/', '+', "Red Cross");
        yield return Sym('/', ',', "Boy Scouts");
        yield return Sym('/', '-', "House");
        yield return Sym('/', '.', "Red X");
        yield return Sym('/', '/', "Red dot");
        yield return Sym('/', '0', "Numbered circle: 0");
        yield return Sym('/', '1', "Numbered circle: 1");
        yield return Sym('/', '2', "Numbered circle: 2");
        yield return Sym('/', '3', "Numbered circle: 3");
        yield return Sym('/', '4', "Numbered circle: 4");
        yield return Sym('/', '5', "Numbered circle: 5");
        yield return Sym('/', '6', "Numbered circle: 6");
        yield return Sym('/', '7', "Numbered circle: 7");
        yield return Sym('/', '8', "Numbered circle: 8");
        yield return Sym('/', '9', "Numbered circle: 9");
        yield return Sym('/', ':', "Fire");
        yield return Sym('/', ';', "Campground, tent");
        yield return Sym('/', '<', "Motorcycle");
        yield return Sym('/', '=', "Railroad engine");
        yield return Sym('/', '>', "Car");
        yield return Sym('/', '?', "File server");
        yield return Sym('/', '@', "Hurricane predicted path");
        yield return Sym('/', 'A', "Aid station");
        yield return Sym('/', 'B', "BBS");
        yield return Sym('/', 'C', "Canoe");
        yield return Sym('/', 'E', "Eyeball");
        yield return Sym('/', 'F', "Farm vehicle, tractor");
        yield return Sym('/', 'G', "Grid square, 3 by 3");
        yield return Sym('/', 'H', "Hotel");
        yield return Sym('/', 'I', "TCP/IP network station");
        yield return Sym('/', 'K', "School");
        yield return Sym('/', 'L', "PC user");
        yield return Sym('/', 'M', "Mac apple");
        yield return Sym('/', 'N', "NTS station");
        yield return Sym('/', 'O', "Balloon");
        yield return Sym('/', 'P', "Police car");
        yield return Sym('/', 'R', "Recreational vehicle");
        yield return Sym('/', 'S', "Space Shuttle");
        yield return Sym('/', 'T', "SSTV");
        yield return Sym('/', 'U', "Bus");
        yield return Sym('/', 'V', "ATV, Amateur Television");
        yield return Sym('/', 'W', "Weather service site");
        yield return Sym('/', 'X', "Helicopter");
        yield return Sym('/', 'Y', "Sailboat");
        yield return Sym('/', 'Z', "Windows flag");
        yield return Sym('/', '[', "Human");
        yield return Sym('/', '\\', "DF triangle");
        yield return Sym('/', ']', "Mailbox, post office");
        yield return Sym('/', '^', "Large aircraft");
        yield return Sym('/', '_', "Weather station");
        yield return Sym('/', '`', "Satellite dish antenna");
        yield return Sym('/', 'a', "Ambulance");
        yield return Sym('/', 'b', "Bicycle");
        yield return Sym('/', 'c', "Incident command post");
        yield return Sym('/', 'd', "Fire station");
        yield return Sym('/', 'e', "Horse, equestrian");
        yield return Sym('/', 'f', "Fire truck");
        yield return Sym('/', 'g', "Glider");
        yield return Sym('/', 'h', "Hospital");
        yield return Sym('/', 'i', "IOTA, islands on the air");
        yield return Sym('/', 'j', "Jeep");
        yield return Sym('/', 'k', "Truck");
        yield return Sym('/', 'l', "Laptop");
        yield return Sym('/', 'm', "Mic-E repeater");
        yield return Sym('/', 'n', "Node, black bulls-eye");
        yield return Sym('/', 'o', "Emergency operations center");
        yield return Sym('/', 'p', "Dog");
        yield return Sym('/', 'q', "Grid square, 2 by 2");
        yield return Sym('/', 'r', "Repeater tower");
        yield return Sym('/', 's', "Ship, power boat");
        yield return Sym('/', 't', "Truck stop");
        yield return Sym('/', 'u', "Semi-trailer truck, 18-wheeler");
        yield return Sym('/', 'v', "Van");
        yield return Sym('/', 'w', "Water station");
        yield return Sym('/', 'x', "X / Unix");
        yield return Sym('/', 'y', "House, yagi antenna");
        yield return Sym('/', 'z', "Shelter");

        // ── Alternate table '\' ───────────────────────────────────────────────
        yield return Sym('\\', '!', "Emergency");
        yield return Sym('\\', '#', "Digipeater, green star");
        yield return Sym('\\', '$', "Bank or ATM");
        yield return Sym('\\', '&', "Gateway station");
        yield return Sym('\\', '\'', "Crash / incident site");
        yield return Sym('\\', '(', "Cloudy");
        yield return Sym('\\', ')', "Firenet MEO, MODIS Earth Observation");
        yield return Sym('\\', '*', "Snow");
        yield return Sym('\\', '+', "Church");
        yield return Sym('\\', ',', "Girl Scouts");
        yield return Sym('\\', '-', "House, HF antenna");
        yield return Sym('\\', '.', "Ambiguous, question mark inside circle");
        yield return Sym('\\', '/', "Waypoint destination");
        yield return Sym('\\', '0', "Circle, IRLP / Echolink/WIRES");
        yield return Sym('\\', '8', "802.11 WiFi or other network node");
        yield return Sym('\\', '9', "Gas station");
        yield return Sym('\\', ':', "Hail");
        yield return Sym('\\', ';', "Park, picnic area");
        yield return Sym('\\', '<', "Advisory, single red flag");
        yield return Sym('\\', '>', "Red car");
        yield return Sym('\\', '?', "Info kiosk");
        yield return Sym('\\', '@', "Hurricane, Tropical storm");
        yield return Sym('\\', 'A', "White box");
        yield return Sym('\\', 'B', "Blowing snow");
        yield return Sym('\\', 'C', "Coast Guard");
        yield return Sym('\\', 'D', "Drizzling rain");
        yield return Sym('\\', 'E', "Smoke, Chimney");
        yield return Sym('\\', 'F', "Freezing rain");
        yield return Sym('\\', 'G', "Snow shower");
        yield return Sym('\\', 'H', "Haze");
        yield return Sym('\\', 'I', "Rain shower");
        yield return Sym('\\', 'J', "Lightning");
        yield return Sym('\\', 'K', "Kenwood HT");
        yield return Sym('\\', 'L', "Lighthouse");
        yield return Sym('\\', 'N', "Navigation buoy");
        yield return Sym('\\', 'O', "Rocket");
        yield return Sym('\\', 'P', "Parking");
        yield return Sym('\\', 'Q', "Earthquake");
        yield return Sym('\\', 'R', "Restaurant");
        yield return Sym('\\', 'S', "Satellite");
        yield return Sym('\\', 'T', "Thunderstorm");
        yield return Sym('\\', 'U', "Sunny");
        yield return Sym('\\', 'V', "VORTAC, Navigational aid");
        yield return Sym('\\', 'W', "NWS site");
        yield return Sym('\\', 'X', "Pharmacy");
        yield return Sym('\\', '[', "Wall Cloud");
        yield return Sym('\\', '^', "Aircraft");
        yield return Sym('\\', '_', "Weather site");
        yield return Sym('\\', '`', "Rain");
        yield return Sym('\\', 'a', "Red diamond");
        yield return Sym('\\', 'b', "Blowing dust, sand");
        yield return Sym('\\', 'c', "CD triangle, RACES, CERTS, SATERN");
        yield return Sym('\\', 'd', "DX spot");
        yield return Sym('\\', 'e', "Sleet");
        yield return Sym('\\', 'f', "Funnel cloud");
        yield return Sym('\\', 'g', "Gale, two red flags");
        yield return Sym('\\', 'h', "Store");
        yield return Sym('\\', 'i', "Black box, point of interest");
        yield return Sym('\\', 'j', "Work zone, excavating machine");
        yield return Sym('\\', 'k', "SUV, ATV");
        yield return Sym('\\', 'm', "Value sign, 3 digit display");
        yield return Sym('\\', 'n', "Red triangle");
        yield return Sym('\\', 'o', "Small circle");
        yield return Sym('\\', 'p', "Partly cloudy");
        yield return Sym('\\', 'r', "Restrooms");
        yield return Sym('\\', 's', "Ship, boat");
        yield return Sym('\\', 't', "Tornado");
        yield return Sym('\\', 'u', "Truck");
        yield return Sym('\\', 'v', "Van");
        yield return Sym('\\', 'w', "Flooding");
        yield return Sym('\\', 'y', "Skywarn");
        yield return Sym('\\', 'z', "Shelter");
        yield return Sym('\\', '{', "Fog");
    }

    private static AprsSymbol Sym(char table, char code, string description)
    {
        var primary = table == '/';
        return new AprsSymbol(
            table, code, null, description, CategoryFor(description),
            IsPrimaryTable: primary, IsAlternateTable: !primary,
            MarkerIconKey: IconKeyFor(description), FallbackDisplayText: FallbackFor(description),
            IsKnown: true);
    }

    // Groups a symbol for ordering/coloring. Keyword-based, derived from the description so the full
    // table stays a single source of truth. Order matters: earlier checks win.
    private static AprsSymbolCategory CategoryFor(string description)
    {
        var d = description.ToLowerInvariant();
        bool Has(params string[] keys) => keys.Any(d.Contains);

        if (Has("weather", "rain", "snow", "storm", "cloud", "hurricane", "thunder", "hail", "haze",
                "sunny", "tornado", "fog", "sleet", "drizzle", "funnel", "gale", "flood", "smoke",
                "blowing", "freezing", "wall cloud", "skywarn", "nws"))
            return AprsSymbolCategory.Weather;
        if (Has("house", "home"))
            return AprsSymbolCategory.Home;
        if (Has("digipeater", "gateway", "igate", "node", "network", "wifi", "tcp/ip"))
            return AprsSymbolCategory.Digipeater;
        if (Has("repeater"))
            return AprsSymbolCategory.Repeater;
        if (Has("car", "truck", "van", "bus", "motorcycle", "bicycle", "jeep", "ambulance", "fire truck",
                "aircraft", "helicopter", "balloon", "boat", "ship", "canoe", "sailboat", "yacht",
                "snowmobile", "suv", "atv", "train", "railroad", "rocket", "satellite", "shuttle",
                "glider", "wheelchair", "horse", "dog", "vehicle", "semi", "human", "bike"))
            return AprsSymbolCategory.Mobile;
        if (Has("hotel", "hospital", "school", "church", "bank", "restaurant", "store", "shelter",
                "lighthouse", "restroom", "parking", "pharmacy", "kiosk", "gas station", "bbs",
                "aid station", "incident command", "emergency operations", "fire station", "campground",
                "mailbox", "park"))
            return AprsSymbolCategory.Infrastructure;
        return AprsSymbolCategory.Object;
    }

    // Key for the colored-dot fallback (used only when a symbol has no drawable sprite).
    private static string IconKeyFor(string description)
    {
        var d = description.ToLowerInvariant();
        if (d.Contains("house") || d.Contains("home")) return "home";
        if (d.Contains("digipeater")) return "digipeater";
        if (d.Contains("repeater")) return "repeater";
        if (d.Contains("truck") || d.Contains("van") || d.Contains("bus") || d.Contains("semi")) return "truck";
        if (d.Contains("car")) return "car";
        if (CategoryFor(description) == AprsSymbolCategory.Weather) return "weather";
        return "object";
    }

    // Short letter designation shown beside the icon (e.g. House -> "HO", Police station -> "PS",
    // Numbered circle: 3 -> "3"). A readable stand-in; the full name is always in the tooltip.
    private static string FallbackFor(string description)
    {
        var tokens = Regex.Matches(description, "[A-Za-z0-9]+")
            .Select(m => m.Value)
            .ToList();
        if (tokens.Count == 0)
            return "?";

        // Numbered circles and similar: a trailing single digit is the clearest label.
        if (tokens.Count > 1 && tokens[^1].Length == 1 && char.IsDigit(tokens[^1][0]))
            return tokens[^1];

        // Multi-word: initials of the first two words (e.g. "Police station" -> "PS").
        if (tokens.Count >= 2)
            return $"{char.ToUpperInvariant(tokens[0][0])}{char.ToUpperInvariant(tokens[1][0])}";

        // Single word: just its first letter, matching the familiar one-letter codes
        // (House -> "H", Car -> "C", Digipeater -> "D").
        return char.ToUpperInvariant(tokens[0][0]).ToString();
    }
}
