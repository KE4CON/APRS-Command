using System.Reflection;
using System.Text.Json;

namespace Aprs.Services;

/// <summary>
/// Default <see cref="IDeviceIdentificationService"/>. Loads a bundled snapshot of the APRS Foundation
/// device-ID database (embedded <c>tocalls.dense.json</c>) once, then resolves a station to a
/// <see cref="DeviceIdentity"/> two ways: by pattern-matching the destination tocall against the
/// <c>tocalls</c> section (with <c>?</c> wildcards, most-specific match winning), and by matching a MIC-E
/// packet's comment against the <c>mice</c>/<c>micelegacy</c> sections. Immutable and thread-safe after
/// construction.
/// </summary>
public sealed class DeviceIdentificationService : IDeviceIdentificationService
{
    private readonly IReadOnlyList<TocallPattern> patterns;

    // Modern MIC-E: the comment's last two characters are the device code (e.g. "_\"" = Yaesu FTM-350).
    private readonly IReadOnlyDictionary<string, DeviceIdentity> miceByCode;

    // Legacy Kenwood MIC-E: the comment starts with a prefix character and may end with a suffix
    // character. Keyed by prefix+suffix (e.g. "]=") and by bare prefix (e.g. "]"), so both a decorated
    // and a plain comment resolve.
    private readonly IReadOnlyDictionary<string, DeviceIdentity> miceLegacyByKey;

    /// <summary>Number of tocall patterns loaded (for diagnostics/tests).</summary>
    public int PatternCount => patterns.Count;

    public DeviceIdentificationService()
        : this(LoadBundledJson())
    {
    }

    /// <summary>Constructs from raw device-ID JSON (used by tests to avoid the embedded resource).</summary>
    public DeviceIdentificationService(string deviceIdJson)
    {
        var parsed = Parse(deviceIdJson);
        patterns = parsed.Tocalls;
        miceByCode = parsed.Mice;
        miceLegacyByKey = parsed.MiceLegacy;
    }

    public DeviceIdentity? Identify(string? destinationTocall)
    {
        if (string.IsNullOrEmpty(destinationTocall)) return null;

        TocallPattern? best = null;
        foreach (var p in patterns)
        {
            if (!p.Matches(destinationTocall)) continue;
            // Most specific wins: more fixed (non-wildcard) characters, then a longer pattern.
            if (best is null
                || p.SpecificChars > best.SpecificChars
                || (p.SpecificChars == best.SpecificChars && p.Length > best.Length))
            {
                best = p;
            }
        }

        return best?.Identity;
    }

    public DeviceIdentity? IdentifyMicE(string? micEComment)
    {
        var comment = StripStatusPrefix(micEComment);
        if (comment.Length == 0) return null;

        // Modern radios first: an unusual two-character code at the very end of the comment. Checking
        // this before the legacy prefix avoids a modern comment that happens to start with '>'/']' being
        // mistaken for a Kenwood.
        if (comment.Length >= 2
            && miceByCode.TryGetValue(comment[^2..], out var modern))
        {
            return modern;
        }

        // Legacy Kenwood: a prefix character (with any operator text after it) and an optional suffix
        // character at the very end. Prefer the more specific prefix+suffix, then the bare prefix.
        var prefix = comment[0];
        if (comment.Length >= 2
            && miceLegacyByKey.TryGetValue(new string(new[] { prefix, comment[^1] }), out var withSuffix))
        {
            return withSuffix;
        }

        return miceLegacyByKey.TryGetValue(prefix.ToString(), out var prefixOnly) ? prefixOnly : null;
    }

    public DeviceIdentity? Identify(string? destinationTocall, string? micEComment)
        => Identify(destinationTocall) ?? IdentifyMicE(micEComment);

    /// <summary>
    /// Removes a leading "[status] " tag that the MIC-E decoder prepends to the comment (e.g.
    /// "[En Route] ]" → "]"), so the legacy prefix character is exposed for matching. A modern trailing
    /// code is unaffected either way.
    /// </summary>
    private static string StripStatusPrefix(string? comment)
    {
        if (string.IsNullOrEmpty(comment)) return string.Empty;
        if (comment[0] != '[') return comment;

        var close = comment.IndexOf(']');
        if (close < 0) return comment;

        var rest = comment[(close + 1)..];
        return rest.StartsWith(' ') ? rest[1..] : rest;
    }

    private static ParsedDatabase Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var classLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("classes", out var classes) && classes.ValueKind == JsonValueKind.Object)
        {
            foreach (var c in classes.EnumerateObject())
            {
                classLabels[c.Name] = GetString(c.Value, "shown") ?? c.Name;
            }
        }

        DeviceIdentity ReadIdentity(JsonElement v)
        {
            var deviceClass = GetString(v, "class") ?? string.Empty;
            return new DeviceIdentity(
                Vendor: GetString(v, "vendor") ?? string.Empty,
                Model: GetString(v, "model") ?? string.Empty,
                DeviceClass: deviceClass,
                DeviceClassLabel: classLabels.TryGetValue(deviceClass, out var label) ? label : deviceClass,
                Os: GetString(v, "os"));
        }

        var list = new List<TocallPattern>();
        if (root.TryGetProperty("tocalls", out var tocalls) && tocalls.ValueKind == JsonValueKind.Object)
        {
            foreach (var t in tocalls.EnumerateObject())
            {
                list.Add(new TocallPattern(t.Name, ReadIdentity(t.Value)));
            }
        }

        var mice = new Dictionary<string, DeviceIdentity>(StringComparer.Ordinal);
        if (root.TryGetProperty("mice", out var miceSection) && miceSection.ValueKind == JsonValueKind.Object)
        {
            foreach (var m in miceSection.EnumerateObject())
            {
                mice[m.Name] = ReadIdentity(m.Value);
            }
        }

        var legacy = new Dictionary<string, DeviceIdentity>(StringComparer.Ordinal);
        if (root.TryGetProperty("micelegacy", out var legacySection) && legacySection.ValueKind == JsonValueKind.Object)
        {
            foreach (var m in legacySection.EnumerateObject())
            {
                legacy[m.Name] = ReadIdentity(m.Value);
            }
        }

        return new ParsedDatabase(list, mice, legacy);
    }

    private static string? GetString(JsonElement element, string property)
        => element.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static string LoadBundledJson()
    {
        var assembly = typeof(DeviceIdentificationService).Assembly;
        var name = Array.Find(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith("tocalls.dense.json", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Bundled device-ID database resource was not found.");

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Bundled device-ID database resource could not be opened.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The three device-ID sections parsed from the database in one pass.</summary>
    private sealed record ParsedDatabase(
        IReadOnlyList<TocallPattern> Tocalls,
        IReadOnlyDictionary<string, DeviceIdentity> Mice,
        IReadOnlyDictionary<string, DeviceIdentity> MiceLegacy);

    /// <summary>A single tocall pattern (which may contain '?' wildcards) and its resolved identity.</summary>
    private sealed class TocallPattern
    {
        private readonly string pattern;

        public DeviceIdentity Identity { get; }
        public int Length => pattern.Length;
        public int SpecificChars { get; }

        public TocallPattern(string pattern, DeviceIdentity identity)
        {
            this.pattern = pattern;
            Identity = identity;
            SpecificChars = pattern.Count(c => c != '?');
        }

        /// <summary>True if the pattern matches the start of the tocall ('?' = any single character).</summary>
        public bool Matches(string tocall)
        {
            if (pattern.Length > tocall.Length) return false;
            for (var i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] != '?' && pattern[i] != tocall[i]) return false;
            }
            return true;
        }
    }
}
