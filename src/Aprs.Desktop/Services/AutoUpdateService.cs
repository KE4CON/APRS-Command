using Velopack;
using Velopack.Sources;

namespace Aprs.Desktop.Services;

/// <summary>Result of a startup update check.</summary>
public sealed record StartupUpdateResult(
    bool UpdateAvailable,
    string? LatestVersion,
    string? ReleaseUrl,
    bool CanAutoInstall);

/// <summary>
/// Coordinates the automatic update check that runs shortly after startup.
///
/// <para>Two paths, depending on how the app was installed:</para>
/// <list type="bullet">
/// <item><b>Velopack install</b> (installer download): updates can be downloaded
/// and applied in place, restarting into the new version.</item>
/// <item><b>Portable / zip install</b>: no in-place update is possible, so the
/// check reports the release URL for the operator to download manually.</item>
/// </list>
///
/// <para>Design rules (off-grid friendly):</para>
/// <list type="bullet">
/// <item>Never blocks startup — callers run this in the background after the
/// main window is up.</item>
/// <item>Never surfaces errors. No internet (common for field deployments)
/// means the check silently reports "no update" and the app carries on.</item>
/// <item>Only ever prompts when an update actually exists.</item>
/// </list>
/// </summary>
public sealed class AutoUpdateService
{
    private const string RepoUrl = "https://github.com/KE4CON/APRS-Command";

    private readonly UpdateManager? velopack;
    private UpdateInfo? pendingUpdate;

    public AutoUpdateService()
    {
        // UpdateManager construction is safe anywhere; IsInstalled tells us
        // whether this copy was installed via a Velopack installer.
        try
        {
            velopack = new UpdateManager(new GithubSource(RepoUrl, null, false));
        }
        catch
        {
            velopack = null;
        }
    }

    /// <summary>True when this copy can download and apply updates in place.</summary>
    public bool CanAutoInstall => velopack?.IsInstalled == true;

    /// <summary>
    /// Checks for a newer release. Silent on every kind of failure — no
    /// internet, GitHub down, rate-limited — all simply report no update.
    /// </summary>
    public async Task<StartupUpdateResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        // Velopack path: authoritative when installed, and required for auto-apply.
        if (CanAutoInstall)
        {
            try
            {
                pendingUpdate = await velopack!.CheckForUpdatesAsync().ConfigureAwait(false);
                if (pendingUpdate is not null)
                {
                    return new StartupUpdateResult(
                        UpdateAvailable: true,
                        LatestVersion:   pendingUpdate.TargetFullRelease.Version.ToString(),
                        ReleaseUrl:      $"{RepoUrl}/releases",
                        CanAutoInstall:  true);
                }
                return new StartupUpdateResult(false, null, null, true);
            }
            catch
            {
                // No internet / API failure — silent no-op.
                return new StartupUpdateResult(false, null, null, true);
            }
        }

        // Portable path: GitHub API version comparison only.
        try
        {
            await using var checker = new UpdateCheckerService();
            var result = await checker.CheckAsync(cancellationToken).ConfigureAwait(false);
            return new StartupUpdateResult(
                UpdateAvailable: result.UpdateAvailable,
                LatestVersion:   result.LatestVersion,
                ReleaseUrl:      result.ReleaseUrl ?? $"{RepoUrl}/releases",
                CanAutoInstall:  false);
        }
        catch
        {
            return new StartupUpdateResult(false, null, null, false);
        }
    }

    /// <summary>
    /// Downloads the pending update and restarts into the new version.
    /// Only valid after <see cref="CheckAsync"/> reported an update with
    /// <see cref="StartupUpdateResult.CanAutoInstall"/> true.
    /// Returns false if anything failed (download interrupted, etc.) —
    /// the app keeps running on the current version.
    /// </summary>
    public async Task<bool> DownloadAndRestartAsync()
    {
        if (velopack is null || pendingUpdate is null) return false;
        try
        {
            await velopack.DownloadUpdatesAsync(pendingUpdate).ConfigureAwait(false);
            velopack.ApplyUpdatesAndRestart(pendingUpdate.TargetFullRelease);
            return true; // not reached on success — the process restarts
        }
        catch
        {
            return false;
        }
    }
}
