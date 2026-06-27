using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Mapping;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class MapViewModel : INotifyPropertyChanged
{
    private StationMarkerViewModel? selectedStation;
    private StationDetailsViewModel? selectedStationDetails;
    private ObjectMarkerViewModel? selectedObject;
    private WeatherStationMarkerViewModel? selectedWeather;
    private ObjectManagerViewModel? objectManager;
    private readonly IMapCoordinateConverter coordinateConverter;

    public MapViewModel(IEnumerable<StationMarker> markers)
        : this(markers, MapTileCacheConfiguration.Default, CreateDefaultProvider(), new PlaceholderMapCoordinateConverter())
    {
    }

    public MapViewModel(
        IEnumerable<StationMarker> markers,
        MapTileCacheConfiguration tileCacheConfiguration,
        MapTileProviderDefinition tileProvider)
        : this(markers, tileCacheConfiguration, tileProvider, new PlaceholderMapCoordinateConverter())
    {
    }

    public MapViewModel(
        IEnumerable<StationMarker> markers,
        MapTileCacheConfiguration tileCacheConfiguration,
        MapTileProviderDefinition tileProvider,
        IMapCoordinateConverter coordinateConverter)
    {
        Markers = new ObservableCollection<StationMarkerViewModel>(
            markers.Select(marker => new StationMarkerViewModel(marker)));
        ObjectMarkers = [];
        WeatherMarkers = [];
        TileCacheConfiguration = tileCacheConfiguration;
        TileProvider = tileProvider;
        this.coordinateConverter = coordinateConverter;
        BeginCreateObjectCommand    = new DesktopCommand(BeginCreateObjectPlacement);
        ClearObjectSelectionCommand = new DesktopCommand(ClearObjectSelection);
        HomeCommand                 = new DesktopCommand(() => NavigationRequested?.Invoke(this, MapNavigationRequest.Home));
        CentreOnStationCommand      = new DesktopCommand(() => NavigationRequested?.Invoke(this, MapNavigationRequest.CentreOnStation));
        FindStationCommand          = new DesktopCommand(() => FindStationRequested?.Invoke(this, EventArgs.Empty));
        ToggleMapLayerCommand       = new DesktopCommand(() => ToggleMapLayerRequested?.Invoke(this, EventArgs.Empty));
        MeasureDistanceCommand      = new DesktopCommand(() => MeasureDistanceRequested?.Invoke(this, EventArgs.Empty));
        AlertStatusCommand          = new DesktopCommand(() => AlertStatusRequested?.Invoke(this, EventArgs.Empty));
        ToggleTrailsCommand         = new DesktopCommand(() => ShowTrails = !ShowTrails);
        ToggleRadarCommand          = new DesktopCommand(() => ShowRadar = !ShowRadar);
    }

    /// <summary>Fired when a sidebar navigation button or station list click requests map navigation.</summary>
    public event EventHandler<MapNavigationRequest>? NavigationRequested;

    public event EventHandler? FindStationRequested;
    public event EventHandler? ToggleMapLayerRequested;
    public event EventHandler? MeasureDistanceRequested;
    public event EventHandler? AlertStatusRequested;

    private bool showTrails;
    public bool ShowTrails
    {
        get => showTrails;
        private set
        {
            if (showTrails != value)
            {
                showTrails = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrailsButtonTooltip));
            }
        }
    }

    public string TrailsButtonTooltip => ShowTrails ? "Station trails ON — click to hide" : "Station trails OFF — click to show";

    private bool showRadar;
    public bool ShowRadar
    {
        get => showRadar;
        private set
        {
            if (showRadar != value)
            {
                showRadar = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RadarButtonTooltip));
            }
        }
    }

    public string RadarButtonTooltip => ShowRadar ? "Radar overlay ON — click to hide" : "Radar overlay OFF — click to show";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<StationMarkerViewModel> Markers { get; }

    public ObservableCollection<ObjectMarkerViewModel> ObjectMarkers { get; }

    public ObservableCollection<WeatherStationMarkerViewModel> WeatherMarkers { get; }

    public MapTileCacheConfiguration TileCacheConfiguration { get; }

    public MapTileProviderDefinition TileProvider { get; }

    public bool TileCacheEnabled => TileCacheConfiguration.CacheEnabled;

    public bool InternetTileDownloadAllowed => TileCacheConfiguration.AllowInternetTileDownload && TileProvider.InternetDownloadAllowed;

    public StationMarkerViewModel? SelectedStation
    {
        get => selectedStation;
        private set
        {
            if (ReferenceEquals(selectedStation, value))
            {
                return;
            }

            selectedStation = value;
            selectedStationDetails = value is null
                ? null
                : new StationDetailsViewModel(value, DateTimeOffset.UtcNow);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedStationDetails));
            OnPropertyChanged(nameof(HasSelectedStation));
        }
    }

    public StationDetailsViewModel? SelectedStationDetails => selectedStationDetails;

    public bool HasSelectedStation => selectedStationDetails is not null;

    public int MarkerCount => Markers.Count;

    public int ObjectMarkerCount => ObjectMarkers.Count;

    public int WeatherMarkerCount => WeatherMarkers.Count;

    public int TotalMarkerCount => MarkerCount + ObjectMarkerCount + WeatherMarkerCount;

    public bool IsCreateObjectMode { get; private set; }

    public string ObjectPlacementStatus { get; private set; } = "Object placement inactive.";

    public ObjectMarkerViewModel? SelectedObject
    {
        get => selectedObject;
        private set
        {
            if (ReferenceEquals(selectedObject, value))
            {
                return;
            }

            selectedObject = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedObject));
        }
    }

    public bool HasSelectedObject => SelectedObject is not null;

    public WeatherStationMarkerViewModel? SelectedWeather
    {
        get => selectedWeather;
        private set
        {
            if (ReferenceEquals(selectedWeather, value))
            {
                return;
            }

            selectedWeather = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedWeather));
        }
    }

    public bool HasSelectedWeather => SelectedWeather is not null;

    public DesktopCommand BeginCreateObjectCommand { get; }

    public DesktopCommand ClearObjectSelectionCommand { get; }

    public DesktopCommand HomeCommand { get; }

    public DesktopCommand CentreOnStationCommand { get; }

    public DesktopCommand FindStationCommand { get; }

    public DesktopCommand ToggleMapLayerCommand { get; }

    public DesktopCommand MeasureDistanceCommand { get; }
    public DesktopCommand ToggleTrailsCommand { get; }
    public DesktopCommand ToggleRadarCommand { get; }

    public DesktopCommand AlertStatusCommand { get; }

    /// <summary>
    /// Requests the MapView to centre on specific coordinates. Called by the station list
    /// when the operator clicks a callsign, so the map jumps to that station.
    /// </summary>
    public void RequestCentreOnPosition(double latitude, double longitude)
        => NavigationRequested?.Invoke(this, new MapNavigationRequest(MapNavigationKind.CentreOnPosition, latitude, longitude));

    public void SelectStation(StationMarkerViewModel marker)
    {
        if (Markers.Contains(marker))
        {
            SelectedStation = marker;
            SelectedObject = null;
            SelectedWeather = null;
        }
    }

    public void LoadWeatherStations(IEnumerable<WeatherStationDisplayRecord> stations)
    {
        WeatherMarkers.Clear();
        foreach (var marker in stations
            .Select(station => WeatherStationMarker.TryCreate(station, out var marker) ? marker : null)
            .OfType<WeatherStationMarker>())
        {
            WeatherMarkers.Add(new WeatherStationMarkerViewModel(marker));
        }

        if (SelectedWeather is not null && WeatherMarkers.FirstOrDefault(marker => string.Equals(marker.StationId, SelectedWeather.StationId, StringComparison.OrdinalIgnoreCase)) is { } refreshed)
        {
            SelectedWeather = refreshed;
        }

        OnPropertyChanged(nameof(WeatherMarkers));
        OnPropertyChanged(nameof(WeatherMarkerCount));
        OnPropertyChanged(nameof(TotalMarkerCount));
    }

    public void AttachObjectManager(ObjectManagerViewModel manager)
    {
        objectManager = manager;
        RefreshObjectMarkers();
    }

    public void RefreshObjectMarkers()
    {
        ObjectMarkers.Clear();
        if (objectManager is not null)
        {
            foreach (var marker in objectManager.GetObjectStates()
                .Select(state => ObjectMarker.TryCreate(state, out var marker) ? marker : null)
                .OfType<ObjectMarker>())
            {
                ObjectMarkers.Add(new ObjectMarkerViewModel(marker));
            }
        }

        if (SelectedObject is not null && ObjectMarkers.FirstOrDefault(marker => string.Equals(marker.ObjectName, SelectedObject.ObjectName, StringComparison.OrdinalIgnoreCase)) is { } refreshed)
        {
            SelectedObject = refreshed;
        }

        OnPropertyChanged(nameof(ObjectMarkers));
        OnPropertyChanged(nameof(ObjectMarkerCount));
        OnPropertyChanged(nameof(TotalMarkerCount));
    }

    public void SelectObject(ObjectMarkerViewModel marker)
    {
        if (!ObjectMarkers.Contains(marker))
        {
            return;
        }

        SelectedStation = null;
        SelectedWeather = null;
        SelectedObject = marker;
        objectManager?.SelectObjectByName(marker.ObjectName);
        ObjectPlacementStatus = $"Selected object {marker.ObjectName}.";
        OnPropertyChanged(nameof(ObjectPlacementStatus));
    }

    public void ClearObjectSelection()
    {
        SelectedObject = null;
        SelectedWeather = null;
        ObjectPlacementStatus = "Object selection cleared.";
        OnPropertyChanged(nameof(ObjectPlacementStatus));
    }

    public void SelectWeather(WeatherStationMarkerViewModel marker)
    {
        if (!WeatherMarkers.Contains(marker))
        {
            return;
        }

        SelectedStation = null;
        SelectedObject = null;
        SelectedWeather = marker;
        ObjectPlacementStatus = $"Selected weather station {marker.DisplayName}.";
        OnPropertyChanged(nameof(ObjectPlacementStatus));
    }

    public void BeginCreateObjectPlacement()
    {
        IsCreateObjectMode = true;
        SelectedObject = null;
        ObjectPlacementStatus = "Click the map to place a new local object draft.";
        OnPropertyChanged(nameof(IsCreateObjectMode));
        OnPropertyChanged(nameof(ObjectPlacementStatus));
    }

    public bool HandleMapClick(double xPercent, double yPercent)
    {
        var coordinate = coordinateConverter.FromNormalizedPoint(xPercent, yPercent);
        if (IsCreateObjectMode)
        {
            return PlaceObjectAt(coordinate.Latitude, coordinate.Longitude);
        }

        if (SelectedObject is not null)
        {
            return MoveSelectedObjectTo(coordinate.Latitude, coordinate.Longitude);
        }

        ObjectPlacementStatus = $"Map coordinate {coordinate.Latitude:0.0000}, {coordinate.Longitude:0.0000}.";
        OnPropertyChanged(nameof(ObjectPlacementStatus));
        return false;
    }

    public bool PlaceObjectAt(double latitude, double longitude)
    {
        if (objectManager is null)
        {
            ObjectPlacementStatus = "Object editor is not connected.";
            OnPropertyChanged(nameof(ObjectPlacementStatus));
            return false;
        }

        objectManager.CreateObjectDraftAt(latitude, longitude);
        IsCreateObjectMode = false;
        ObjectPlacementStatus = $"Draft object coordinates set to {latitude:0.0000}, {longitude:0.0000}.";
        OnPropertyChanged(nameof(IsCreateObjectMode));
        OnPropertyChanged(nameof(ObjectPlacementStatus));
        return true;
    }

    public bool MoveSelectedObjectTo(double latitude, double longitude, bool adoptIfRemote = false)
    {
        if (objectManager is null || SelectedObject is null)
        {
            ObjectPlacementStatus = "Select an object before moving it.";
            OnPropertyChanged(nameof(ObjectPlacementStatus));
            return false;
        }

        var moved = objectManager.MoveObjectTo(SelectedObject.ObjectName, latitude, longitude, adoptIfRemote);
        ObjectPlacementStatus = moved
            ? $"Moved {SelectedObject.ObjectName} to {latitude:0.0000}, {longitude:0.0000}."
            : objectManager.StatusText;
        RefreshObjectMarkers();
        OnPropertyChanged(nameof(ObjectPlacementStatus));
        return moved;
    }

    public void ClearSelection()
    {
        SelectedStation = null;
        SelectedWeather = null;
        ClearObjectSelection();
    }

    /// <summary>
    /// Replaces the current station markers with the supplied snapshots. Called by the live
    /// data coordinator on the UI thread after new packets are ingested. Snapshots that cannot
    /// be turned into a marker (e.g. no position yet) are skipped.
    /// </summary>
    public void UpdateStations(IEnumerable<StationSnapshot> stations)
    {
        var markers = stations
            .Select(station => StationMarker.TryCreate(station, out var marker) ? marker : null)
            .OfType<StationMarker>()
            .ToList();

        Markers.Clear();
        foreach (var marker in markers)
        {
            Markers.Add(new StationMarkerViewModel(marker));
        }
    }

    public static MapViewModel FromStations(IEnumerable<StationSnapshot> stations)
    {
        var markers = stations
            .Select(station => StationMarker.TryCreate(station, out var marker) ? marker : null)
            .OfType<StationMarker>();

        return new MapViewModel(markers);
    }

    public static MapViewModel CreateDesignTime()
    {
        var now = DateTimeOffset.UtcNow;
        var viewModel = new MapViewModel(
        [
            StationMarker.Create(
                "N0CALL",
                "Net Control",
                39.0583,
                -84.5083,
                '/',
                '-',
                now.AddMinutes(-8),
                StationLifecycleState.Active,
                AprsPacketSource.Simulation,
                CourseDegrees: null,
                SpeedKnots: null,
                altitudeFeet: 820,
                lastPath: ["TCPIP*"],
                comment: "Net control station online",
                lastRawPacket: "N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Net control station online",
                packetCount: 4),
            StationMarker.Create(
                "W1AW-9",
                "W1AW-9",
                41.3908,
                -72.6819,
                '/',
                '>',
                now.AddMinutes(-22),
                StationLifecycleState.Active,
                AprsPacketSource.Simulation,
                CourseDegrees: 123,
                SpeedKnots: 45,
                altitudeFeet: 510,
                lastPath: ["WIDE1-1", "WIDE2-1"],
                comment: "Mobile test",
                lastRawPacket: "W1AW-9>APRS,WIDE1-1,WIDE2-1:=4123.45N/07234.56W>Mobile test",
                packetCount: 2),
            StationMarker.Create(
                "WX9XYZ",
                "Weather WX9XYZ",
                38.6270,
                -90.1994,
                '/',
                '_',
                now.AddMinutes(-74),
                StationLifecycleState.Stale,
                AprsPacketSource.Simulation,
                CourseDegrees: null,
                SpeedKnots: null,
                altitudeFeet: null,
                lastPath: ["TCPIP*"],
                comment: "Weather station",
                lastRawPacket: "WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132",
                packetCount: 7)
        ]);

        viewModel.LoadWeatherStations([
            new WeatherStationDisplayRecord(
                "WX9XYZ",
                "Weather WX9XYZ",
                WeatherStationSourceType.AprsWeatherStation,
                38.6270,
                -90.1994,
                180,
                5,
                10,
                72,
                0,
                0,
                0,
                50,
                1013.2,
                null,
                null,
                null,
                null,
                now.AddMinutes(-12),
                TimeSpan.FromMinutes(12),
                WeatherDataState.Current,
                "WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132",
                WeatherStationOrigin.Simulation)
        ]);

        return viewModel;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static MapTileProviderDefinition CreateDefaultProvider()
    {
        return new MapTileProviderDefinition(
            Name: "SampleGrid",
            UrlTemplate: string.Empty,
            MinimumZoom: 0,
            MaximumZoom: 18,
            AttributionText: "Placeholder grid, no external map tiles loaded.",
            SupportsOfflineCaching: true,
            InternetDownloadAllowed: false);
    }
}
