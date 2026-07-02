using System.Threading.Tasks;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Tests that verify placeholder weather driver source types fail gracefully
/// with clear, actionable error messages rather than crashing or silently
/// returning empty data. The placeholder source types will be replaced by
/// full implementations in future driver variants.
/// </summary>
public sealed class WeatherDriverPlaceholderTests
{
    private static readonly System.DateTimeOffset Now =
        new(2026, 7, 2, 12, 0, 0, System.TimeSpan.Zero);

    // ── Ecowitt / Fine Offset ─────────────────────────────────────────────

    [Fact]
    public async Task Ecowitt_CustomUploadReceiver_FailsWithClearMessage()
    {
        var config = EcowittWeatherConfiguration.Default with
        {
            Enabled = true,
            DataSourceType = EcowittWeatherDataSourceType.CustomUploadReceiverPlaceholder,
            GatewayHost = "192.168.1.100"
        };

        var driver = new EcowittWeatherInputDriver(config);
        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.NotNull(driver.LastError);
        Assert.Contains("placeholder", driver.LastError!.Message, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("local gateway", driver.LastError.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ecowitt_FileImport_FailsWithClearMessage()
    {
        var config = EcowittWeatherConfiguration.Default with
        {
            Enabled = true,
            DataSourceType = EcowittWeatherDataSourceType.FileImportPlaceholder,
            GatewayHost = "192.168.1.100"
        };

        var driver = new EcowittWeatherInputDriver(config);
        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.NotNull(driver.LastError);
        Assert.Contains("placeholder", driver.LastError!.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ecowitt_DefaultConfiguration_UsesLocalGatewayPolling()
    {
        Assert.Equal(
            EcowittWeatherDataSourceType.LocalGatewayHttpPolling,
            EcowittWeatherConfiguration.Default.DataSourceType);
    }

    // ── Davis WeatherLink ─────────────────────────────────────────────────

    [Fact]
    public async Task Davis_LocalFileImport_FailsWithClearMessage()
    {
        var config = DavisWeatherConfiguration.Default with
        {
            Enabled = true,
            DataSourceType = DavisWeatherDataSourceType.LocalFileImport
        };

        var driver = new DavisWeatherInputDriver(config);
        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.NotNull(driver.LastError);
        Assert.Contains("placeholder", driver.LastError!.Message, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Cloud API", driver.LastError.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Davis_LocalHttpIp_FailsWithClearMessage()
    {
        var config = DavisWeatherConfiguration.Default with
        {
            Enabled = true,
            DataSourceType = DavisWeatherDataSourceType.LocalHttpIpPlaceholder
        };

        var driver = new DavisWeatherInputDriver(config);
        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.NotNull(driver.LastError);
        Assert.Contains("placeholder", driver.LastError!.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Davis_SerialLogger_FailsWithClearMessage()
    {
        var config = DavisWeatherConfiguration.Default with
        {
            Enabled = true,
            DataSourceType = DavisWeatherDataSourceType.SerialLoggerPlaceholder
        };

        var driver = new DavisWeatherInputDriver(config);
        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.NotNull(driver.LastError);
        Assert.Contains("placeholder", driver.LastError!.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Davis_DefaultConfiguration_UsesWeatherLinkCloudApi()
    {
        Assert.Equal(
            DavisWeatherDataSourceType.WeatherLinkCloudApi,
            DavisWeatherConfiguration.Default.DataSourceType);
    }

    // ── Ambient Weather ───────────────────────────────────────────────────

    [Fact]
    public async Task AmbientWeather_LocalNetwork_FailsWithClearMessage()
    {
        var config = AmbientWeatherConfiguration.Default with
        {
            Enabled = true,
            DataSourceType = AmbientWeatherDataSourceType.LocalNetworkPlaceholder
        };

        var driver = new AmbientWeatherInputDriver(config);
        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.NotNull(driver.LastError);
        Assert.Contains("placeholder", driver.LastError!.Message, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("API polling", driver.LastError.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AmbientWeather_FileImport_FailsWithClearMessage()
    {
        var config = AmbientWeatherConfiguration.Default with
        {
            Enabled = true,
            DataSourceType = AmbientWeatherDataSourceType.FileImportPlaceholder
        };

        var driver = new AmbientWeatherInputDriver(config);
        await driver.StartAsync();

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.NotNull(driver.LastError);
        Assert.Contains("placeholder", driver.LastError!.Message, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AmbientWeather_DefaultConfiguration_UsesAmbientWeatherApi()
    {
        Assert.Equal(
            AmbientWeatherDataSourceType.AmbientWeatherApi,
            AmbientWeatherConfiguration.Default.DataSourceType);
    }

    // ── Cross-cutting ─────────────────────────────────────────────────────

    [Fact]
    public async Task AllPlaceholderDrivers_DoNotCrashOnPollAfterFailure()
    {
        // Drivers that faulted during start should not throw on poll
        var ecowitt = new EcowittWeatherInputDriver(
            EcowittWeatherConfiguration.Default with
            {
                Enabled = true,
                DataSourceType = EcowittWeatherDataSourceType.FileImportPlaceholder,
                GatewayHost = "192.168.1.1"
            });
        await ecowitt.StartAsync();
        var polled = await ecowitt.PollOnceAsync(Now);
        Assert.False(polled);
    }
}
