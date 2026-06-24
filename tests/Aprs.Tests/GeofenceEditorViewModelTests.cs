using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class GeofenceEditorViewModelTests
{
    [Fact]
    public void ViewModelExposesGeofencesAndValidation()
    {
        var service = new GeofenceService();
        service.CreateGeofence(new GeofenceDefinition(Guid.NewGuid(), "Circle", null, true, GeofenceType.Circle, 39, -84, 1000, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, true, true, AlertSeverity.Warning, [], []));

        var viewModel = new GeofenceEditorViewModel(service);

        Assert.Equal("1 geofences", viewModel.CountText);
        Assert.Single(viewModel.Geofences);
        Assert.Equal("Ready.", viewModel.ValidationText);
    }

    [Fact]
    public void ViewModelCanCreateValidCircle()
    {
        var viewModel = new GeofenceEditorViewModel(new GeofenceService())
        {
            Name = "Test circle",
            SelectedType = nameof(GeofenceType.Circle),
            CenterLatitude = "39.05833",
            CenterLongitude = "-84.50833",
            RadiusMeters = "1000"
        };

        viewModel.SaveCommand.Execute(null);

        var row = Assert.Single(viewModel.Geofences);
        Assert.Equal("Valid", row.Validation);
    }

    [Fact]
    public void ViewModelShowsValidationErrorsForInvalidCircle()
    {
        var viewModel = new GeofenceEditorViewModel(new GeofenceService())
        {
            Name = "Bad circle",
            SelectedType = nameof(GeofenceType.Circle),
            CenterLatitude = "39.05833",
            CenterLongitude = "-84.50833",
            RadiusMeters = "-10"
        };

        viewModel.SaveCommand.Execute(null);

        Assert.Contains("radius", viewModel.ValidationText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(viewModel.Geofences);
    }

    [Fact]
    public void PlaceholderMapCommandsUpdateDraftCoordinatesAndPolygonPoints()
    {
        var viewModel = new GeofenceEditorViewModel(new GeofenceService())
        {
            PolygonPointsText = string.Empty
        };

        viewModel.SetMapCenterPlaceholderCommand.Execute(null);
        viewModel.AddPolygonPointPlaceholderCommand.Execute(null);

        Assert.Equal("39.05833", viewModel.CenterLatitude);
        Assert.Equal("-84.50833", viewModel.CenterLongitude);
        Assert.Contains("39.05833,-84.50833", viewModel.PolygonPointsText);
    }
}
