namespace Aprs.Desktop.Configuration;

/// <summary>Persisted frequency reference panel settings.</summary>
public sealed record FrequencyReferenceSettings(
    IReadOnlyList<FrequencyEntry> Entries)
{
    public static FrequencyReferenceSettings Default { get; } = new(FrequencyEntry.Defaults);
}
