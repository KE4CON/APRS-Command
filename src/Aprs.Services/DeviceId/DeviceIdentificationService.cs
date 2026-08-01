using System.Reflection;
using System.Text.Json;

namespace Aprs.Services;

/// <summary>
/// Default <see cref="IDeviceIdentificationService"/>. Loads a bundled snapshot of the APRS Foundation
/// device-ID database (embedded <c>tocalls.dense.json</c>) once, then resolves a destination tocall to
/// a <see cref="DeviceIdentity"/> by pattern-matching against the <c>tocalls</c> section (with <c>?</c>
/// wildcards), preferring the most specific match. Immutable and thread-safe after construction.
///
/// <para>Scope: this covers the <c>tocalls</c> section (software, trackers, most stations). MIC-E
/// radio-model identification (the <c>mice</c>/<c>micelegacy</c> sections) is a planned follow-up that
/// needs suffix extraction tied to the MIC-E decoder.</para>
/// </summary>
public sealed class DeviceIdentificationService : IDeviceIdentificationService
{
    private readonly IReadOnlyList<TocallPattern> patterns;

    /// <summary>Number of tocall patterns loaded (for diagnostics/tests).</summary>
    public int PatternCount => patterns.Count;

    public DeviceIdentificationService()
        : this(LoadBundledJson())
    {
    }

    /// <summary>Constructs from raw device-ID JSON (used by tests to avoid the embedded resource).</summary>
    public DeviceIdentificationService(string deviceIdJson)
    {
        patterns = Parse(deviceIdJson);
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

    private static IReadOnlyList<TocallPattern> Parse(string json)
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

        var list = new List<TocallPattern>();
        if (root.TryGetProperty("tocalls", out var tocalls) && tocalls.ValueKind == JsonValueKind.Object)
        {
            foreach (var t in tocalls.EnumerateObject())
            {
                var v = t.Value;
                var deviceClass = GetString(v, "class") ?? string.Empty;
                var identity = new DeviceIdentity(
                    Vendor: GetString(v, "vendor") ?? string.Empty,
                    Model: GetString(v, "model") ?? string.Empty,
                    DeviceClass: deviceClass,
                    DeviceClassLabel: classLabels.TryGetValue(deviceClass, out var label) ? label : deviceClass,
                    Os: GetString(v, "os"));
                list.Add(new TocallPattern(t.Name, identity));
            }
        }

        return list;
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
