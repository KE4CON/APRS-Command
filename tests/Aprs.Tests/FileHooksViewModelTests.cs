using Aprs.Desktop.ViewModels;
using AprsCommand.Api;
using AprsCommand.Contracts;
using Xunit;

namespace Aprs.Tests;

public sealed class FileHooksViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"aprs-file-hooks-vm-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ViewModelExposesStatus()
    {
        var viewModel = new FileHooksViewModel(new FileHookService(Configuration()));

        Assert.True(viewModel.FileHooksEnabled);
        Assert.True(viewModel.ImportEnabled);
        Assert.True(viewModel.ExportEnabled);
        Assert.Equal(root, viewModel.BaseFolderPath);
    }

    [Fact]
    public void ManualExportCommandUpdatesStatus()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        provider.SeedStations(new StationUpdateDto { Callsign = "N0CALL" });
        var viewModel = new FileHooksViewModel(new FileHookService(Configuration(), provider));

        viewModel.ManualExportCommand.Execute(null);

        Assert.Contains("Export", viewModel.LastAction, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual("-", viewModel.LastExportTime);
        Assert.True(File.Exists(Path.Combine(root, "exports", "stations.json")));
    }

    [Fact]
    public void ManualImportScanCommandUpdatesAcceptedCount()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        var configuration = Configuration();
        var service = new FileHookService(configuration, provider);
        service.EnsureFolderStructure();
        File.WriteAllText(
            Path.Combine(configuration.ImportFolderPath, "stations", "station.json"),
            "{\"schemaVersion\":\"1.0\",\"callsign\":\"N0CALL\"}");
        var viewModel = new FileHooksViewModel(service);

        viewModel.ManualImportScanCommand.Execute(null);

        Assert.Equal(1, viewModel.AcceptedImportCount);
        Assert.Contains("Scanned", viewModel.LastAction);
        Assert.Single(provider.GetStations());
    }

    [Fact]
    public void ClearStatusCommandClearsCounts()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        var configuration = Configuration();
        var service = new FileHookService(configuration, provider);
        service.EnsureFolderStructure();
        File.WriteAllText(
            Path.Combine(configuration.ImportFolderPath, "stations", "station.json"),
            "{\"schemaVersion\":\"1.0\",\"callsign\":\"N0CALL\"}");
        var viewModel = new FileHooksViewModel(service);
        viewModel.ManualImportScanCommand.Execute(null);

        viewModel.ClearStatusCommand.Execute(null);

        Assert.Equal(0, viewModel.AcceptedImportCount);
        Assert.Equal("-", viewModel.LastImportTime);
        Assert.Equal("Status cleared.", viewModel.LastAction);
    }

    private FileHookConfiguration Configuration()
    {
        return FileHookConfiguration.Default with
        {
            FileHooksEnabled = true,
            ImportEnabled = true,
            ExportEnabled = true,
            BaseFolderPath = root,
            ImportFolderPath = Path.Combine(root, "incoming"),
            ExportFolderPath = Path.Combine(root, "exports"),
            AllowImportedStationData = true
        };
    }
}
