using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Aprs.Desktop.Services;

/// <summary>Result of an update check.</summary>
public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    string? ReleaseName,
    string? ReleaseNotes,
    bool IsPreRelease,
    DateTimeOffset CheckedAt);

/// <summary>
/// Checks the GitHub Releases API for newer versions of APRS Command.
/// Compares the running version against the latest published release tag.
/// </summary>
public sealed class UpdateCheckerService : IAsyncDisposable
{
    private const string ReleasesUrl =
        "https://api.github.com/repos/KE4CON/APRS-Command/releases";

    private readonly HttpClient http;
    private UpdateCheckResult? lastResult;

    public UpdateCheckerService()
    {
        http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "APRSCommand/0.2.0 (github.com/KE4CON/APRS-Command)");
        http.Timeout = TimeSpan.FromSeconds(15);
    }

    /// <summary>The most recent update check result, or null if never checked.</summary>
    public UpdateCheckResult? LastResult => lastResult;

    /// <summary>
    /// Checks GitHub for the latest release and returns the result.
    /// Compares semantic versions — ignores "v" prefix and pre-release suffixes when comparing.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();
        var checkedAt = DateTimeOffset.UtcNow;

        try
        {
            var json = await http.GetFromJsonAsync<JsonElement[]>(
                ReleasesUrl, cancellationToken).ConfigureAwait(false);

            if (json is null || json.Length == 0)
            {
                lastResult = new UpdateCheckResult(false, currentVersion, null, null, null, null, false, checkedAt);
                return lastResult;
            }

            // Find the latest non-draft release (may include pre-releases).
            var latest = json
                .Where(r => !r.TryGetProperty("draft", out var d) || !d.GetBoolean())
                .FirstOrDefault();

            if (latest.ValueKind == JsonValueKind.Undefined)
            {
                lastResult = new UpdateCheckResult(false, currentVersion, null, null, null, null, false, checkedAt);
                return lastResult;
            }

            var tagName    = latest.TryGetProperty("tag_name", out var tag)    ? tag.GetString()  : null;
            var name       = latest.TryGetProperty("name", out var n)          ? n.GetString()    : null;
            var htmlUrl    = latest.TryGetProperty("html_url", out var url)    ? url.GetString()  : null;
            var body       = latest.TryGetProperty("body", out var b)          ? b.GetString()    : null;
            var preRelease = latest.TryGetProperty("prerelease", out var pr)   && pr.GetBoolean();

            var latestVersion = tagName?.TrimStart('v');
            var updateAvailable = IsNewer(latestVersion, currentVersion);

            lastResult = new UpdateCheckResult(
                updateAvailable, currentVersion, latestVersion,
                htmlUrl, name, body, preRelease, checkedAt);
            return lastResult;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            lastResult = new UpdateCheckResult(
                false, currentVersion, null, null,
                $"Check failed: {ex.Message}", null, false, checkedAt);
            return lastResult;
        }
    }

    private static string GetCurrentVersion()
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        return asm.GetName().Version?.ToString(3) ?? "0.2.0";
    }

    private static bool IsNewer(string? latest, string current)
    {
        if (string.IsNullOrWhiteSpace(latest)) return false;
        // Strip pre-release suffixes like "-alpha"
        var latestClean  = latest.Split('-')[0];
        var currentClean = current.Split('-')[0];
        if (!Version.TryParse(latestClean, out var lv)) return false;
        if (!Version.TryParse(currentClean, out var cv)) return false;
        return lv > cv;
    }

    public async ValueTask DisposeAsync()
    {
        http.Dispose();
        await ValueTask.CompletedTask;
    }
}
