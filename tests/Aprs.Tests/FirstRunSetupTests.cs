using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class FirstRunSetupTests : IDisposable
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
    private readonly string root = Path.Combine(Path.GetTempPath(), "APRSCommandFirstRunTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DefaultConfiguration_IsIncompleteAndUsesSafeDefaults()
    {
        var configuration = FirstRunSetupConfiguration.CreateDefault(TestNow, root);

        Assert.False(configuration.FirstRunCompleted);
        Assert.False(configuration.TransmitEnabled);
        Assert.False(configuration.AprsIsTransmitEnabled);
        Assert.False(configuration.RfTransmitEnabled);
        Assert.False(configuration.IGateEnabled);
        Assert.False(configuration.DigipeaterEnabled);
        Assert.False(configuration.BeaconingEnabled);
        Assert.False(configuration.WeatherBeaconingEnabled);
        Assert.False(configuration.RestApiEnabled);
        Assert.False(configuration.WebSocketEnabled);
        Assert.False(configuration.FileHooksEnabled);
        Assert.False(configuration.PluginLoadingEnabled);
        Assert.False(configuration.StationProfileConfigured);
        Assert.Equal(TestNow, configuration.CreatedTimestampUtc);
        Assert.Equal(TestNow, configuration.UpdatedTimestampUtc);
    }

    [Fact]
    public void ApplicationFolderLayout_UsesPlatformPathHelpers()
    {
        var layout = ApplicationFolderLayout.FromRoot(root);

        Assert.All(layout.AllFolders, folder =>
        {
            Assert.StartsWith(Path.GetFullPath(root), folder, StringComparison.Ordinal);
            Assert.DoesNotContain(@"C:\APRS Command", folder, StringComparison.OrdinalIgnoreCase);
        });
        Assert.EndsWith(Path.Combine("file-hooks", "incoming"), layout.FileHooksIncomingFolderPath, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("file-hooks", "processed"), layout.FileHooksProcessedFolderPath, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("file-hooks", "rejected"), layout.FileHooksRejectedFolderPath, StringComparison.Ordinal);
    }

    [Fact]
    public void PrepareFolders_CreatesStandardApplicationFolders()
    {
        var configuration = FirstRunSetupConfiguration.CreateDefault(TestNow, root);
        var service = new ApplicationFolderSetupService();

        var result = service.PrepareFolders(configuration, createDefaultConfigurationFiles: false);

        Assert.True(result.Success);
        Assert.All(result.Layout.AllFolders, folder => Assert.True(Directory.Exists(folder), folder));
        Assert.Contains(result.Layout.ConfigFolderPath, result.CreatedFolders);
        Assert.Contains(result.Layout.MapCacheFolderPath, result.CreatedFolders);
        Assert.Contains(result.Layout.FileHooksIncomingFolderPath, result.CreatedFolders);
    }

    [Fact]
    public void PrepareFolders_CreatesSafePlaceholderConfigurationFiles()
    {
        var configuration = FirstRunSetupConfiguration.CreateDefault(TestNow, root);
        var service = new ApplicationFolderSetupService();

        var result = service.PrepareFolders(configuration);

        Assert.True(result.Success);
        Assert.NotEmpty(result.CreatedConfigurationFiles);
        Assert.Contains(result.CreatedConfigurationFiles, path => path.EndsWith("appsettings.safe-defaults.json", StringComparison.Ordinal));
        Assert.Contains(result.CreatedConfigurationFiles, path => path.EndsWith("safety.safe-defaults.json", StringComparison.Ordinal));
        var combined = string.Join(Environment.NewLine, result.CreatedConfigurationFiles.Select(File.ReadAllText));
        Assert.Contains("\"transmitEnabled\": false", combined);
        Assert.Contains("\"restApiEnabled\": false", combined);
        Assert.DoesNotContain("12345", combined);
        Assert.DoesNotContain("api_key", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("N0CALL", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MarkCompleted_UpdatesFirstRunStateAndTimestamp()
    {
        var configuration = FirstRunSetupConfiguration.CreateDefault(TestNow, root);
        var service = new FirstRunSetupService();
        var completedAt = TestNow.AddMinutes(5);

        var completed = service.MarkCompleted(configuration, completedAt);

        Assert.True(completed.FirstRunCompleted);
        Assert.True(completed.SafetySettingsReviewed);
        Assert.Equal(completedAt, completed.UpdatedTimestampUtc);
    }

    [Fact]
    public void FirstRunSetupViewModel_ExposesSafeDefaults()
    {
        var configuration = FirstRunSetupConfiguration.CreateDefault(TestNow, root);
        var viewModel = new FirstRunSetupViewModel(configuration);

        Assert.Equal("Welcome to APRS Command", viewModel.Title);
        Assert.True(viewModel.TransmitDisabled);
        Assert.True(viewModel.ExtensionInputsDisabled);
        Assert.Contains(viewModel.SafetyDefaults, item => item.Contains("does not transmit", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(root, viewModel.ApplicationDataFolderPath);
    }
}
