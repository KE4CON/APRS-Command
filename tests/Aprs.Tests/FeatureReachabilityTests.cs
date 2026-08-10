using System.Reflection;
using Aprs.Desktop.ViewModels;
using Aprs.Desktop.Views;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Guards the wiring that makes the five previously-orphaned features reachable from the menu. The XAML
/// compiler already verifies the menu's Click handlers and command bindings resolve (a dangling reference
/// is a build error); these reflection checks add belt-and-suspenders coverage for the contract the menu
/// handlers depend on, so a rename/removal that keeps compiling elsewhere still fails loudly here.
/// </summary>
public sealed class FeatureReachabilityTests
{
    [Theory]
    [InlineData(nameof(MainWindowViewModel.Geofences), typeof(GeofenceEditorViewModel))]
    [InlineData(nameof(MainWindowViewModel.Simulation), typeof(SimulationViewModel))]
    [InlineData(nameof(MainWindowViewModel.Training), typeof(TrainingModeViewModel))]
    [InlineData(nameof(MainWindowViewModel.DirewolfProfile), typeof(DirewolfProfileViewModel))]
    [InlineData(nameof(MainWindowViewModel.FileHooks), typeof(FileHooksViewModel))]
    public void MainWindowViewModel_ExposesFeatureViewModel(string propertyName, System.Type expectedType)
    {
        var property = typeof(MainWindowViewModel).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.Equal(expectedType, property!.PropertyType);
    }

    [Theory]
    [InlineData("OpenGeofences_Click")]
    [InlineData("OpenSimulation_Click")]
    [InlineData("OpenTrainingMode_Click")]
    [InlineData("OpenDirewolfProfile_Click")]
    [InlineData("OpenFileHooks_Click")]
    public void MainWindow_DeclaresMenuHandler(string handlerName)
    {
        var handler = typeof(MainWindow).GetMethod(handlerName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(handler);
    }

    [Fact]
    public void MapViewModel_ExposesZoomCommands()
    {
        Assert.NotNull(typeof(MapViewModel).GetProperty(nameof(MapViewModel.ZoomInCommand)));
        Assert.NotNull(typeof(MapViewModel).GetProperty(nameof(MapViewModel.ZoomOutCommand)));
    }

    [Theory]
    [InlineData(typeof(GeofenceEditorWindow))]
    [InlineData(typeof(SimulationWindow))]
    [InlineData(typeof(TrainingModeWindow))]
    [InlineData(typeof(DirewolfProfileWindow))]
    [InlineData(typeof(FileHooksWindow))]
    public void FeatureHostWindow_Exists(System.Type windowType)
    {
        Assert.True(typeof(Avalonia.Controls.Window).IsAssignableFrom(windowType),
            $"{windowType.Name} should be an Avalonia Window that hosts its feature view.");
    }
}
