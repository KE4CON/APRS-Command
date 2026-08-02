using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// The device-ID refresh layer: the hot-swappable service, and the weekly/manual update orchestration
/// (skip-when-fresh, apply + cache on success, and stay on the last good database on any failure).
/// </summary>
public sealed class DeviceIdDatabaseUpdateServiceTests
{
    // A distinctive tocall that won't exist in the bundled snapshot, so resolving it proves a downloaded
    // database actually took effect.
    private const string DownloadedJson = """
    { "classes": { "software": {"shown":"Desktop software"} },
      "tocalls": { "APZTST": {"class":"software","vendor":"Test","model":"Downloaded DB"} } }
    """;

    private static readonly DateTimeOffset T0 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    // ── RefreshableDeviceIdentificationService ───────────────────────────────

    [Fact]
    public void Refreshable_StartsOnBundledSnapshot()
    {
        var service = new RefreshableDeviceIdentificationService();

        Assert.True(service.PatternCount > 300);       // bundled dataset loaded
        Assert.Null(service.Identify("APZTST"));       // synthetic tocall not in the bundle
        Assert.Equal("APRS Command", service.Identify("APCMD0")!.Model);
    }

    [Fact]
    public void Refreshable_LoadFrom_SwapsTheActiveDatabase()
    {
        var service = new RefreshableDeviceIdentificationService();

        service.LoadFrom(DownloadedJson);

        Assert.Equal("Downloaded DB", service.Identify("APZTST")!.Model);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ \"tocalls\": {} }")] // parses but has no patterns
    public void Refreshable_LoadFrom_RejectsUnusableJson_AndKeepsCurrent(string badJson)
    {
        var service = new RefreshableDeviceIdentificationService();

        Assert.ThrowsAny<Exception>(() => service.LoadFrom(badJson));
        Assert.Equal("APRS Command", service.Identify("APCMD0")!.Model); // unchanged
    }

    // ── Update orchestration ─────────────────────────────────────────────────

    private static (RefreshableDeviceIdentificationService Service, FakeDownloader Downloader, InMemoryStore Store, FakeClock Clock, DeviceIdDatabaseUpdateService Updater)
        CreateUpdater(DateTimeOffset? lastUpdated = null)
    {
        var service = new RefreshableDeviceIdentificationService();
        var downloader = new FakeDownloader();
        var store = new InMemoryStore();
        if (lastUpdated is { } stamp)
        {
            store.Save("{\"tocalls\":{\"APOLD0\":{\"class\":\"software\",\"vendor\":\"x\",\"model\":\"old\"}}}", stamp);
        }

        var clock = new FakeClock { UtcNow = T0.AddDays(30) };
        var updater = new DeviceIdDatabaseUpdateService(service, downloader, store, clock);
        return (service, downloader, store, clock, updater);
    }

    [Fact]
    public async Task Update_Force_DownloadsAppliesAndCaches()
    {
        var (service, downloader, store, clock, updater) = CreateUpdater();
        downloader.NextResult = DownloadedJson;

        var result = await updater.UpdateAsync(force: true);

        Assert.Equal(DeviceIdUpdateOutcome.Updated, result.Outcome);
        Assert.Equal("Downloaded DB", service.Identify("APZTST")!.Model); // applied in memory
        Assert.Equal(DownloadedJson, store.ReadCachedJson());             // persisted
        Assert.Equal(clock.UtcNow, updater.LastUpdatedUtc);
    }

    [Fact]
    public async Task Update_SkipsWhenCacheIsWithinRefreshInterval()
    {
        // Last updated 2 days ago; interval is a week → skip, no download.
        var (_, downloader, _, clock, updater) = CreateUpdater(lastUpdated: T0.AddDays(28));
        clock.UtcNow = T0.AddDays(30);
        downloader.NextResult = DownloadedJson;

        var result = await updater.UpdateAsync(force: false);

        Assert.Equal(DeviceIdUpdateOutcome.SkippedFresh, result.Outcome);
        Assert.Equal(0, downloader.CallCount);
    }

    [Fact]
    public async Task Update_RefreshesWhenCacheIsStale()
    {
        // Last updated 10 days ago → past the weekly interval → download.
        var (service, downloader, _, clock, updater) = CreateUpdater(lastUpdated: T0.AddDays(20));
        clock.UtcNow = T0.AddDays(30);
        downloader.NextResult = DownloadedJson;

        var result = await updater.UpdateAsync(force: false);

        Assert.Equal(DeviceIdUpdateOutcome.Updated, result.Outcome);
        Assert.Equal("Downloaded DB", service.Identify("APZTST")!.Model);
    }

    [Fact]
    public async Task Update_DownloadFailure_KeepsCurrentDatabase()
    {
        var (service, downloader, store, _, updater) = CreateUpdater();
        downloader.Exception = new HttpRequestException("offline");

        var result = await updater.UpdateAsync(force: true);

        Assert.Equal(DeviceIdUpdateOutcome.Failed, result.Outcome);
        Assert.Contains("Download failed", result.FailureReason);
        Assert.Equal("APRS Command", service.Identify("APCMD0")!.Model); // bundled still active
        Assert.Null(store.ReadCachedJson());                             // nothing cached
    }

    [Fact]
    public async Task Update_InvalidDownload_KeepsCurrentDatabase()
    {
        var (service, downloader, store, _, updater) = CreateUpdater();
        downloader.NextResult = "garbage not json";

        var result = await updater.UpdateAsync(force: true);

        Assert.Equal(DeviceIdUpdateOutcome.Failed, result.Outcome);
        Assert.Contains("invalid", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("APRS Command", service.Identify("APCMD0")!.Model);
        Assert.Null(store.ReadCachedJson());
    }

    [Fact]
    public void LoadCachedIfAvailable_AppliesCache_ButToleratesCorruption()
    {
        var service = new RefreshableDeviceIdentificationService();
        var store = new InMemoryStore();
        store.Save(DownloadedJson, T0);
        var good = new DeviceIdDatabaseUpdateService(service, new FakeDownloader(), store, new FakeClock());

        good.LoadCachedIfAvailable();
        Assert.Equal("Downloaded DB", service.Identify("APZTST")!.Model);

        var service2 = new RefreshableDeviceIdentificationService();
        var corruptStore = new InMemoryStore();
        corruptStore.Save("}{ broken", T0);
        var bad = new DeviceIdDatabaseUpdateService(service2, new FakeDownloader(), corruptStore, new FakeClock());

        bad.LoadCachedIfAvailable(); // must not throw
        Assert.Equal("APRS Command", service2.Identify("APCMD0")!.Model); // bundled still active
    }

    private sealed class FakeDownloader : IDeviceIdDatabaseDownloader
    {
        public string? NextResult { get; set; }
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }

        public Task<string> DownloadAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (Exception is not null) throw Exception;
            return Task.FromResult(NextResult ?? throw new InvalidOperationException("no result configured"));
        }
    }

    private sealed class InMemoryStore : IDeviceIdDatabaseStore
    {
        private string? json;
        private DateTimeOffset? updated;

        public string? ReadCachedJson() => json;
        public DateTimeOffset? GetLastUpdatedUtc() => updated;
        public void Save(string j, DateTimeOffset u) { json = j; updated = u; }
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; } = T0;
    }
}
