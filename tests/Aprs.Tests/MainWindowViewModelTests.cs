using System.Reflection;
using Aprs.Desktop;
using Aprs.Desktop.ViewModels;
using Xunit;

namespace Aprs.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void CreateDesignTime_InitializesAllViewModels()
    {
        var vm = MainWindowViewModel.CreateDesignTime();

        Assert.NotNull(vm.Map);
        Assert.NotNull(vm.StationList);
        Assert.NotNull(vm.RawPacketLog);
        Assert.NotNull(vm.FirstRunSetup);
        Assert.NotNull(vm.Connections);
        Assert.NotNull(vm.MessageCenter);
        Assert.NotNull(vm.ObjectManager);
        Assert.NotNull(vm.Weather);
        Assert.NotNull(vm.DecodedEventLog);
        Assert.NotNull(vm.EventMonitor);
        Assert.NotNull(vm.Replay);
        Assert.NotNull(vm.RfDiagnostics);
        Assert.NotNull(vm.Alerts);
    }

    [Fact]
    public void DesktopAssemblyMetadata_UsesAprsCommandDisplayName()
    {
        var assembly = typeof(App).Assembly;

        Assert.Equal("APRS Command", assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        Assert.Equal("APRS Command", assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
    }

    [Theory]
    [InlineData(nameof(MainWindowViewModel.OpenMessagesCommand),      nameof(MainWindowViewModel.MessagesRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenObjectsCommand),       nameof(MainWindowViewModel.ObjectsRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenWeatherCommand),       nameof(MainWindowViewModel.WeatherRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenEventsCommand),        nameof(MainWindowViewModel.EventsRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenEventBusCommand),      nameof(MainWindowViewModel.EventBusRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenReplayCommand),        nameof(MainWindowViewModel.ReplayRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenRfDiagnosticsCommand), nameof(MainWindowViewModel.RfDiagnosticsRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenAlertsCommand),        nameof(MainWindowViewModel.AlertsRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenStationListCommand),   nameof(MainWindowViewModel.StationListRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenRawPacketsCommand),    nameof(MainWindowViewModel.RawPacketsRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenSettingsCommand),      nameof(MainWindowViewModel.SettingsRequested))]
    [InlineData(nameof(MainWindowViewModel.OpenHelpCommand),          nameof(MainWindowViewModel.HelpRequested))]
    public void FeatureCommands_RaiseMatchingWindowEvent(string commandProp, string eventName)
    {
        var vm = MainWindowViewModel.CreateDesignTime();
        var type = typeof(MainWindowViewModel);
        var command = Assert.IsType<DesktopCommand>(type.GetProperty(commandProp)!.GetValue(vm));

        var raised = 0;
        var eventInfo = type.GetEvent(eventName)!;
        EventHandler handler = (_, _) => raised++;
        eventInfo.AddEventHandler(vm, handler);

        command.Execute(null);

        Assert.Equal(1, raised);
        eventInfo.RemoveEventHandler(vm, handler);
    }

    [Fact]
    public void OpenHelpCommand_RaisesHelpRequested()
    {
        var vm = MainWindowViewModel.CreateDesignTime();
        var raised = 0;
        vm.HelpRequested += (_, _) => raised++;

        vm.OpenHelpCommand.Execute(null);

        Assert.Equal(1, raised);
    }

    [Fact]
    public void MainWindowXaml_UsesSingleFeatureNavigationSurface()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Aprs.Desktop", "Views", "MainWindow.axaml"));

        // Old panel-based navigation must not exist.
        Assert.DoesNotContain("<WrapPanel>", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabControl Grid.Column=\"1\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<UniformGrid Grid.Row=\"1\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Name=\"FeaturePanel\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedFeatureContent", xaml, StringComparison.Ordinal);

        // Menu bar with all commands.
        Assert.Contains("<Menu", xaml, StringComparison.Ordinal);
        foreach (var command in new[]
        {
            "OpenSettingsCommand", "OpenMessagesCommand", "OpenObjectsCommand",
            "OpenWeatherCommand", "OpenEventsCommand", "OpenEventBusCommand",
            "OpenReplayCommand", "OpenRfDiagnosticsCommand", "OpenAlertsCommand",
            "OpenStationListCommand", "OpenRawPacketsCommand", "OpenHelpCommand"
        })
        {
            Assert.Equal(1, Count(xaml, $"Command=\"{{Binding {command}}}\""));
        }

        // Icon sidebar.
        Assert.Contains("ToolTip.Tip=\"Home / Reset view\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"Beacon Now", xaml, StringComparison.Ordinal);

        // Map fills the main area.
        Assert.Contains("<views:MapView", xaml, StringComparison.Ordinal);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrossPlatformAprs.sln")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }

    private static int Count(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
