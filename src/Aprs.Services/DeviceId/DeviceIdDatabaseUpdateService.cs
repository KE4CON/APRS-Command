namespace Aprs.Services;

public enum DeviceIdUpdateOutcome
{
    /// <summary>A fresh database was downloaded, validated, applied, and cached.</summary>
    Updated,

    /// <summary>The cached copy is still within the refresh interval, so nothing was fetched.</summary>
    SkippedFresh,

    /// <summary>The download or validation failed; the previously-active database is unchanged.</summary>
    Failed
}

public sealed record DeviceIdUpdateResult(
    DeviceIdUpdateOutcome Outcome,
    string? FailureReason,
    DateTimeOffset? LastUpdatedUtc,
    int PatternCount);

/// <summary>
/// Keeps the active device-ID database current: loads the freshest on-disk copy at startup, and refreshes
/// it from the network on a weekly cadence or on explicit request. Every failure mode is non-fatal — the
/// bundled snapshot (or the last good cached copy) stays in use — because identification is a nicety, not
/// a critical path.
/// </summary>
public interface IDeviceIdDatabaseUpdateService
{
    DateTimeOffset? LastUpdatedUtc { get; }

    /// <summary>Loads the cached database (if any) into the active service. Safe to call once at startup.</summary>
    void LoadCachedIfAvailable();

    /// <summary>
    /// Refreshes from the network. When <paramref name="force"/> is false the download is skipped if the
    /// cached copy is still within the refresh interval.
    /// </summary>
    Task<DeviceIdUpdateResult> UpdateAsync(bool force, CancellationToken cancellationToken = default);
}

public sealed class DeviceIdDatabaseUpdateService : IDeviceIdDatabaseUpdateService, IDisposable
{
    /// <summary>Default weekly refresh cadence.</summary>
    public static readonly TimeSpan DefaultRefreshInterval = TimeSpan.FromDays(7);

    private readonly RefreshableDeviceIdentificationService service;
    private readonly IDeviceIdDatabaseDownloader downloader;
    private readonly IDeviceIdDatabaseStore store;
    private readonly IBeaconSchedulerClock clock;
    private readonly TimeSpan refreshInterval;
    private readonly SemaphoreSlim gate = new(1, 1);

    /// <summary>Releases the update gate. The DI container disposes this singleton at shutdown.</summary>
    public void Dispose() => gate.Dispose();

    public DeviceIdDatabaseUpdateService(
        RefreshableDeviceIdentificationService service,
        IDeviceIdDatabaseDownloader downloader,
        IDeviceIdDatabaseStore store,
        IBeaconSchedulerClock? clock = null,
        TimeSpan? refreshInterval = null)
    {
        this.service = service;
        this.downloader = downloader;
        this.store = store;
        this.clock = clock ?? new SystemBeaconSchedulerClock();
        this.refreshInterval = refreshInterval ?? DefaultRefreshInterval;
        LastUpdatedUtc = store.GetLastUpdatedUtc();
    }

    public DateTimeOffset? LastUpdatedUtc { get; private set; }

    public void LoadCachedIfAvailable()
    {
        var cached = store.ReadCachedJson();
        if (string.IsNullOrWhiteSpace(cached))
        {
            return;
        }

        try
        {
            service.LoadFrom(cached);
        }
        catch
        {
            // A corrupt cache must never break startup — the bundled snapshot stays active.
        }
    }

    public async Task<DeviceIdUpdateResult> UpdateAsync(bool force, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!force && LastUpdatedUtc is { } last && clock.UtcNow - last < refreshInterval)
            {
                return new DeviceIdUpdateResult(DeviceIdUpdateOutcome.SkippedFresh, null, LastUpdatedUtc, service.PatternCount);
            }

            string json;
            try
            {
                json = await downloader.DownloadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new DeviceIdUpdateResult(DeviceIdUpdateOutcome.Failed, $"Download failed: {ex.Message}", LastUpdatedUtc, service.PatternCount);
            }

            try
            {
                service.LoadFrom(json); // validates + atomically swaps; throws (unchanged) if unusable
            }
            catch (Exception ex)
            {
                return new DeviceIdUpdateResult(DeviceIdUpdateOutcome.Failed, $"Downloaded database was invalid: {ex.Message}", LastUpdatedUtc, service.PatternCount);
            }

            var now = clock.UtcNow;
            try
            {
                store.Save(json, now);
            }
            catch
            {
                // Cache write is best-effort; the in-memory database is already refreshed.
            }

            LastUpdatedUtc = now;
            return new DeviceIdUpdateResult(DeviceIdUpdateOutcome.Updated, null, now, service.PatternCount);
        }
        finally
        {
            gate.Release();
        }
    }
}
