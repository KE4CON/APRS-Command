using System.Globalization;

namespace Aprs.Services;

/// <summary>
/// Persists the most recently downloaded device-ID database (and when it was fetched) so a fresher copy
/// than the bundled snapshot survives restarts and the weekly refresh gate has something to compare to.
/// </summary>
public interface IDeviceIdDatabaseStore
{
    /// <summary>The cached database JSON, or null if nothing has been downloaded yet.</summary>
    string? ReadCachedJson();

    /// <summary>When the cached copy was last written (UTC), or null if there is none.</summary>
    DateTimeOffset? GetLastUpdatedUtc();

    /// <summary>Writes the database JSON and stamps the update time.</summary>
    void Save(string json, DateTimeOffset updatedUtc);
}

/// <summary>File-backed <see cref="IDeviceIdDatabaseStore"/> writing into a single folder.</summary>
public sealed class FileDeviceIdDatabaseStore : IDeviceIdDatabaseStore
{
    private const string JsonFileName = "tocalls.dense.json";
    private const string TimestampFileName = "tocalls.updated.utc";

    private readonly string folderPath;

    public FileDeviceIdDatabaseStore(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        this.folderPath = folderPath;
    }

    private string JsonPath => Path.Combine(folderPath, JsonFileName);

    private string TimestampPath => Path.Combine(folderPath, TimestampFileName);

    public string? ReadCachedJson()
        => File.Exists(JsonPath) ? File.ReadAllText(JsonPath) : null;

    public DateTimeOffset? GetLastUpdatedUtc()
    {
        if (!File.Exists(TimestampPath))
        {
            return null;
        }

        var text = File.ReadAllText(TimestampPath).Trim();
        return DateTimeOffset.TryParse(
            text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
    }

    public void Save(string json, DateTimeOffset updatedUtc)
    {
        Directory.CreateDirectory(folderPath);
        File.WriteAllText(JsonPath, json);
        File.WriteAllText(TimestampPath, updatedUtc.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
    }
}
