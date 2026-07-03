using Aprs.Desktop.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Tests for AutoUpdateService — the startup update coordinator.
/// Network-dependent paths (actual GitHub checks) are integration-level;
/// these tests cover the contract that matters for field deployments:
/// construction never throws, and failures are always silent.
/// </summary>
public sealed class AutoUpdateServiceTests
{
    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var exception = Record.Exception(() => new AutoUpdateService());
        Assert.Null(exception);
    }

    [Fact]
    public void CanAutoInstall_IsFalse_WhenNotVelopackInstalled()
    {
        // Test host is never a Velopack-installed app.
        var svc = new AutoUpdateService();
        Assert.False(svc.CanAutoInstall);
    }

    [Fact]
    public async Task DownloadAndRestartAsync_ReturnsFalse_WithoutPendingUpdate()
    {
        // No CheckAsync has run, so there is no pending update — must be a safe no-op.
        var svc = new AutoUpdateService();
        var ok = await svc.DownloadAndRestartAsync();
        Assert.False(ok);
    }

    [Fact]
    public async Task CheckAsync_NeverThrows_EvenWithoutNetwork()
    {
        // The whole design contract: offline field deployments must never see
        // an exception or error from the update check.
        var svc = new AutoUpdateService();
        var exception = await Record.ExceptionAsync(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await svc.CheckAsync(cts.Token);
            Assert.NotNull(result);
        });
        Assert.Null(exception);
    }

    [Fact]
    public void StartupUpdateResult_PreservesFields()
    {
        var r = new StartupUpdateResult(true, "0.9.9", "https://example.com/rel", false);
        Assert.True(r.UpdateAvailable);
        Assert.Equal("0.9.9", r.LatestVersion);
        Assert.Equal("https://example.com/rel", r.ReleaseUrl);
        Assert.False(r.CanAutoInstall);
    }
}
