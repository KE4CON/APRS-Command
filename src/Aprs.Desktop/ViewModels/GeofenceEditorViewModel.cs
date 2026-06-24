using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class GeofenceEditorViewModel : INotifyPropertyChanged
{
    private readonly IGeofenceService geofenceService;
    private GeofenceRowViewModel? selectedGeofence;
    private string name = "New circle";
    private string description = string.Empty;
    private bool enabled = true;
    private string centerLatitude = "39.05833";
    private string centerLongitude = "-84.50833";
    private string radiusMeters = "1609";
    private string polygonPointsText = "39.050,-84.520\n39.070,-84.520\n39.070,-84.490\n39.050,-84.490";
    private bool alertOnEnter = true;
    private bool alertOnExit = true;
    private string selectedType = nameof(GeofenceType.Circle);
    private string validationText = "Ready.";

    public GeofenceEditorViewModel(IGeofenceService geofenceService)
    {
        this.geofenceService = geofenceService;
        Geofences = new ObservableCollection<GeofenceRowViewModel>();
        TypeOptions = Enum.GetNames<GeofenceType>();
        CreateCircleCommand = new DesktopCommand(CreateCircle);
        CreatePolygonCommand = new DesktopCommand(CreatePolygon);
        SaveCommand = new DesktopCommand(Save);
        DeleteCommand = new DesktopCommand(DeleteSelected);
        ToggleEnabledCommand = new DesktopCommand(ToggleEnabled);
        SetMapCenterPlaceholderCommand = new DesktopCommand(SetMapCenterPlaceholder);
        AddPolygonPointPlaceholderCommand = new DesktopCommand(AddPolygonPointPlaceholder);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GeofenceRowViewModel> Geofences { get; }

    public IReadOnlyList<string> TypeOptions { get; }

    public DesktopCommand CreateCircleCommand { get; }

    public DesktopCommand CreatePolygonCommand { get; }

    public DesktopCommand SaveCommand { get; }

    public DesktopCommand DeleteCommand { get; }

    public DesktopCommand ToggleEnabledCommand { get; }

    public DesktopCommand SetMapCenterPlaceholderCommand { get; }

    public DesktopCommand AddPolygonPointPlaceholderCommand { get; }

    public GeofenceRowViewModel? SelectedGeofence
    {
        get => selectedGeofence;
        set
        {
            selectedGeofence = value;
            LoadSelected();
            OnPropertyChanged();
        }
    }

    public string Name
    {
        get => name;
        set => SetField(ref name, value);
    }

    public string Description
    {
        get => description;
        set => SetField(ref description, value);
    }

    public bool Enabled
    {
        get => enabled;
        set => SetField(ref enabled, value);
    }

    public string SelectedType
    {
        get => selectedType;
        set => SetField(ref selectedType, value);
    }

    public string CenterLatitude
    {
        get => centerLatitude;
        set => SetField(ref centerLatitude, value);
    }

    public string CenterLongitude
    {
        get => centerLongitude;
        set => SetField(ref centerLongitude, value);
    }

    public string RadiusMeters
    {
        get => radiusMeters;
        set => SetField(ref radiusMeters, value);
    }

    public string PolygonPointsText
    {
        get => polygonPointsText;
        set => SetField(ref polygonPointsText, value);
    }

    public bool AlertOnEnter
    {
        get => alertOnEnter;
        set => SetField(ref alertOnEnter, value);
    }

    public bool AlertOnExit
    {
        get => alertOnExit;
        set => SetField(ref alertOnExit, value);
    }

    public string ValidationText
    {
        get => validationText;
        private set => SetField(ref validationText, value);
    }

    public string CountText => $"{Geofences.Count} geofences";

    public void Refresh()
    {
        Geofences.Clear();
        foreach (var row in geofenceService.GetAllGeofences().Select(geofence => new GeofenceRowViewModel(geofence)))
        {
            Geofences.Add(row);
        }

        OnPropertyChanged(nameof(CountText));
    }

    public static GeofenceEditorViewModel CreateDesignTime()
    {
        var service = new GeofenceService();
        service.CreateGeofence(CreateDesignCircle("Net control area", 39.05833, -84.50833, 1609));
        service.CreateGeofence(CreateDesignPolygon());
        return new GeofenceEditorViewModel(service);
    }

    private void CreateCircle()
    {
        SelectedType = nameof(GeofenceType.Circle);
        Name = "New circle";
        CenterLatitude = "39.05833";
        CenterLongitude = "-84.50833";
        RadiusMeters = "1609";
        PolygonPointsText = string.Empty;
        selectedGeofence = null;
        OnPropertyChanged(nameof(SelectedGeofence));
        ValidationText = "Circle draft ready.";
    }

    private void CreatePolygon()
    {
        SelectedType = nameof(GeofenceType.Polygon);
        Name = "New polygon";
        PolygonPointsText = "39.050,-84.520\n39.070,-84.520\n39.070,-84.490\n39.050,-84.490";
        selectedGeofence = null;
        OnPropertyChanged(nameof(SelectedGeofence));
        ValidationText = "Polygon draft ready.";
    }

    private void Save()
    {
        var now = DateTimeOffset.UtcNow;
        var id = selectedGeofence?.GeofenceId ?? Guid.NewGuid();
        var existing = geofenceService.GetGeofence(id);
        var geofence = BuildDraft(id, existing?.CreatedAtUtc ?? now, now);
        var validation = geofenceService.ValidateGeofence(geofence);
        if (!validation.IsValid)
        {
            ValidationText = string.Join("; ", validation.Errors);
            return;
        }

        if (existing is null)
        {
            geofenceService.CreateGeofence(geofence);
        }
        else
        {
            geofenceService.UpdateGeofence(geofence);
        }

        ValidationText = validation.Warnings.Count == 0 ? "Saved." : $"Saved with warnings: {string.Join("; ", validation.Warnings)}";
        Refresh();
        SelectedGeofence = Geofences.FirstOrDefault(row => row.GeofenceId == id);
    }

    private void DeleteSelected()
    {
        if (selectedGeofence is null)
        {
            return;
        }

        geofenceService.DeleteGeofence(selectedGeofence.GeofenceId);
        selectedGeofence = null;
        OnPropertyChanged(nameof(SelectedGeofence));
        ValidationText = "Deleted.";
        Refresh();
    }

    private void ToggleEnabled()
    {
        if (selectedGeofence is null)
        {
            return;
        }

        var current = geofenceService.GetGeofence(selectedGeofence.GeofenceId);
        if (current is null)
        {
            return;
        }

        geofenceService.SetGeofenceEnabled(current.GeofenceId, !current.Enabled);
        ValidationText = current.Enabled ? "Disabled." : "Enabled.";
        Refresh();
    }

    private void SetMapCenterPlaceholder()
    {
        CenterLatitude = "39.05833";
        CenterLongitude = "-84.50833";
        ValidationText = "Map click placeholder set the circle center.";
    }

    private void AddPolygonPointPlaceholder()
    {
        PolygonPointsText = string.IsNullOrWhiteSpace(PolygonPointsText)
            ? "39.05833,-84.50833"
            : $"{PolygonPointsText.Trim()}\n39.05833,-84.50833";
        ValidationText = "Map click placeholder added a polygon point.";
    }

    private void LoadSelected()
    {
        if (selectedGeofence is null)
        {
            return;
        }

        var geofence = geofenceService.GetGeofence(selectedGeofence.GeofenceId);
        if (geofence is null)
        {
            return;
        }

        Name = geofence.Name;
        Description = geofence.Description ?? string.Empty;
        Enabled = geofence.Enabled;
        SelectedType = geofence.GeofenceType.ToString();
        CenterLatitude = geofence.CenterLatitude?.ToString("0.#####", CultureInfo.InvariantCulture) ?? string.Empty;
        CenterLongitude = geofence.CenterLongitude?.ToString("0.#####", CultureInfo.InvariantCulture) ?? string.Empty;
        RadiusMeters = geofence.RadiusMeters?.ToString("0", CultureInfo.InvariantCulture) ?? string.Empty;
        PolygonPointsText = string.Join(Environment.NewLine, geofence.PolygonPoints.Select(point => $"{point.Latitude.ToString("0.#####", CultureInfo.InvariantCulture)},{point.Longitude.ToString("0.#####", CultureInfo.InvariantCulture)}"));
        AlertOnEnter = geofence.AlertOnEnter;
        AlertOnExit = geofence.AlertOnExit;
        ValidationText = geofence.ValidationErrors.Count == 0 ? "Selected." : string.Join("; ", geofence.ValidationErrors);
    }

    private GeofenceDefinition BuildDraft(Guid id, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        var type = Enum.TryParse<GeofenceType>(SelectedType, out var parsedType) ? parsedType : GeofenceType.Circle;
        double.TryParse(CenterLatitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat);
        double.TryParse(CenterLongitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon);
        double.TryParse(RadiusMeters, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius);

        return new GeofenceDefinition(
            id,
            Name,
            Description,
            Enabled,
            type,
            string.IsNullOrWhiteSpace(CenterLatitude) ? null : lat,
            string.IsNullOrWhiteSpace(CenterLongitude) ? null : lon,
            string.IsNullOrWhiteSpace(RadiusMeters) ? null : radius,
            ParsePolygonPoints(PolygonPointsText),
            createdAtUtc,
            updatedAtUtc,
            null,
            "#2563EB solid",
            AlertOnEnter,
            AlertOnExit,
            AlertSeverity.Warning,
            [],
            []);
    }

    private static IReadOnlyList<GeofencePoint> ParsePolygonPoints(string text)
    {
        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(',', StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .Select(parts => new GeofencePoint(
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static GeofenceDefinition CreateDesignCircle(string name, double latitude, double longitude, double radius)
    {
        var now = DateTimeOffset.UtcNow;
        return new GeofenceDefinition(Guid.NewGuid(), name, "Sample circle geofence.", true, GeofenceType.Circle, latitude, longitude, radius, [], now, now, null, "#2563EB solid", true, true, AlertSeverity.Warning, [], []);
    }

    private static GeofenceDefinition CreateDesignPolygon()
    {
        var now = DateTimeOffset.UtcNow;
        return new GeofenceDefinition(
            Guid.NewGuid(),
            "Shelter zone",
            "Sample polygon geofence.",
            true,
            GeofenceType.Polygon,
            null,
            null,
            null,
            [new GeofencePoint(39.050, -84.520), new GeofencePoint(39.070, -84.520), new GeofencePoint(39.070, -84.490), new GeofencePoint(39.050, -84.490)],
            now,
            now,
            null,
            "#16A34A dashed",
            true,
            false,
            AlertSeverity.Advisory,
            [],
            []);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
