using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Mapping;
using Aprs.Services;
using Aprs.Desktop.Mapping;

namespace Aprs.Desktop.ViewModels;

public enum DrawMode { None, Line, Polygon, Circle, Text, Erase, Eyedropper, SelectText }

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
        ToggleRadarCommand             = new DesktopCommand(() => ShowRadar = !ShowRadar);
        // Toolbar tool buttons select their tool (they do not toggle off — Exit leaves draw mode).
        DrawLineCommand                = new DesktopCommand(() => DrawMode = DrawMode.Line);
        DrawPolygonCommand             = new DesktopCommand(() => DrawMode = DrawMode.Polygon);
        DrawCircleCommand              = new DesktopCommand(() => DrawMode = DrawMode.Circle);
        DrawEraseCommand               = new DesktopCommand(() => DrawMode = DrawMode.Erase);
        DrawTextCommand                = new DesktopCommand(() => DrawMode = DrawMode.Text);
        DrawEyedropperCommand          = new DesktopCommand(() => DrawMode = DrawMode.Eyedropper);
        DrawSelectTextCommand          = new DesktopCommand(() => DrawMode = DrawMode.SelectText);
        OpenDrawingToolbarCommand      = new DesktopCommand(() => DrawMode = lastDrawingTool);
        ToggleMeasurementsCommand      = new DesktopCommand(() => ShowMeasurements = !ShowMeasurements);
        UseImperialUnitsCommand        = new DesktopCommand(() => MeasurementsImperial = true);
        UseMetricUnitsCommand          = new DesktopCommand(() => MeasurementsImperial = false);
        ToggleSmallUnitsCommand        = new DesktopCommand(() => PreferSmallUnits = !PreferSmallUnits);
        SetDrawColorCommand            = new DesktopCommand(p => { if (p is string hex) SetActiveToolColorHex(hex); });
        SetCustomColorCommand          = new DesktopCommand(() => CustomColorRequested?.Invoke(this, EventArgs.Empty));
        SetFillStyleCommand            = new DesktopCommand(p => { if (p is string s && Enum.TryParse<DrawFillStyle>(s, out var fs)) CurrentToolFill = fs; });
        ExitDrawModeCommand            = new DesktopCommand(() => DrawMode = DrawMode.None);
        ClearDrawingsCommand           = new DesktopCommand(() => ClearDrawingsRequested?.Invoke(this, EventArgs.Empty));
        ImportGeoFileCommand           = new DesktopCommand(() => ImportGeoFileRequested?.Invoke(this, EventArgs.Empty));
        ExportGeoFileCommand           = new DesktopCommand(() => ExportGeoFileRequested?.Invoke(this, EventArgs.Empty));
        ClearCustomRingCenterCommand   = new DesktopCommand(() => CustomRingCenter = null);
        SetRingsHereCommand            = new DesktopCommand(SetRingsAtSelectedStation, () => SelectedStation?.Latitude is not null);
        
        ToggleRadarAnimationCommand    = new DesktopCommand(() => RadarAnimating = !RadarAnimating);
        RadarPreviousFrameCommand      = new DesktopCommand(() => RadarStepRequested?.Invoke(this, -1));
        RadarNextFrameCommand          = new DesktopCommand(() => RadarStepRequested?.Invoke(this, +1));
        ToggleRingsCommand          = new DesktopCommand(() => ShowRings = !ShowRings);
        AssignTacticalCommand       = new DesktopCommand(() => AssignTacticalRequested?.Invoke(this, SelectedStation));
    }

    /// <summary>Fired when a sidebar navigation button or station list click requests map navigation.</summary>
    public event EventHandler<MapNavigationRequest>? NavigationRequested;

    public event EventHandler? FindStationRequested;
    public event EventHandler? ToggleMapLayerRequested;
    public event EventHandler? MeasureDistanceRequested;
    public event EventHandler<StationMarkerViewModel?>? AssignTacticalRequested;
    public event EventHandler<int>? RadarStepRequested;
    public event EventHandler? ClearDrawingsRequested;
    public event EventHandler? ImportGeoFileRequested;
    public event EventHandler? ExportGeoFileRequested;
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
                OnPropertyChanged(nameof(RadarMenuHeaderText));
                OnPropertyChanged(nameof(RadarButtonBackground));
                OnPropertyChanged(nameof(ShowAnimationControls));
            }
        }
    }

    public string RadarButtonTooltip => ShowRadar ? "Radar overlay ON — click to hide" : "Radar overlay OFF — click to show";

    public string RadarMenuHeaderText => ShowRadar ? "Radar Overlay: ON 🌧" : "Radar Overlay: OFF 🌧";

    public Avalonia.Media.IBrush RadarButtonBackground => ShowRadar
        ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(51, 29, 78, 216)) // 20% opacity blue
        : Avalonia.Media.Brushes.Transparent;

    // ── Radar animation state ──────────────────────────────────────────

    private DrawMode drawMode = DrawMode.None;
    private string drawModeLabel = string.Empty;
    private bool radarAnimating;
    public DrawMode DrawMode
    {
        get => drawMode;
        set
        {
            if (drawMode != value)
            {
                drawMode = value;
                if (Array.IndexOf(DrawingTools, value) >= 0) lastDrawingTool = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDrawModeActive));
                OnPropertyChanged(nameof(DrawModeTooltip));
                RaiseToolStyleChanged();
                RaiseToolActiveChanged();
                DrawModeChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsDrawModeActive => drawMode != DrawMode.None;

    public string DrawModeTooltip => drawMode switch
    {
        DrawMode.Line    => "Drawing: Line — click to add points, double-click to finish",
        DrawMode.Polygon => "Drawing: Polygon — click each corner, double-click to close",
        DrawMode.Circle  => "Drawing: Circle — press the center and drag out to set the radius",
        DrawMode.Text    => "Text — press and drag on the map to size it, then type your label",
        DrawMode.Eyedropper => "Eyedropper — click the map to grab that color",
        DrawMode.SelectText => "Edit text — click a label to select it, then drag its handle to resize or drag the label to move it",
        DrawMode.Erase   => "Erase mode — click a shape to delete it",
        _                => "Draw tools — add lines, polygons, and circles to the map"
    };

    // ── Per-tool drawing style (color · fill · width) ───────────────────────
    // Each drawing tool remembers its own style; the on-map drawing toolbar edits the active tool's.
    private sealed class ToolStyle
    {
        public string ColorHex = "#FF0000";
        public DrawFillStyle Fill = DrawFillStyle.Solid;
        public double Width = 3.0;
    }

    private static readonly DrawMode[] DrawingTools = { DrawMode.Line, DrawMode.Polygon, DrawMode.Circle, DrawMode.Text };
    private DrawMode lastDrawingTool = DrawMode.Line;
    private readonly Dictionary<DrawMode, ToolStyle> toolStyles = new()
    {
        [DrawMode.Line]    = new ToolStyle { ColorHex = "#FF0000", Fill = DrawFillStyle.Solid, Width = 3.0 },
        [DrawMode.Polygon] = new ToolStyle { ColorHex = "#1E6FEB", Fill = DrawFillStyle.Solid, Width = 3.0 },
        [DrawMode.Circle]  = new ToolStyle { ColorHex = "#00C000", Fill = DrawFillStyle.None,  Width = 3.0 },
        [DrawMode.Text]    = new ToolStyle { ColorHex = "#111827", Fill = DrawFillStyle.Solid, Width = 3.0 },
    };

    // For utility modes (eyedropper/erase/select), the style belongs to the last drawing tool used.
    private DrawMode StyleMode => Array.IndexOf(DrawingTools, drawMode) >= 0 ? drawMode : lastDrawingTool;
    private ToolStyle ActiveStyle => toolStyles[StyleMode];

    // Read by the map view when creating a shape.
    public string ActiveColorHex => ActiveStyle.ColorHex;
    public DrawFillStyle ActiveFill => ActiveStyle.Fill;
    public double ActiveWidth => ActiveStyle.Width;

    // Bound (two-way) by the drawing toolbar — edits the active tool's style.
    public Avalonia.Media.Color CurrentToolColor
    {
        get { try { return Avalonia.Media.Color.Parse(ActiveStyle.ColorHex); } catch { return Avalonia.Media.Colors.Red; } }
        set
        {
            var hex = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
            if (ActiveStyle.ColorHex != hex) { ActiveStyle.ColorHex = hex; RaiseToolStyleChanged(); }
        }
    }
    public Avalonia.Media.IBrush CurrentToolColorBrush => new Avalonia.Media.SolidColorBrush(CurrentToolColor);

    public DrawFillStyle CurrentToolFill
    {
        get => ActiveStyle.Fill;
        set { if (ActiveStyle.Fill != value) { ActiveStyle.Fill = value; OnPropertyChanged(); } }
    }

    public double CurrentToolWidth
    {
        get => ActiveStyle.Width;
        set { if (Math.Abs(ActiveStyle.Width - value) > 1e-9) { ActiveStyle.Width = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentToolWidthValue)); } }
    }

    // NumericUpDown.Value is decimal?; expose a decimal wrapper so the two-way binding reliably writes back.
    public decimal? CurrentToolWidthValue
    {
        get => (decimal)ActiveStyle.Width;
        set { if (value is { } v) CurrentToolWidth = (double)v; }
    }

    /// <summary>Set the active tool's color from a hex string (menu swatch, eyedropper, custom dialog).</summary>
    public void SetActiveToolColorHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || ActiveStyle.ColorHex == hex) return;
        ActiveStyle.ColorHex = hex;
        RaiseToolStyleChanged();
    }

    private void RaiseToolStyleChanged()
    {
        OnPropertyChanged(nameof(CurrentToolColor));
        OnPropertyChanged(nameof(CurrentToolColorBrush));
        OnPropertyChanged(nameof(CurrentToolFill));
        OnPropertyChanged(nameof(CurrentToolWidth));
    }

    private void RaiseToolActiveChanged()
    {
        OnPropertyChanged(nameof(IsLineActive));
        OnPropertyChanged(nameof(IsPolygonActive));
        OnPropertyChanged(nameof(IsCircleActive));
        OnPropertyChanged(nameof(IsTextActive));
        OnPropertyChanged(nameof(IsEyedropperActive));
        OnPropertyChanged(nameof(IsSelectTextActive));
        OnPropertyChanged(nameof(IsEraseActive));
    }

    public bool IsLineActive       => drawMode == DrawMode.Line;
    public bool IsPolygonActive    => drawMode == DrawMode.Polygon;
    public bool IsCircleActive     => drawMode == DrawMode.Circle;
    public bool IsTextActive       => drawMode == DrawMode.Text;
    public bool IsEyedropperActive => drawMode == DrawMode.Eyedropper;
    public bool IsSelectTextActive => drawMode == DrawMode.SelectText;
    public bool IsEraseActive      => drawMode == DrawMode.Erase;

    // Last-used map-text size; the text dialog defaults to this and updates it.
    private double currentTextSize = 14.0;
    public double CurrentTextSize
    {
        get => currentTextSize;
        set { if (currentTextSize != value) { currentTextSize = value; OnPropertyChanged(); } }
    }

    // Last-used text background (hex, or null for none).
    private string? currentTextBackground = "#FFFFFF";
    public string? CurrentTextBackground
    {
        get => currentTextBackground;
        set { if (currentTextBackground != value) { currentTextBackground = value; OnPropertyChanged(); } }
    }

    // Shape measurements (length / diameter / area) shown as labels on drawn shapes.
    private bool showMeasurements = true;
    public bool ShowMeasurements
    {
        get => showMeasurements;
        set { if (showMeasurements != value) { showMeasurements = value; OnPropertyChanged(); OnPropertyChanged(nameof(MeasurementsMenuHeader)); } }
    }

    private bool measurementsImperial = true;
    public bool MeasurementsImperial
    {
        get => measurementsImperial;
        set { if (measurementsImperial != value) { measurementsImperial = value; OnPropertyChanged(); } }
    }

    // When true, keep the smaller unit (ft/acres, m/ha) even for large shapes instead of auto-scaling up.
    private bool preferSmallUnits;
    public bool PreferSmallUnits
    {
        get => preferSmallUnits;
        set { if (preferSmallUnits != value) { preferSmallUnits = value; OnPropertyChanged(); OnPropertyChanged(nameof(SmallUnitsMenuHeader)); } }
    }

    public string MeasurementsMenuHeader => ShowMeasurements ? "Hide shape measurements" : "Show shape measurements";
    public string SmallUnitsMenuHeader => PreferSmallUnits
        ? "Auto-scale units (mi · sq mi · km²)"
        : "Keep small units (ft · acres · m · ha)";

    public event EventHandler<DrawMode>? DrawModeChanged;

    public DesktopCommand DrawLineCommand { get; }
    public DesktopCommand DrawPolygonCommand { get; }
    public DesktopCommand DrawCircleCommand { get; }
    public DesktopCommand DrawEraseCommand { get; }
    public DesktopCommand DrawTextCommand { get; }
    public DesktopCommand DrawEyedropperCommand { get; }
    public DesktopCommand DrawSelectTextCommand { get; }
    public DesktopCommand OpenDrawingToolbarCommand { get; }

    /// <summary>After the eyedropper samples a color, return to the last drawing tool (keep the toolbar open).</summary>
    public void ExitEyedropper() => DrawMode = lastDrawingTool;
    public DesktopCommand ToggleMeasurementsCommand { get; }
    public DesktopCommand UseImperialUnitsCommand { get; }
    public DesktopCommand UseMetricUnitsCommand { get; }
    public DesktopCommand ToggleSmallUnitsCommand { get; }
    public DesktopCommand SetDrawColorCommand { get; }
    public DesktopCommand SetCustomColorCommand { get; }
    public DesktopCommand SetFillStyleCommand { get; }

    /// <summary>Raised when the user asks for the custom color picker (handled by the map view).</summary>
    public event EventHandler? CustomColorRequested;
    public DesktopCommand ExitDrawModeCommand { get; }
    public DesktopCommand ClearDrawingsCommand { get; }
    public DesktopCommand ImportGeoFileCommand { get; }
    public DesktopCommand ExportGeoFileCommand { get; }

    public bool RadarAnimating
    {
        get => radarAnimating;
        set
        {
            if (radarAnimating != value)
            {
                radarAnimating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AnimationButtonLabel));
                OnPropertyChanged(nameof(ShowAnimationControls));
            }
        }
    }

    private int radarFrameIndex;
    public int RadarFrameIndex
    {
        get => radarFrameIndex;
        set
        {
            if (radarFrameIndex != value)
            {
                radarFrameIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RadarFrameLabel));
            }
        }
    }

    private int radarFrameCount;
    public int RadarFrameCount
    {
        get => radarFrameCount;
        set
        {
            if (radarFrameCount != value)
            {
                radarFrameCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RadarFrameLabel));
                OnPropertyChanged(nameof(ShowAnimationControls));
            }
        }
    }

    private string radarFrameTime = string.Empty;
    public string RadarFrameTime
    {
        get => radarFrameTime;
        set { if (radarFrameTime != value) { radarFrameTime = value; OnPropertyChanged(); } }
    }

    public bool ShowAnimationControls => ShowRadar && radarFrameCount > 0;
    public string AnimationButtonLabel => radarAnimating ? "⏸" : "▶";
    public string RadarFrameLabel => radarFrameCount > 0 ? $"{radarFrameIndex + 1} / {radarFrameCount}" : string.Empty;

    private bool showRings;
    private (double Lat, double Lon)? customRingCenter;
    public bool ShowRings
    {
        get => showRings;
        private set
        {
            if (showRings != value)
            {
                showRings = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RingsButtonTooltip));
            }
        }
    }

    public string RingsButtonTooltip => ShowRings ? "Range rings ON — click to hide" : "Range rings OFF — click to show";

    /// <summary>
    /// When set, range rings radiate from this point instead of the operator's station.
    /// Null = use station position (original behaviour).
    /// </summary>
    public (double Lat, double Lon)? CustomRingCenter
    {
        get => customRingCenter;
        set
        {
            if (customRingCenter != value)
            {
                customRingCenter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CustomRingCenterLabel));
                RingCenterChanged?.Invoke(this, value);
            }
        }
    }

    public string CustomRingCenterLabel => customRingCenter.HasValue
        ? $"Rings from {customRingCenter.Value.Lat:F4}°, {customRingCenter.Value.Lon:F4}°"
        : "Rings from my station";

    public event EventHandler<(double Lat, double Lon)?>? RingCenterChanged;

    public DesktopCommand ClearCustomRingCenterCommand { get; }
    public DesktopCommand SetRingsHereCommand { get; }

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
                : new StationDetailsViewModel(value, DateTimeOffset.UtcNow,
                    Configuration.StationProfile.Load().Latitude,
                    Configuration.StationProfile.Load().Longitude);
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
    public DesktopCommand ToggleRadarAnimationCommand { get; }
    public DesktopCommand RadarPreviousFrameCommand { get; }
    public DesktopCommand RadarNextFrameCommand { get; }
    public DesktopCommand ToggleRingsCommand { get; }
    public DesktopCommand AssignTacticalCommand { get; }

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


    private void SetRingsAtSelectedStation()
    {
        if (SelectedStation?.Latitude is { } lat && SelectedStation?.Longitude is { } lon)
            CustomRingCenter = (lat, lon);
    }
}