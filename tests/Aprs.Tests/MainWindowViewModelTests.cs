using System.Reflection;
using Aprs.Desktop;
using Aprs.Desktop.ViewModels;
using Xunit;

namespace Aprs.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void CreateDesignTime_InitializesMapFirstShellViewModels()
    {
        var viewModel = MainWindowViewModel.CreateDesignTime();

        Assert.NotNull(viewModel.Map);
        Assert.NotNull(viewModel.StationList);
        Assert.NotNull(viewModel.RawPacketLog);
        Assert.NotNull(viewModel.FirstRunSetup);
        Assert.Equal(MainFeaturePanel.Messages, viewModel.SelectedFeature);
        Assert.Equal(0, viewModel.SelectedFeatureIndex);
        Assert.Equal("Messages", viewModel.SelectedFeatureName);
        Assert.NotNull(viewModel.SelectedFeatureDescription);
        Assert.Same(viewModel.MessageCenter, viewModel.SelectedFeatureContent);
    }

    [Fact]
    public void CreateDesignTime_KeepsFeaturePanelViewModelsAvailable()
    {
        var viewModel = MainWindowViewModel.CreateDesignTime();

        Assert.NotNull(viewModel.MessageCenter);
        Assert.NotNull(viewModel.ObjectManager);
        Assert.NotNull(viewModel.Weather);
        Assert.NotNull(viewModel.DecodedEventLog);
        Assert.NotNull(viewModel.EventMonitor);
        Assert.NotNull(viewModel.Replay);
        Assert.NotNull(viewModel.RfDiagnostics);
        Assert.NotNull(viewModel.Alerts);
    }

    [Fact]
    public void DesktopAssemblyMetadata_UsesAprsCommandDisplayName()
    {
        var assembly = typeof(App).Assembly;

        Assert.Equal("APRS Command", assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title);
        Assert.Equal("APRS Command", assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
    }

    [Theory]
    [InlineData(nameof(MainWindowViewModel.OpenMessagesCommand), MainFeaturePanel.Messages)]
    [InlineData(nameof(MainWindowViewModel.OpenObjectsCommand), MainFeaturePanel.Objects)]
    [InlineData(nameof(MainWindowViewModel.OpenWeatherCommand), MainFeaturePanel.Weather)]
    [InlineData(nameof(MainWindowViewModel.OpenEventsCommand), MainFeaturePanel.Events)]
    [InlineData(nameof(MainWindowViewModel.OpenEventBusCommand), MainFeaturePanel.EventBus)]
    [InlineData(nameof(MainWindowViewModel.OpenReplayCommand), MainFeaturePanel.Replay)]
    [InlineData(nameof(MainWindowViewModel.OpenRfDiagnosticsCommand), MainFeaturePanel.RfDiagnostics)]
    [InlineData(nameof(MainWindowViewModel.OpenAlertsCommand), MainFeaturePanel.Alerts)]
    public void FeatureCommandsSelectMatchingFeaturePanel(string commandPropertyName, MainFeaturePanel expectedPanel)
    {
        var viewModel = MainWindowViewModel.CreateDesignTime();
        var command = Assert.IsType<DesktopCommand>(typeof(MainWindowViewModel)
            .GetProperty(commandPropertyName)!
            .GetValue(viewModel));

        command.Execute(null);

        Assert.Equal(expectedPanel, viewModel.SelectedFeature);
        Assert.Equal((int)expectedPanel, viewModel.SelectedFeatureIndex);
        Assert.NotNull(viewModel.SelectedFeatureDescription);
        Assert.NotNull(viewModel.SelectedFeatureContent);
    }

    [Theory]
    [InlineData(nameof(MainWindowViewModel.OpenMessagesCommand), "Messages", typeof(MessageCenterViewModel))]
    [InlineData(nameof(MainWindowViewModel.OpenObjectsCommand), "Objects", typeof(ObjectManagerViewModel))]
    [InlineData(nameof(MainWindowViewModel.OpenWeatherCommand), "Weather", typeof(WeatherViewModel))]
    [InlineData(nameof(MainWindowViewModel.OpenEventsCommand), "Events", typeof(DecodedEventLogViewModel))]
    [InlineData(nameof(MainWindowViewModel.OpenEventBusCommand), "Event Bus", typeof(EventMonitorViewModel))]
    [InlineData(nameof(MainWindowViewModel.OpenReplayCommand), "Replay", typeof(ReplayViewModel))]
    [InlineData(nameof(MainWindowViewModel.OpenRfDiagnosticsCommand), "RF Diagnostics", typeof(RfDiagnosticsViewModel))]
    [InlineData(nameof(MainWindowViewModel.OpenAlertsCommand), "Alerts", typeof(AlertRulesViewModel))]
    public void FeatureCommandsExposeVisiblePanelState(string commandPropertyName, string expectedName, Type expectedContentType)
    {
        var viewModel = MainWindowViewModel.CreateDesignTime();
        var changedProperties = new List<string>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };
        var command = Assert.IsType<DesktopCommand>(typeof(MainWindowViewModel)
            .GetProperty(commandPropertyName)!
            .GetValue(viewModel));

        command.Execute(null);

        Assert.Equal(expectedName, viewModel.SelectedFeatureName);
        Assert.NotEmpty(viewModel.SelectedFeatureDescription);
        Assert.IsType(expectedContentType, viewModel.SelectedFeatureContent);
        if (commandPropertyName != nameof(MainWindowViewModel.OpenMessagesCommand))
        {
            Assert.Contains(nameof(MainWindowViewModel.SelectedFeatureContent), changedProperties);
            Assert.Contains(nameof(MainWindowViewModel.SelectedFeatureDescription), changedProperties);
        }
    }

    [Fact]
    public void SelectedFeatureIndexUpdatesSelectedFeature()
    {
        var viewModel = MainWindowViewModel.CreateDesignTime();

        viewModel.SelectedFeatureIndex = (int)MainFeaturePanel.RfDiagnostics;

        Assert.Equal(MainFeaturePanel.RfDiagnostics, viewModel.SelectedFeature);
        Assert.Equal("RF Diagnostics", viewModel.SelectedFeatureName);
    }

    [Fact]
    public void InvalidSelectedFeatureIndexIsIgnored()
    {
        var viewModel = MainWindowViewModel.CreateDesignTime();

        viewModel.SelectedFeatureIndex = 999;

        Assert.Equal(MainFeaturePanel.Messages, viewModel.SelectedFeature);
        Assert.Equal(0, viewModel.SelectedFeatureIndex);
    }

    [Fact]
    public void OpenHelpCommandRaisesHelpRequested()
    {
        var viewModel = MainWindowViewModel.CreateDesignTime();
        var requestCount = 0;
        viewModel.HelpRequested += (_, _) => requestCount++;

        viewModel.OpenHelpCommand.Execute(null);

        Assert.Equal(1, requestCount);
    }

    [Fact]
    public void MainWindowXaml_UsesSingleFeatureNavigationSurface()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Aprs.Desktop", "Views", "MainWindow.axaml"));

        Assert.DoesNotContain("<WrapPanel>", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<TabControl Grid.Column=\"1\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<UniformGrid Grid.Row=\"1\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Name=\"FeaturePanel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Name=\"FeatureButtonGrid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("RowDefinitions=\"32,32\"", xaml, StringComparison.Ordinal);
        Assert.Contains("ColumnDefinitions=\"*,*,*,*\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding SelectedFeatureContent}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SelectedFeatureName}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SelectedFeatureDescription}\"", xaml, StringComparison.Ordinal);
        Assert.Equal(1, Count(xaml, "<Button Grid.Column=\"1\""));

        foreach (var command in new[]
        {
            "OpenMessagesCommand",
            "OpenObjectsCommand",
            "OpenWeatherCommand",
            "OpenEventsCommand",
            "OpenEventBusCommand",
            "OpenReplayCommand",
            "OpenRfDiagnosticsCommand",
            "OpenAlertsCommand"
        })
        {
            Assert.Equal(1, Count(xaml, $"Command=\"{{Binding {command}}}\""));
        }
    }

    [Fact]
    public void MainWindowXaml_ContainsCompleteLowerRightFeatureButtonSet()
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Aprs.Desktop", "Views", "MainWindow.axaml"));
        var featureLabels = new[]
        {
            "Messages",
            "Objects",
            "Weather",
            "Events",
            "Event Bus",
            "Replay",
            "RF Diag",
            "Alerts"
        };

        foreach (var label in featureLabels)
        {
            Assert.Equal(1, Count(xaml, $"Content=\"{label}\""));
        }

        Assert.Equal(8, Count(xaml, "HorizontalContentAlignment=\"Center\""));
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrossPlatformAprs.sln")))
            {
                directory = directory.Parent;
            }

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
