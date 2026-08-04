using Avalonia.Controls;
using Aprs.Desktop.ViewModels;
using Aprs.Desktop.Runtime;
using Aprs.Desktop.Mapping;
using Aprs.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using NetTopologySuite.Geometries;
using Aprs.Desktop.Configuration;
using Aprs.Desktop.Services;
using BruTile;
using BruTile.Cache;
using BruTile.Predefined;
using BruTile.Web;

namespace Aprs.Desktop.Views;

public sealed partial class MapView : UserControl
{
    private MapViewModel? currentViewModel;
    private GenericCollectionLayer<List<IFeature>>? markerLayer;
    private WritableLayer? trailLayer;
    private WritableLayer? ringsLayer;
    private WritableLayer? drawingLayer;
    private readonly List<DrawingShape> completedShapes = [];
    private DrawingShape? shapeInProgress;
    private (double X, double Y)? drawHoverWorld;   // cursor position for the in-progress rubber-band preview
    private bool circleDragging;                    // true while press-dragging a circle radius
    private bool textDragging;                      // true while press-dragging a new text's size
    private (double X, double Y) textAnchor;        // world anchor of the text being drag-sized
    private double lastResolution;                  // last map resolution seen (to re-scale zoom-scaled text)
    private DrawingShape? selectedText;             // text selected for resize/move (SelectText mode)
    private bool resizingText;                      // dragging the selected text's resize handle
    private bool movingText;                        // dragging the selected text's body
    private (double X, double Y) moveGrabOffset;    // grab-point → anchor offset while moving
    private const double MinCircleRadiusMetres = 50.0;
    private DrawMode currentDrawMode = DrawMode.None;
    private TileLayer? radarLayer;           // single static layer (legacy, kept for compat)
    private Mapping.WmsRadarUrlBuilder? radarUrlBuilder;
    private readonly List<TileLayer> radarFrameLayers = [];                          // animation frames
    private readonly List<Mapping.WmsRadarUrlBuilder> radarFrameUrlBuilders = [];     // one builder per frame
    private Avalonia.Threading.DispatcherTimer? radarAnimTimer;
    private int currentFrameIndex;
    private ILayer? currentBaseLayer;
    // The USGS aerial-imagery layers have no tiles over large open water (e.g. the Great Lakes),
    // which would otherwise render as blank white when zoomed in. We fill those gaps with a solid,
    // muted water color set as the map background — close to the natural aerial-water tone so the
    // shoreline transition is soft rather than a hard bright-blue seam. Captured/restored so the
    // fill only applies while an imagery base map is active.
    private Mapsui.Styles.Color defaultBackColor = Mapsui.Styles.Color.White;
    private static readonly Mapsui.Styles.Color ImageryWaterFill = new(52, 91, 115); // #345B73
    private int baseMapIndex; // cycles through BaseMapKind values on toggle
    private StationTrailService? trailService; // set by WireTrailService()
    private bool mapInitialized;
    private bool hasFitToData;

    // Regional zoom level used for the initial home view.
    private const double HomeResolution = 611;

    public MapView()
    {
        InitializeComponent();
        Loaded += (_, _) => InitializeMap();
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void InitializeMap()
    {
        if (mapInitialized)
        {
            return;
        }

        mapInitialized = true;

        var map = MapControl.Map;
        defaultBackColor = map.BackColor;
        currentBaseLayer = CreateBaseLayer(BaseMapKind.OpenStreetMap);
        map.Layers.Add(currentBaseLayer);

        // Trail layer sits between the base map and APRS markers.
        trailLayer = new WritableLayer { Name = "Station trails" };
        map.Layers.Add(trailLayer);

        // Range rings layer — above trails, below radar.
        ringsLayer = new WritableLayer { Name = "Range rings" };
        map.Layers.Add(ringsLayer);

        // Draw tools layer — above rings, below radar and markers.
        drawingLayer = new WritableLayer { Name = "Draw tools", Style = null };
        map.Layers.Add(drawingLayer);

        // Radar layer — between trails and markers, initially hidden.
        radarUrlBuilder = new Mapping.WmsRadarUrlBuilder();
        var radarSchema = new BruTile.Predefined.GlobalSphericalMercator(0, 10) { Name = "NEXRAD Radar (latest)" };
        var radarHttpSource = new BruTile.Web.HttpTileSource(
            radarSchema, radarUrlBuilder, name: "NEXRAD Radar (latest)",
            attribution: new BruTile.Attribution("NOAA/NWS NEXRAD", "https://www.weather.gov/"));
        radarLayer = new TileLayer(radarHttpSource)
        {
            Name    = "NEXRAD Radar",
            Opacity = 0.65,
            Enabled = false  // off by default
        };
        map.Layers.Add(radarLayer);

        markerLayer = new GenericCollectionLayer<List<IFeature>>
        {
            Name = "APRS markers"
        };
        map.Layers.Add(markerLayer);

        map.Info += OnMapInfo;

        // Draw tools use raw pointer input (not Mapsui's discrete tap event) so we can
        // support press-drag gestures (circle radius) and suppress the map's own pan/zoom
        // while a tool is active. Tunnel routing lets us pre-empt Mapsui's built-in handling:
        // when a draw tool is active we mark the event handled, so the map never pans underneath.
        MapControl.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, OnDrawPointerPressed,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
        MapControl.AddHandler(Avalonia.Input.InputElement.PointerMovedEvent, OnDrawPointerMoved,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);
        MapControl.AddHandler(Avalonia.Input.InputElement.PointerReleasedEvent, OnDrawPointerReleased,
            Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // Zoom-scaled text (GroundHeightMetres > 0) must be re-rendered when the zoom changes.
        // Mapsui exposes no viewport-changed event here, so poll the resolution and redraw when it moves.
        var zoomWatch = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        zoomWatch.Tick += (_, _) =>
        {
            var res = MapControl.Map.Navigator.Viewport.Resolution;
            if (res <= 0 || Math.Abs(res - lastResolution) < 1e-9) return;
            lastResolution = res;
            if (completedShapes.Any(s => s.ShapeType == DrawShapeType.Text && s.GroundHeightMetres > 0)
                || shapeInProgress is { ShapeType: DrawShapeType.Text })
            {
                RedrawAllShapes();
            }
        };
        zoomWatch.Start();

        RefreshFeatures();

        // The APRS symbol sheets are referenced by markers but Mapsui only fetches
        // image sources during a Navigator fetch cycle (a viewport change). Marker
        // updates alone won't pull them in, so the sheets must be loaded into the
        // render service's image cache explicitly; otherwise every station renders as
        // Mapsui's placeholder circle instead of its symbol.
        _ = PreloadSymbolSheetsAsync();

        // Open the map centered on the operator's home (QTH) instead of drifting to
        // wherever the first received stations happen to be.
        CenterOnHome();
    }

    // Pull the embedded APRS symbol sheets into the map's image-source cache so the
    // ImageStyle renderer can crop and draw symbol regions on the first paint.
    private async System.Threading.Tasks.Task PreloadSymbolSheetsAsync()
    {
        try
        {
            // Constructing an Image with each sheet source registers it in the global
            // Image.SourceToSourceId map, which FetchAllImageDataAsync then loads.
            _ = new Mapsui.Styles.Image { Source = PrimarySheet };
            _ = new Mapsui.Styles.Image { Source = SecondarySheet };
            _ = new Mapsui.Styles.Image { Source = OverlaySheet };

            await MapControl.Map.RenderService.ImageSourceCache
                .FetchAllImageDataAsync(Mapsui.Styles.Image.SourceToSourceId)
                .ConfigureAwait(true);

            // Redraw now that the symbols are available.
            MapControl.RefreshGraphics();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Map] Failed to preload APRS symbol sheets: {ex.Message}");
        }
    }

    private enum BaseMapKind
    {
        OpenStreetMap,
        UsgsTopo,
        UsgsImagery,
        UsgsImageryTopo
    }

    // Swaps the base map while keeping the APRS markers layer on top. Each base map caches
    // tiles to its own folder, so switching back to one you've used is instant and works
    // offline for areas you've already viewed.
    private void SetBaseMap(BaseMapKind kind)
    {
        if (!mapInitialized)
        {
            return;
        }

        var map = MapControl.Map;
        if (currentBaseLayer is not null)
        {
            map.Layers.Remove(currentBaseLayer);
        }

        currentBaseLayer = CreateBaseLayer(kind);
        map.Layers.Add(currentBaseLayer);
        map.Layers.MoveToBottom(currentBaseLayer);

        // The USGS aerial-imagery layers leave open water blank (no tiles); fill those gaps with a
        // solid muted water color so lakes/oceans read as water instead of white. The vector maps
        // (OSM, USGS Topo) draw water themselves, so restore the normal background for them.
        map.BackColor = NeedsWaterFill(kind) ? ImageryWaterFill : defaultBackColor;
    }

    // The aerial-imagery basemaps (imagery, imagery+topo) are land-only and leave open water
    // blank at high zoom, so the map gets a water-colored background. The vector maps (OSM,
    // USGS Topo) draw water themselves and need none.
    private static bool NeedsWaterFill(BaseMapKind kind) =>
        kind is BaseMapKind.UsgsImagery or BaseMapKind.UsgsImageryTopo;

    private void BaseMapSelector_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        // This event fires once while the XAML is still loading (when the initial selection
        // is applied), before the map is initialized and before the named field is assigned.
        // Ignore it until the map is ready, and read the index from the sender so we never
        // touch a not-yet-assigned field.
        if (!mapInitialized || sender is not Avalonia.Controls.ComboBox comboBox)
        {
            return;
        }

        var kind = comboBox.SelectedIndex switch
        {
            1 => BaseMapKind.UsgsTopo,
            2 => BaseMapKind.UsgsImagery,
            3 => BaseMapKind.UsgsImageryTopo,
            _ => BaseMapKind.OpenStreetMap
        };
        SetBaseMap(kind);
    }

    // Auto-caching tile base layers. Tiles fetched while online are written to a per-map
    // on-disk cache and reused later (including offline). This only caches tiles actually
    // viewed and never pre-fetches, staying within each provider's usage policy. A
    // descriptive User-Agent is sent. OpenStreetMap is global; the USGS layers are US-only.
    private ILayer CreateBaseLayer(BaseMapKind kind)
    {
        // name, url template, max cached zoom, cache subfolder, attribution
        var (name, urlTemplate, maxZoom, cacheFolder, attributionText, attributionUrl) = kind switch
        {
            BaseMapKind.UsgsTopo => (
                "USGS Topo",
                "https://basemap.nationalmap.gov/arcgis/rest/services/USGSTopo/MapServer/tile/{z}/{y}/{x}",
                16, "usgs-topo", "USGS The National Map", "https://www.usgs.gov/"),
            BaseMapKind.UsgsImagery => (
                "USGS Imagery",
                "https://basemap.nationalmap.gov/arcgis/rest/services/USGSImageryOnly/MapServer/tile/{z}/{y}/{x}",
                16, "usgs-imagery", "USGS The National Map", "https://www.usgs.gov/"),
            BaseMapKind.UsgsImageryTopo => (
                "USGS Imagery + Topo",
                "https://basemap.nationalmap.gov/arcgis/rest/services/USGSImageryTopo/MapServer/tile/{z}/{y}/{x}",
                16, "usgs-imagerytopo", "USGS The National Map", "https://www.usgs.gov/"),
            _ => (
                "OpenStreetMap",
                "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                19, "osm", "© OpenStreetMap contributors", "https://www.openstreetmap.org/copyright")
        };

        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AprsCommand", "tile-cache", cacheFolder);
        Directory.CreateDirectory(cacheDirectory);

        var tileSource = new HttpTileSource(
            new GlobalSphericalMercator(0, maxZoom),
            urlTemplate,
            name: name,
            persistentCache: new FileCache(cacheDirectory, "png"),
            attribution: new Attribution(attributionText, attributionUrl),
            configureHttpRequestMessage: request => request.Headers.UserAgent.ParseAdd(
                "AprsCommand/1.0 (+https://github.com/KE4CON/CrossPlatformAPRS)"));

        return new TileLayer(tileSource) { Name = name };
    }

    private void AttachViewModel()
    {
        if (currentViewModel is not null)
        {
            currentViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            currentViewModel.Markers.CollectionChanged -= Markers_CollectionChanged;
            currentViewModel.NavigationRequested -= OnNavigationRequested;
            currentViewModel.FindStationRequested -= OnFindStationRequested;
            currentViewModel.ToggleMapLayerRequested -= OnToggleMapLayerRequested;
            currentViewModel.DrawModeChanged -= OnDrawModeChanged;
            currentViewModel.ClearDrawingsRequested -= OnClearDrawings;
            currentViewModel.ImportGeoFileRequested -= OnImportGeoFile;
            currentViewModel.ExportGeoFileRequested -= OnExportGeoFile;
            currentViewModel.CustomColorRequested -= OnCustomColorRequested;
            currentViewModel.RingCenterChanged -= OnRingCenterChanged;
        }

        currentViewModel = DataContext as MapViewModel;
        if (currentViewModel is not null)
        {
            currentViewModel.PropertyChanged += ViewModel_PropertyChanged;
            currentViewModel.Markers.CollectionChanged += Markers_CollectionChanged;
            currentViewModel.NavigationRequested += OnNavigationRequested;
            currentViewModel.FindStationRequested += OnFindStationRequested;
            currentViewModel.ToggleMapLayerRequested += OnToggleMapLayerRequested;
            currentViewModel.DrawModeChanged += OnDrawModeChanged;
            currentViewModel.ClearDrawingsRequested += OnClearDrawings;
            currentViewModel.ImportGeoFileRequested += OnImportGeoFile;
            currentViewModel.ExportGeoFileRequested += OnExportGeoFile;
            currentViewModel.CustomColorRequested += OnCustomColorRequested;
            currentViewModel.RingCenterChanged += OnRingCenterChanged;
        }

        UpdatePanels();
        RefreshFeatures();
    }

    private bool featuresRefreshQueued;

    private void Markers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // UpdateStations clears and re-adds every marker (N events per refresh). Coalesce them into
        // one feature rebuild per UI cycle so a burst (e.g. a 175-station replay load) doesn't
        // rebuild the whole Mapsui feature set N times and starve the rest of the UI.
        if (featuresRefreshQueued)
        {
            return;
        }

        featuresRefreshQueued = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () =>
            {
                featuresRefreshQueued = false;
                RefreshFeatures();
            },
            Avalonia.Threading.DispatcherPriority.Background);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapViewModel.SelectedStation)
            or nameof(MapViewModel.SelectedStationDetails)
            or nameof(MapViewModel.HasSelectedStation)
            or nameof(MapViewModel.SelectedObject)
            or nameof(MapViewModel.ObjectMarkers)
            or nameof(MapViewModel.ObjectMarkerCount)
            or nameof(MapViewModel.SelectedWeather)
            or nameof(MapViewModel.WeatherMarkers)
            or nameof(MapViewModel.WeatherMarkerCount))
        {
            UpdatePanels();
            RefreshFeatures();
        }

        // When the trails toggle changes, redraw immediately.
        if (e.PropertyName == nameof(MapViewModel.ShowTrails) && trailService is not null)
        {
            UpdateTrails(trailService);
        }

        // Measurement toggle / unit change — redraw the drawing layer to add/remove/reformat labels.
        if (e.PropertyName is nameof(MapViewModel.ShowMeasurements) or nameof(MapViewModel.MeasurementsImperial))
        {
            RedrawAllShapes();
        }

        // When radar toggle changes, enable/disable the layer.
        if (e.PropertyName == nameof(MapViewModel.ShowRadar) && radarLayer is not null)
        {
            var show = (DataContext as MapViewModel)?.ShowRadar ?? false;
            Console.Error.WriteLine($"[RadarDebug] ShowRadar changed to {show}. radarFrameLayers.Count={radarFrameLayers.Count}, radarLayer.Enabled(before)={radarLayer.Enabled}");

            if (radarFrameLayers.Count > 0)
            {
                // Animation frames are loaded — toggle the current frame instead of the
                // static layer, since the static layer is permanently disabled once
                // animation frames exist.
                radarLayer.Enabled = false;
                if (show)
                {
                    var idx = Math.Clamp(currentFrameIndex, 0, radarFrameLayers.Count - 1);
                    ShowFrame(idx);
                }
                else
                {
                    foreach (var fl in radarFrameLayers)
                        fl.Enabled = false;
                }
            }
            else
            {
                // No animation frames yet — show the static (current-time) radar layer.
                radarLayer.Enabled = show;
            }

            if (!show)
            {
                // Stop the animation timer without re-showing the latest frame —
                // StopAnimation() normally restores the latest frame for the
                // 'pause but keep radar visible' case, but here the operator has
                // turned radar off entirely, so everything must stay disabled.
                radarAnimTimer?.Stop();
                radarAnimTimer = null;
                if (DataContext is MapViewModel animVm) animVm.RadarAnimating = false;
            }
            MapControl.Map.RefreshData();
            MapControl.RefreshGraphics();
            Console.Error.WriteLine($"[RadarDebug] After toggle: radarLayer.Enabled={radarLayer.Enabled}, frame layers enabled=[{string.Join(",", radarFrameLayers.Select(l => l.Enabled))}], total map layers={MapControl.Map.Layers.Count}");
        }

        // When animation toggle changes, start or stop.
        if (e.PropertyName == nameof(MapViewModel.RadarAnimating))
        {
            if ((DataContext as MapViewModel)?.RadarAnimating == true)
                StartAnimation();
            else
                StopAnimation();
        }

        // When rings toggle changes, draw or clear.
        if (e.PropertyName == nameof(MapViewModel.ShowRings))
        {
            if ((DataContext as MapViewModel)?.ShowRings == true)
                DrawRings();
            else
                ClearRings();
        }
    }

    /// <summary>Draws range rings at the operator's configured distances around the station.</summary>
    private void DrawRings()
    {
        if (ringsLayer is null) return;
        ringsLayer.Clear();

        double lat, lon;

        // Use custom ring center if set, otherwise fall back to station position
        var vm = DataContext as MapViewModel;
        if (vm?.CustomRingCenter.HasValue == true)
        {
            lat = vm.CustomRingCenter.Value.Lat;
            lon = vm.CustomRingCenter.Value.Lon;
        }
        else
        {
            var profile = Configuration.StationProfile.Load();
            if (profile.Latitude == 0 && profile.Longitude == 0)
            {
                ringsLayer.DataHasChanged();
                return;
            }
            lat = profile.Latitude;
            lon = profile.Longitude;

            // Read configured ring distances — stored in miles, convert to meters.
            var d1 = profile.Ring1Distance;
            var d2 = profile.Ring2Distance;
            var d3 = profile.Ring3Distance;
            var unit = profile.DistanceUnit == Configuration.DistanceUnit.Kilometres ? "km" : "mi";
            var metersPerUnit = profile.DistanceUnit == Configuration.DistanceUnit.Kilometres
                ? 1000.0 : 1609.344;

            var ringDefs = new[]
            {
                (d1, "#60a5fa", $"{d1} {unit}"),
                (d2, "#34d399", $"{d2} {unit}"),
                (d3, "#f87171", $"{d3} {unit}"),
            };

            foreach (var (dist, color, _) in ringDefs)
            {
                var radiusMeters = dist * metersPerUnit;
                var circle       = CreateCircle(lat, lon, radiusMeters);
                if (circle is null) continue;

                var feature = new GeometryFeature(circle);
                feature.Styles.Add(new VectorStyle
                {
                    Line = new Pen(Mapsui.Styles.Color.FromString(color), 2.5f),
                    Fill = null,
                    Outline = null
                });
                ringsLayer.Add(feature);
            }

            ringsLayer.DataHasChanged();
            MapControl.RefreshGraphics();
            return;
        }

        // Custom ring center path — use profile ring distances
        var profileForCenter = Configuration.StationProfile.Load();
        var cd1 = profileForCenter.Ring1Distance;
        var cd2 = profileForCenter.Ring2Distance;
        var cd3 = profileForCenter.Ring3Distance;
        var cMetersPerUnit = profileForCenter.DistanceUnit == Configuration.DistanceUnit.Kilometres
            ? 1000.0 : 1609.344;

        foreach (var (dist, color) in new[] { (cd1, "#60a5fa"), (cd2, "#34d399"), (cd3, "#f87171") })
        {
            var radiusMeters = dist * cMetersPerUnit;
            var circle       = CreateCircle(lat, lon, radiusMeters);
            if (circle is null) continue;

            var feature = new GeometryFeature(circle);
            feature.Styles.Add(new VectorStyle
            {
                Line = new Pen(Mapsui.Styles.Color.FromString(color), 2.5f),
                Fill = null,
                Outline = null
            });
            ringsLayer.Add(feature);
        }

        ringsLayer.DataHasChanged();
        MapControl.RefreshGraphics();
    }

    private void OnRingCenterChanged(object? sender, (double Lat, double Lon)? center)
    {
        DrawRings();
    }

    /// <summary>
    /// Called from the map context menu or double-click handler to set the
    /// ring center to a specific world coordinate.
    /// </summary>
    public void SetRingCenterFromMap(double lat, double lon)
    {
        if (DataContext is MapViewModel vm)
        {
            vm.CustomRingCenter = (lat, lon);
            if (!vm.ShowRings)
                vm.ToggleRingsCommand.Execute(null);
        }
    }

    private void ClearRings()
    {
        if (ringsLayer is null) return;
        ringsLayer.Clear();
        ringsLayer.DataHasChanged();
        MapControl.RefreshGraphics();
    }

    /// <summary>Creates a circle polygon in Web Mercator (EPSG:3857) around a lat/lon point.</summary>
    private static NetTopologySuite.Geometries.Geometry? CreateCircle(
        double latDeg, double lonDeg, double radiusMeters)
    {
        try
        {
            const int segments = 64;
            var (cx, cy) = Mapsui.Projections.SphericalMercator.FromLonLat(lonDeg, latDeg);
            var coords = new NetTopologySuite.Geometries.Coordinate[segments + 1];
            for (int i = 0; i < segments; i++)
            {
                var angle = 2 * Math.PI * i / segments;
                coords[i] = new NetTopologySuite.Geometries.Coordinate(
                    cx + radiusMeters * Math.Cos(angle),
                    cy + radiusMeters * Math.Sin(angle));
            }
            coords[segments] = coords[0]; // close the ring
            var factory = NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory();
            // Use LinearRing (closed line) not Polygon — eliminates any fill rendering entirely.
            return factory.CreateLinearRing(coords);
        }
        catch { return null; }
    }

    /// <summary>
    /// Called from App.axaml.cs when animation frames are available from RadarAnimationService.
    /// Creates one TileLayer per frame and inserts them into the map layer stack.
    /// </summary>
    public void LoadAnimationFrames(IReadOnlyList<Aprs.Desktop.Services.RadarFrame> frames)
    {
        // Remove old animation layers from the map.
        foreach (var fl in radarFrameLayers)
            MapControl.Map.Layers.Remove(fl);
        radarFrameLayers.Clear();
        radarFrameUrlBuilders.Clear();

        if (frames.Count == 0) return;

        // Find the index where the static radar layer sits so we insert at the same position.
        int insertIdx = MapControl.Map.Layers.Count - 1;
        if (radarLayer is not null)
        {
            var layerList = MapControl.Map.Layers.ToList();
            for (int i = 0; i < layerList.Count; i++)
            {
                if (layerList[i] == radarLayer) { insertIdx = i; break; }
            }
        }

        // Create a TileLayer for each frame, all disabled initially.
        foreach (var frame in frames)
        {
            var urlBuilder = new Mapping.WmsRadarUrlBuilder(frame.Timestamp);
            var schema = new BruTile.Predefined.GlobalSphericalMercator(0, 10) { Name = $"Radar {frame.Label}" };
            var httpSource = new BruTile.Web.HttpTileSource(
                schema, urlBuilder, name: $"Radar {frame.Label}",
                attribution: new BruTile.Attribution("NOAA/NWS NEXRAD", "https://www.weather.gov/"));
            var layer = new TileLayer(httpSource)
            {
                Name    = $"Radar {frame.Label}",
                Opacity = 0.65f,
                Enabled = false
            };
            radarFrameUrlBuilders.Add(urlBuilder);
            radarFrameLayers.Add(layer);
            MapControl.Map.Layers.Insert(insertIdx, layer);
        }

        // Disable the static layer — animation layers take over.
        if (radarLayer is not null) radarLayer.Enabled = false;

        // Update the viewmodel with frame count.
        if (DataContext is MapViewModel vm)
        {
            vm.RadarFrameCount = frames.Count;
            vm.RadarFrameIndex = frames.Count - 1; // start on the latest frame
            vm.RadarFrameTime  = frames[^1].Label;
        }

        // Show the latest frame immediately, but only if the operator has radar enabled.
        currentFrameIndex = frames.Count - 1;
        if ((DataContext as MapViewModel)?.ShowRadar == true)
            ShowFrame(currentFrameIndex);
    }

    private void StartAnimation()
    {
        if (radarFrameLayers.Count == 0) return;
        currentFrameIndex = 0;
        radarAnimTimer?.Stop();
        radarAnimTimer = new Avalonia.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(500),
            Avalonia.Threading.DispatcherPriority.Background,
            (_, _) => AdvanceFrame());
        radarAnimTimer.Start();
    }

    private void StopAnimation()
    {
        radarAnimTimer?.Stop();
        radarAnimTimer = null;
        // Show the latest frame when stopped.
        if (radarFrameLayers.Count > 0)
        {
            currentFrameIndex = radarFrameLayers.Count - 1;
            ShowFrame(currentFrameIndex);
        }
    }

    private void AdvanceFrame()
    {
        if (radarFrameLayers.Count == 0) return;
        currentFrameIndex = (currentFrameIndex + 1) % radarFrameLayers.Count;
        ShowFrame(currentFrameIndex);
    }

    /// <summary>Called from RadarStepRequested event — step one frame forward or back manually.</summary>
    public void StepFrame(int delta)
    {
        if (radarFrameLayers.Count == 0) return;
        currentFrameIndex = (currentFrameIndex + delta + radarFrameLayers.Count) % radarFrameLayers.Count;
        ShowFrame(currentFrameIndex);
    }

    private void ShowFrame(int index)
    {
        for (int i = 0; i < radarFrameLayers.Count; i++)
            radarFrameLayers[i].Enabled = i == index;

        MapControl.Map.RefreshData();
        MapControl.RefreshGraphics();

        if (DataContext is MapViewModel vm && index < radarFrameLayers.Count)
        {
            vm.RadarFrameIndex = index;
            // Extract time from layer name "Radar HH:mm"
            var parts = radarFrameLayers[index].Name.Split(' ');
            vm.RadarFrameTime  = parts.Length > 1 ? parts[^1] : string.Empty;
        }
    }

    /// <summary>Called by the 5-minute refresh timer to fetch new frames.</summary>
    public void RefreshRadar()
    {
        if (radarLayer is null) return;
        if (radarFrameLayers.Count == 0)
        {
            // Static mode — clear the layer's own tile cache to force a refetch.
            if (!(DataContext is MapViewModel { ShowRadar: true })) return;
            radarLayer.ClearCache();
        }
        else
        {
            // Animation mode — clear all frame layer caches.
            foreach (var layer in radarFrameLayers)
                layer.ClearCache();
        }
        MapControl.Map.RefreshData();
        MapControl.RefreshGraphics();
    }

    /// <summary>Called from App.axaml.cs to give MapView access to the trail service for toggle redraws.</summary>
    public void WireTrailService(StationTrailService service)
    {
        trailService = service;
    }

    private void UpdatePanels()
    {
        if (DataContext is not MapViewModel viewModel)
        {
            EmptySelectionPanel.IsVisible = true;
            StationDetailsPanel.IsVisible = false;
            return;
        }

        EmptySelectionPanel.IsVisible =
            !viewModel.HasSelectedStation && !viewModel.HasSelectedObject && !viewModel.HasSelectedWeather;
        StationDetailsPanel.IsVisible = viewModel.HasSelectedStation;
    }

    private void RefreshFeatures()
    {
        if (markerLayer is null || DataContext is not MapViewModel viewModel)
        {
            return;
        }

        var features = new List<IFeature>();

        foreach (var marker in viewModel.Markers)
        {
            var selected = ReferenceEquals(marker, viewModel.SelectedStation);
            var feature = MakeFeature(marker.MapLeftPercent, marker.MapTopPercent);
            AddStationStyles(feature, marker, selected);
            feature.Styles.Add(LabelFor(marker.DisplayName));
            feature["station"] = marker;
            features.Add(feature);
        }

        foreach (var marker in viewModel.WeatherMarkers)
        {
            var selected = ReferenceEquals(marker, viewModel.SelectedWeather);
            var color = marker.IsStale ? new Color(100, 116, 139) : new Color(2, 132, 199);
            var feature = MakeFeature(marker.MapLeftPercent, marker.MapTopPercent);
            feature.Styles.Add(DotStyle(color, SymbolType.Ellipse, selected));
            feature.Styles.Add(LabelFor(marker.DisplayName));
            feature["weather"] = marker;
            features.Add(feature);
        }

        foreach (var marker in viewModel.ObjectMarkers)
        {
            var selected = ReferenceEquals(marker, viewModel.SelectedObject);
            var feature = MakeFeature(marker.MapLeftPercent, marker.MapTopPercent);
            feature.Styles.Add(DotStyle(ObjectColor(marker), SymbolType.Rectangle, selected));
            feature.Styles.Add(LabelFor(marker.ObjectName));
            feature["object"] = marker;
            features.Add(feature);
        }

        markerLayer.Features.Clear();
        markerLayer.Features.AddRange(features);
        markerLayer.DataHasChanged();

        FitToDataOnce(features);
    }

    // APRS symbol sheets (aprs.fi set by OH7LZB), bundled as embedded resources.
    // Each sheet is a 16-column grid of 64px cells indexed by symbol code (0x21..0x7E).
    private const string PrimarySheet = "embedded://Aprs.Desktop.aprs-symbols-64-0.png";
    private const string SecondarySheet = "embedded://Aprs.Desktop.aprs-symbols-64-1.png";
    private const string OverlaySheet = "embedded://Aprs.Desktop.aprs-symbols-64-2.png";
    private const int CellSize = 64;
    private const double IconScale = 0.45;          // 64px * 0.45 ~= 29px on screen
    private const double SelectedIconScale = 0.55;

    private static PointFeature MakeFeature(double leftPercent, double topPercent)
    {
        // The view model encodes position as a whole-planet percentage (see
        // PlaceholderMapCoordinateConverter): longitude = x*360-180, latitude = 90-y*180.
        // Recover the real coordinate and project to Web Mercator for Mapsui.
        var longitude = (leftPercent / 100.0 * 360.0) - 180.0;
        var latitude = 90.0 - (topPercent / 100.0 * 180.0);
        var (mercatorX, mercatorY) = SphericalMercator.FromLonLat(longitude, latitude);
        return new PointFeature(new MPoint(mercatorX, mercatorY));
    }

    private static bool TryRegion(char code, out BitmapRegion region)
    {
        region = null!;
        if (code < '!' || code > '~')
        {
            return false;
        }

        var index = code - '!';
        region = new BitmapRegion((index % 16) * CellSize, (index / 16) * CellSize, CellSize, CellSize);
        return true;
    }

    private static void AddStationStyles(PointFeature feature, StationMarkerViewModel marker, bool selected)
    {
        var table   = marker.SymbolTableIdentifier;
        var code    = marker.SymbolCode;
        var opacity = StationOpacity(marker.AgeState);

        // No usable APRS symbol: fall back to the colored category dot.
        if (table is null || code is null || !TryRegion(code.Value, out var region))
        {
            var dot = DotStyle(StationColor(marker), SymbolType.Ellipse, selected);
            dot.Opacity = opacity;
            feature.Styles.Add(dot);
            return;
        }

        if (selected)
        {
            feature.Styles.Add(new SymbolStyle
            {
                SymbolType = SymbolType.Ellipse,
                Fill = new Brush(new Color(250, 204, 21, 170)),
                Outline = new Pen(new Color(202, 138, 4), 2),
                SymbolScale = 0.7
            });
        }

        var scale = selected ? SelectedIconScale : IconScale;
        var sheet = table.Value == '/' ? PrimarySheet : SecondarySheet;
        var imgStyle = new ImageStyle
        {
            Image = new Mapsui.Styles.Image { Source = sheet, BitmapRegion = region },
            SymbolScale = scale,
            Opacity = opacity
        };
        feature.Styles.Add(imgStyle);

        // For overlay symbols (table id other than '/' or '\'), draw the overlay
        // character glyph on top of the base symbol.
        if (marker.Overlay is char overlay && TryRegion(overlay, out var overlayRegion))
        {
            feature.Styles.Add(new ImageStyle
            {
                Image = new Mapsui.Styles.Image { Source = OverlaySheet, BitmapRegion = overlayRegion },
                SymbolScale = scale
            });
        }
    }

    private static SymbolStyle DotStyle(Color color, SymbolType symbolType, bool selected)
    {
        return new SymbolStyle
        {
            SymbolType = symbolType,
            Fill = new Brush(color),
            Outline = new Pen(selected ? new Color(250, 204, 21) : Color.White, selected ? 3 : 2),
            SymbolScale = selected ? 0.9 : 0.7
        };
    }

    private static LabelStyle LabelFor(string label)
    {
        return new LabelStyle
        {
            Text = label,
            ForeColor = new Color(15, 23, 42),
            BackColor = new Brush(new Color(248, 250, 252, 230)),
            Halo = new Pen(Color.White, 1),
            Offset = new Offset(0, 22),
            Font = new Font { Size = 11 }
        };
    }

    // Centers the map on the operator's home QTH at a regional zoom. Runs once at startup
    // (guarded by hasFitToData) so it doesn't fight the user's later panning and zooming.
    private void FitToDataOnce(IReadOnlyCollection<IFeature> features)
    {
        if (hasFitToData)
        {
            return;
        }

        hasFitToData = true;
        CenterOnHome();
    }

    private void CenterOnHome()
    {
        var profile = StationProfile.Load();
        var (x, y) = SphericalMercator.FromLonLat(profile.Longitude, profile.Latitude);
        MapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(x, y), HomeResolution);
    }

    /// <summary>
    /// Redraws the trail layer from the current StationTrailService data.
    /// Does nothing if ShowTrails is false on the current MapViewModel.
    /// </summary>
    public void UpdateTrails(StationTrailService trailService)
    {
        if (trailLayer is null) return;

        // If trails are turned off, clear the layer and return.
        if (DataContext is MapViewModel vm && !vm.ShowTrails)
        {
            trailLayer.Clear();
            trailLayer.DataHasChanged();
            return;
        }

        trailLayer.Clear();

        var lineStyle = new VectorStyle
        {
            Line = new Pen(new Color(99, 102, 241, 160), 2) // indigo, semi-transparent
        };

        foreach (var (_, points) in trailService.GetTrails())
        {
            if (points.Count < 2) continue;

            var coords = points.Select(p =>
            {
                var (mx, my) = SphericalMercator.FromLonLat(p.Longitude, p.Latitude);
                return new Coordinate(mx, my);
            }).ToArray();

            var geometry = new LineString(coords);
            var feature = new GeometryFeature(geometry);
            feature.Styles.Add(lineStyle);
            trailLayer.Add(feature);
        }

        trailLayer.DataHasChanged();
    }

    private async void OnFindStationRequested(object? sender, EventArgs e)
    {
        // Simple input dialog — type a callsign and centre the map on it.
        var dialog = new Avalonia.Controls.Window
        {
            Title = "Find Station",
            Width = 360,
            Height = 160,
            CanResize = false,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
        };

        var panel = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 12 };
        panel.Children.Add(new Avalonia.Controls.TextBlock
        {
            Text = "Enter callsign to find on the map:",
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0F172A"))
        });

        var input = new Avalonia.Controls.TextBox { Watermark = "e.g. KE4CON" };
        panel.Children.Add(input);

        var btnRow = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var findBtn = new Avalonia.Controls.Button { Content = "Find", Padding = new Avalonia.Thickness(16, 6) };
        var cancelBtn = new Avalonia.Controls.Button { Content = "Cancel", Padding = new Avalonia.Thickness(10, 6) };
        btnRow.Children.Add(findBtn);
        btnRow.Children.Add(cancelBtn);
        panel.Children.Add(btnRow);
        dialog.Content = panel;

        string? result = null;
        findBtn.Click   += (_, _) => { result = input.Text?.Trim().ToUpperInvariant(); dialog.Close(); };
        cancelBtn.Click += (_, _) => dialog.Close();
        input.KeyDown   += (_, k) => { if (k.Key == Avalonia.Input.Key.Enter) { result = input.Text?.Trim().ToUpperInvariant(); dialog.Close(); } };

        var owner = this.VisualRoot as Avalonia.Controls.Window;
        if (owner is not null) await dialog.ShowDialog(owner);

        if (string.IsNullOrWhiteSpace(result)) return;

        // Find the station in the current viewmodel markers.
        if (DataContext is not MapViewModel vm) return;
        var marker = vm.Markers.FirstOrDefault(m =>
            string.Equals(m.Callsign, result, StringComparison.OrdinalIgnoreCase));

        if (marker is not null)
        {
            vm.RequestCentreOnPosition(marker.Latitude, marker.Longitude);
        }
        else
        {
            // Station not in the current station list — show a brief message.
            var notFound = new Avalonia.Controls.Window
            {
                Title = "Not Found",
                Width = 320,
                Height = 130,
                CanResize = false,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
            };
            var msg = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(20), Spacing = 12 };
            msg.Children.Add(new Avalonia.Controls.TextBlock
            {
                Text = $"{result} is not in the current station list.\nIt may not have been heard recently.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#475569"))
            });
            var ok = new Avalonia.Controls.Button
            {
                Content = "OK",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Padding = new Avalonia.Thickness(16, 6)
            };
            ok.Click += (_, _) => notFound.Close();
            msg.Children.Add(ok);
            notFound.Content = msg;
            if (owner is not null) await notFound.ShowDialog(owner);
        }
    }

    private void OnToggleMapLayerRequested(object? sender, EventArgs e)
    {
        // Cycle through: OSM → USGS Topo → USGS Imagery → USGS Imagery+Topo → OSM
        var kinds = new[] { BaseMapKind.OpenStreetMap, BaseMapKind.UsgsTopo, BaseMapKind.UsgsImagery, BaseMapKind.UsgsImageryTopo };
        baseMapIndex = (baseMapIndex + 1) % kinds.Length;
        SetBaseMap(kinds[baseMapIndex]);
    }

    private void OnNavigationRequested(object? sender, MapNavigationRequest request)
    {
        switch (request.Kind)
        {
            case MapNavigationKind.Home:
                CenterOnHome();
                break;

            case MapNavigationKind.CentreOnStation:
                var profile = StationProfile.Load();
                var (sx, sy) = SphericalMercator.FromLonLat(profile.Longitude, profile.Latitude);
                MapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(sx, sy), HomeResolution / 4);
                break;

            case MapNavigationKind.CentreOnPosition when request.Latitude.HasValue && request.Longitude.HasValue:
                var (px, py) = SphericalMercator.FromLonLat(request.Longitude.Value, request.Latitude.Value);
                MapControl.Map.Navigator.CenterOnAndZoomTo(new MPoint(px, py), HomeResolution / 8);
                break;
        }
    }

    private void OnMapInfo(object? sender, MapInfoEventArgs e)
    {
        if (DataContext is not MapViewModel viewModel || markerLayer is null)
        {
            return;
        }

        // While a draw tool is active, pointer input is consumed by the draw handlers
        // (OnDrawPointer*), so Mapsui never raises Info here — no draw branch needed.
        var feature = e.GetMapInfo(new ILayer[] { markerLayer })?.Feature;
        if (feature is not null)
        {
            if (feature["station"] is StationMarkerViewModel station)
            {
                viewModel.SelectStation(station);
                UpdatePanels();
                RefreshFeatures();
                return;
            }

            if (feature["object"] is ObjectMarkerViewModel objectMarker)
            {
                viewModel.SelectObject(objectMarker);
                UpdatePanels();
                RefreshFeatures();
                return;
            }

            if (feature["weather"] is WeatherStationMarkerViewModel weather)
            {
                viewModel.SelectWeather(weather);
                UpdatePanels();
                RefreshFeatures();
                return;
            }
        }

        // Empty-map click: convert the world position back to the view model's
        // normalized percentage space and forward it (used for placing/moving objects).
        var world = e.WorldPosition;
        if (world is null)
        {
            return;
        }

        var (longitude, latitude) = SphericalMercator.ToLonLat(world.X, world.Y);
        var xPercent = (longitude + 180.0) / 360.0 * 100.0;
        var yPercent = (90.0 - latitude) / 180.0 * 100.0;
        if (viewModel.HandleMapClick(xPercent, yPercent))
        {
            UpdatePanels();
            RefreshFeatures();
        }
    }

    private void ClearSelectionButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MapViewModel viewModel)
        {
            viewModel.ClearSelection();
            UpdatePanels();
            RefreshFeatures();
        }
    }

    private static float StationOpacity(StationLifecycleState state) => state switch
    {
        StationLifecycleState.Active  => 1.0f,
        StationLifecycleState.Stale   => 0.5f,
        StationLifecycleState.Expired => 0.25f,
        StationLifecycleState.Hidden  => 0.15f,
        _                             => 1.0f
    };

    private static Color StationColor(StationMarkerViewModel marker)
    {
        return marker.AgeState switch
        {
            StationLifecycleState.Hidden => new Color(71, 85, 105),
            StationLifecycleState.Expired => new Color(100, 116, 139),
            _ => marker.MarkerIconKey switch
            {
                "home" => new Color(37, 99, 235),
                "car" => new Color(22, 101, 52),
                "truck" => new Color(21, 128, 61),
                "weather" => new Color(2, 132, 199),
                "digipeater" => new Color(147, 51, 234),
                "repeater" => new Color(190, 18, 60),
                "object" => new Color(202, 138, 4),
                _ => new Color(37, 99, 235)
            }
        };
    }

    private static Color ObjectColor(ObjectMarkerViewModel marker)
    {
        if (marker.IsKilled || marker.LifecycleState == AprsObjectLifecycleState.Killed)
        {
            return new Color(100, 116, 139);
        }

        if (marker.LifecycleState == AprsObjectLifecycleState.Expired)
        {
            return new Color(120, 113, 108);
        }

        return marker.ObjectType == AprsManagedObjectType.Item
            ? new Color(217, 119, 6)
            : new Color(202, 138, 4);
    }

    // ── Draw tools engine ─────────────────────────────────────────────────────

    private void OnDrawModeChanged(object? sender, DrawMode mode)
    {
        // Commit — do NOT discard — any shape already drawn before switching tools.
        // Previously this nulled shapeInProgress, so a finished-looking line/polygon
        // vanished the moment the next tool was picked. Now it is kept on the map.
        CommitInProgressShape();
        currentDrawMode = mode;
        circleDragging = false;
        textDragging = false;
        drawHoverWorld = null;
        if (mode != DrawMode.SelectText) { selectedText = null; resizingText = false; movingText = false; }
        RedrawAllShapes();
        MapControl.Cursor = mode == DrawMode.None
            ? Avalonia.Input.Cursor.Default
            : new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Cross);
    }

    private void OnClearDrawings(object? sender, EventArgs e)
    {
        completedShapes.Clear();
        shapeInProgress = null;
        drawHoverWorld = null;
        circleDragging = false;
        selectedText = null; resizingText = false; movingText = false;
        drawingLayer?.Clear();
        MapControl.Map.RefreshData();
        MapControl.RefreshGraphics();
    }

    private async void OnImportGeoFile(object? sender, EventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Import GPX or KML File",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("GPS Exchange / KML")
                    {
                        Patterns = ["*.gpx", "*.kml"],
                        MimeTypes = ["application/gpx+xml", "application/vnd.google-earth.kml+xml"]
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("All files") { Patterns = ["*"] }
                ]
            });

        if (files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        var result = GeoFileImportExportService.Import(path);

        if (!result.Success)
        {
            // Show error to user — use a simple dialog
            var dialog = new Avalonia.Controls.Window
            {
                Title       = "Import Failed",
                Width       = 420,
                Height      = 180,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                Content     = new Avalonia.Controls.TextBlock
                {
                    Text        = result.ErrorMessage ?? "Unknown error.",
                    Margin      = new Avalonia.Thickness(20),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                }
            };
            await dialog.ShowDialog(topLevel as Avalonia.Controls.Window ?? new Avalonia.Controls.Window());
            return;
        }

        // Add imported shapes to the drawing layer
        foreach (var shape in result.Shapes)
        {
            completedShapes.Add(shape);
        }

        // Add waypoints as point markers via APRS objects if any
        // (For now, convert them to single-point shapes labelled with their name)
        foreach (var wpt in result.Waypoints)
        {
            var (x, y) = GeoFileImportExportService.LatLonToWorld(wpt.Latitude, wpt.Longitude);
            var shape = new DrawingShape
            {
                ShapeType   = DrawShapeType.Line,
                Label       = wpt.Name,
                Color       = "#0F2A4A",
                StrokeWidth = 3.0,
            };
            // A single-point "line" renders as a dot — waypoints show as labelled dots
            shape.Points.Add((x, y));
            shape.Points.Add((x, y));
            completedShapes.Add(shape);
        }

        RedrawAllShapes();
    }

    private async void OnExportGeoFile(object? sender, EventArgs e)
    {
        if (completedShapes.Count == 0)
        {
            var dialog = new Avalonia.Controls.Window
            {
                Title   = "Nothing to Export",
                Width   = 380,
                Height  = 150,
                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                Content = new Avalonia.Controls.TextBlock
                {
                    Text   = "No drawings on the map to export. Use the drawing tools to add lines or polygons first.",
                    Margin = new Avalonia.Thickness(20),
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                }
            };
            var topLvl = TopLevel.GetTopLevel(this) as Avalonia.Controls.Window
                         ?? new Avalonia.Controls.Window();
            await dialog.ShowDialog(topLvl);
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title           = "Export Drawings",
                SuggestedFileName = $"aprs-command-export-{DateTime.Now:yyyyMMdd-HHmm}",
                FileTypeChoices =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("GPX Track File") { Patterns = ["*.gpx"] },
                    new Avalonia.Platform.Storage.FilePickerFileType("KML File")       { Patterns = ["*.kml"] },
                ]
            });

        if (file is null) return;

        var ext     = Path.GetExtension(file.Name).ToLowerInvariant();
        var content = ext == ".kml"
            ? GeoFileImportExportService.ExportToKml(completedShapes, [])
            : GeoFileImportExportService.ExportToGpx(completedShapes, []);

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8);
        await writer.WriteAsync(content);
    }

    // Convert an Avalonia pointer position (device-independent pixels) to map world
    // coordinates (EPSG:3857 metres) via the current Mapsui viewport.
    private MPoint? ScreenToWorld(Avalonia.Point screen)
    {
        try { return MapControl.Map.Navigator.Viewport.ScreenToWorld(screen.X, screen.Y); }
        catch { return null; }
    }

    private void OnDrawPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (currentDrawMode == DrawMode.None || drawingLayer is null) return;
        if (!e.GetCurrentPoint(MapControl).Properties.IsLeftButtonPressed) return;
        e.Handled = true;   // suppress Mapsui pan/zoom while drawing

        var world = ScreenToWorld(e.GetPosition(MapControl));
        if (world is null) return;

        switch (currentDrawMode)
        {
            case DrawMode.Line:
            case DrawMode.Polygon:
                // Double-click finishes the shape; a single click adds a vertex.
                if (e.ClickCount >= 2) CommitInProgressShape();
                else AddVertex(world);
                break;

            case DrawMode.Circle:
                // Press = centre; the following drag sets the radius (see moved/released).
                shapeInProgress = new DrawingShape
                {
                    ShapeType    = DrawShapeType.Circle,
                    Centre       = (world.X, world.Y),
                    RadiusMetres = 0,
                    Color        = CurrentColorHex(),
                    FillStyle    = CurrentFill(),
                    StrokeWidth  = CurrentWidth(),
                };
                circleDragging = true;
                e.Pointer.Capture(MapControl);
                break;

            case DrawMode.Text:
                // Press = anchor; drag sets the size (see moved/released), then a dialog asks for the label.
                textAnchor = (world.X, world.Y);
                shapeInProgress = new DrawingShape
                {
                    ShapeType          = DrawShapeType.Text,
                    Color              = CurrentColorHex(),
                    Label              = "Text",   // placeholder shown while sizing
                    BackgroundColorHex = (DataContext as MapViewModel)?.CurrentTextBackground,
                    GroundHeightMetres = DefaultTextGroundHeight(),
                };
                shapeInProgress.Points.Add((world.X, world.Y));
                textDragging = true;
                e.Pointer.Capture(MapControl);
                RedrawAllShapes();
                break;

            case DrawMode.Eyedropper:
                {
                    var picked = SampleMapPixel(e.GetPosition(MapControl));
                    if (picked is not null && DataContext is MapViewModel vmPick)
                    {
                        vmPick.SetActiveToolColorHex(picked);   // applies to the last drawing tool
                        vmPick.ExitEyedropper();                // return to that tool (keep the toolbar open)
                    }
                }
                break;

            case DrawMode.SelectText:
                HandleSelectTextPress(world);
                e.Pointer.Capture(MapControl);
                break;

            case DrawMode.Erase:
                EraseShapeAt(world);
                break;
        }
    }

    private double CurrentResolution()
    {
        var res = MapControl.Map.Navigator.Viewport.Resolution;
        return res > 0 ? res : 1.0;
    }

    private static double DistWorld(double ax, double ay, double bx, double by)
    {
        var dx = ax - bx; var dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    // Resize handle position: down-and-right of the anchor at a distance equal to the text height.
    private static (double X, double Y) TextHandleWorld(DrawingShape t)
    {
        var h = t.GroundHeightMetres > 0 ? t.GroundHeightMetres : 50.0;
        return (t.Points[0].X + h * 0.7071, t.Points[0].Y - h * 0.7071);
    }

    private void HandleSelectTextPress(MPoint world)
    {
        var res = CurrentResolution();

        // If a text is already selected and the press is on its handle → start resizing.
        if (selectedText is not null)
        {
            var hp = TextHandleWorld(selectedText);
            if (DistWorld(world.X, world.Y, hp.X, hp.Y) < 14 * res) { resizingText = true; return; }
        }

        // Otherwise select the nearest text under the cursor (and start moving it).
        DrawingShape? hit = null;
        double best = double.MaxValue;
        foreach (var s in completedShapes)
        {
            if (s.ShapeType != DrawShapeType.Text || s.Points.Count < 1) continue;
            var d = DistWorld(world.X, world.Y, s.Points[0].X, s.Points[0].Y);
            var reach = Math.Max(s.GroundHeightMetres > 0 ? s.GroundHeightMetres : 40 * res, 20 * res);
            if (d < reach && d < best) { best = d; hit = s; }
        }
        selectedText = hit;
        if (hit is not null)
        {
            movingText = true;
            moveGrabOffset = (world.X - hit.Points[0].X, world.Y - hit.Points[0].Y);
        }
        RedrawAllShapes();
    }

    // A sensible starting text size (~24 px at the current zoom), expressed in world metres.
    private double DefaultTextGroundHeight()
    {
        var res = MapControl.Map.Navigator.Viewport.Resolution;
        return (res > 0 ? res : 1.0) * 24.0;
    }

    private string CurrentColorHex() => (DataContext as MapViewModel)?.ActiveColorHex ?? "#FF0000";
    private DrawFillStyle CurrentFill() => (DataContext as MapViewModel)?.ActiveFill ?? DrawFillStyle.Solid;
    private double CurrentWidth() => (DataContext as MapViewModel)?.ActiveWidth ?? 3.0;

    // Background choices offered in the Add Map Text dialog (label → hex, null = none).
    private static readonly (string Label, string? Hex)[] TextBackgrounds =
    [
        ("None (transparent)", null),
        ("White",  "#FFFFFF"),
        ("Black",  "#000000"),
        ("Yellow", "#FFF3A0"),
        ("Light gray", "#E6E9EE"),
    ];

    // Called after the drag that sizes the text: asks for the label + background, then commits.
    private async System.Threading.Tasks.Task FinishTextAsync(DrawingShape ts)
    {
        var vm = DataContext as MapViewModel;
        var entry = await PromptForTextAsync(vm?.CurrentTextBackground);
        if (entry is not { } e || string.IsNullOrWhiteSpace(e.Text))
        {
            if (ReferenceEquals(shapeInProgress, ts)) shapeInProgress = null;   // cancelled — drop the placeholder
            RedrawAllShapes();
            return;
        }
        if (vm is not null) vm.CurrentTextBackground = e.Background;
        ts.Label = e.Text.Trim();
        ts.BackgroundColorHex = e.Background;
        if (ReferenceEquals(shapeInProgress, ts)) FinaliseShape(ts);
        else { completedShapes.Add(ts); RedrawAllShapes(); }
    }

    private async System.Threading.Tasks.Task<(string Text, string? Background)?> PromptForTextAsync(string? initialBackground)
    {
        if (TopLevel.GetTopLevel(this) is not Avalonia.Controls.Window owner) return null;

        var box = new Avalonia.Controls.TextBox { Watermark = "Label text", MinWidth = 320, AcceptsReturn = false };

        var bg = new Avalonia.Controls.ComboBox { Width = 170 };
        foreach (var o in TextBackgrounds) bg.Items.Add(o.Label);
        var bgIdx = Array.FindIndex(TextBackgrounds, o => string.Equals(o.Hex, initialBackground, StringComparison.OrdinalIgnoreCase));
        bg.SelectedIndex = bgIdx >= 0 ? bgIdx : 1;   // default White

        (string, string?)? result = null;

        var place  = new Avalonia.Controls.Button { Content = "Place", IsDefault = true };
        var cancel = new Avalonia.Controls.Button { Content = "Cancel", IsCancel = true };

        var bgRow = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        };
        bgRow.Children.Add(new Avalonia.Controls.TextBlock { Text = "Background:", Width = 90, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        bgRow.Children.Add(bg);

        var buttons = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
        };
        buttons.Children.Add(place);
        buttons.Children.Add(cancel);

        var panel = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12 };
        panel.Children.Add(new Avalonia.Controls.TextBlock { Text = "Text to place on the map:" });
        panel.Children.Add(box);
        panel.Children.Add(bgRow);
        panel.Children.Add(new Avalonia.Controls.TextBlock { Text = "(Size was set by your drag; it scales as you zoom.)", Opacity = 0.7, FontSize = 11 });
        panel.Children.Add(buttons);

        var dlg = new Avalonia.Controls.Window
        {
            Title = "Add Map Text",
            Content = panel,
            SizeToContent = Avalonia.Controls.SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
        };

        place.Click  += (_, _) =>
        {
            var chosenBg = TextBackgrounds[Math.Max(0, bg.SelectedIndex)].Hex;
            result = (box.Text ?? string.Empty, chosenBg);
            dlg.Close();
        };
        cancel.Click += (_, _) => { result = null; dlg.Close(); };
        dlg.Opened   += (_, _) => box.Focus();

        await dlg.ShowDialog(owner);
        return result;
    }

    // Render a 1×1 sample of the map under the given point and return its colour as #RRGGBB.
    private string? SampleMapPixel(Avalonia.Point pos)
    {
        try
        {
            var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            int w = (int)Math.Ceiling(MapControl.Bounds.Width * scale);
            int h = (int)Math.Ceiling(MapControl.Bounds.Height * scale);
            if (w <= 0 || h <= 0) return null;

            using var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new Avalonia.PixelSize(w, h), new Avalonia.Vector(96 * scale, 96 * scale));
            rtb.Render(MapControl);

            int px = Math.Clamp((int)(pos.X * scale), 0, w - 1);
            int py = Math.Clamp((int)(pos.Y * scale), 0, h - 1);

            var buf = new byte[4];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buf, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { rtb.CopyPixels(new Avalonia.PixelRect(px, py, 1, 1), handle.AddrOfPinnedObject(), buf.Length, 4); }
            finally { handle.Free(); }

            // RenderTargetBitmap is Bgra8888.
            return $"#{buf[2]:X2}{buf[1]:X2}{buf[0]:X2}";
        }
        catch { return null; }
    }

    // Native colour picker: Avalonia ColorView with a round ring spectrum (plus palette swatches,
    // RGB/HSV sliders, and hex). Themed via the ColorPicker StyleInclude added to App.axaml.
    private async void OnCustomColorRequested(object? sender, EventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Avalonia.Controls.Window owner) return;
        if (DataContext is not MapViewModel vm) return;

        Avalonia.Media.Color initial;
        try { initial = Avalonia.Media.Color.Parse(vm.ActiveColorHex); }
        catch { initial = Avalonia.Media.Colors.Red; }

        var view = new Avalonia.Controls.ColorView
        {
            Color = initial,
            IsAlphaEnabled = false,
            ColorSpectrumShape = Avalonia.Controls.ColorSpectrumShape.Ring,   // round wheel
            Palette = new Avalonia.Controls.FluentColorPalette(),             // rich swatch grid on the Palette tab
            Width = 360,
            Height = 420,
        };

        bool ok = false;
        var use    = new Avalonia.Controls.Button { Content = "Use color", IsDefault = true };
        var cancel = new Avalonia.Controls.Button { Content = "Cancel", IsCancel = true };
        var buttons = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Avalonia.Thickness(0, 10, 0, 0),
        };
        buttons.Children.Add(use);
        buttons.Children.Add(cancel);

        var panel = new Avalonia.Controls.StackPanel { Margin = new Avalonia.Thickness(14), Spacing = 8 };
        panel.Children.Add(view);
        panel.Children.Add(buttons);

        var dlg = new Avalonia.Controls.Window
        {
            Title = "Custom Drawing Color",
            Content = panel,
            SizeToContent = Avalonia.Controls.SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
        };
        use.Click    += (_, _) => { ok = true; dlg.Close(); };
        cancel.Click += (_, _) => { ok = false; dlg.Close(); };

        await dlg.ShowDialog(owner);
        if (ok)
        {
            var c = view.Color;
            vm.SetActiveToolColorHex($"#{c.R:X2}{c.G:X2}{c.B:X2}");
        }
    }

    private void OnDrawPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (currentDrawMode == DrawMode.None || drawingLayer is null) return;
        e.Handled = true;

        var world = ScreenToWorld(e.GetPosition(MapControl));
        if (world is null) return;

        if (currentDrawMode == DrawMode.Circle && circleDragging &&
            shapeInProgress is { ShapeType: DrawShapeType.Circle })
        {
            var dx = world.X - shapeInProgress.Centre.X;
            var dy = world.Y - shapeInProgress.Centre.Y;
            shapeInProgress.RadiusMetres = Math.Sqrt(dx * dx + dy * dy);
            RedrawAllShapes();
        }
        else if (currentDrawMode == DrawMode.Text && textDragging &&
                 shapeInProgress is { ShapeType: DrawShapeType.Text })
        {
            // Drag distance from the anchor sets the text's ground height (grows 1:1 with the drag).
            var dx = world.X - textAnchor.X;
            var dy = world.Y - textAnchor.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > 0) shapeInProgress.GroundHeightMetres = dist;
            RedrawAllShapes();
        }
        else if (currentDrawMode == DrawMode.SelectText && selectedText is not null &&
                 e.GetCurrentPoint(MapControl).Properties.IsLeftButtonPressed)
        {
            if (resizingText)
            {
                var h = DistWorld(world.X, world.Y, selectedText.Points[0].X, selectedText.Points[0].Y);
                if (h > 0) { selectedText.GroundHeightMetres = h; RedrawAllShapes(); }
            }
            else if (movingText)
            {
                selectedText.Points[0] = (world.X - moveGrabOffset.X, world.Y - moveGrabOffset.Y);
                RedrawAllShapes();
            }
        }
        else if ((currentDrawMode == DrawMode.Line || currentDrawMode == DrawMode.Polygon) &&
                 shapeInProgress is not null && shapeInProgress.Points.Count >= 1)
        {
            // Rubber-band the segment from the last placed point to the cursor.
            drawHoverWorld = (world.X, world.Y);
            RedrawAllShapes();
        }
    }

    private void OnDrawPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (currentDrawMode == DrawMode.None || drawingLayer is null) return;
        e.Handled = true;

        if (currentDrawMode == DrawMode.Circle && circleDragging)
        {
            circleDragging = false;
            e.Pointer.Capture(null);
            // A real drag becomes a circle; a bare click (no meaningful radius) is discarded.
            if (shapeInProgress is { ShapeType: DrawShapeType.Circle } c && c.RadiusMetres > MinCircleRadiusMetres)
                FinaliseShape(c);
            else
            {
                shapeInProgress = null;
                RedrawAllShapes();
            }
        }
        else if (currentDrawMode == DrawMode.Text && textDragging)
        {
            textDragging = false;
            e.Pointer.Capture(null);
            if (shapeInProgress is { ShapeType: DrawShapeType.Text } ts)
            {
                // A bare click (little/no drag) gets the default size.
                if (ts.GroundHeightMetres < DefaultTextGroundHeight() * 0.4)
                    ts.GroundHeightMetres = DefaultTextGroundHeight();
                _ = FinishTextAsync(ts);
            }
        }
        else if (currentDrawMode == DrawMode.SelectText)
        {
            resizingText = false;
            movingText = false;
            e.Pointer.Capture(null);
        }
    }

    private void AddVertex(MPoint world)
    {
        shapeInProgress ??= new DrawingShape
        {
            ShapeType   = currentDrawMode == DrawMode.Polygon ? DrawShapeType.Polygon : DrawShapeType.Line,
            Color       = CurrentColorHex(),
            FillStyle   = CurrentFill(),
            StrokeWidth = CurrentWidth(),
        };
        shapeInProgress.Points.Add((world.X, world.Y));
        RedrawAllShapes();
    }

    /// <summary>
    /// Commit the in-progress shape to the map if it has enough points to be a real shape;
    /// otherwise discard it. Called when the shape is finished (double-click) and whenever
    /// the active tool changes — so a drawn shape is never silently lost on tool switch.
    /// </summary>
    public void CommitInProgressShape()
    {
        var s = shapeInProgress;
        drawHoverWorld = null;
        if (s is null) return;
        // Text is only committed through its own dialog (FinishTextAsync); never auto-commit the
        // "Text" placeholder that exists mid-placement.
        if (s.ShapeType == DrawShapeType.Text) { shapeInProgress = null; RedrawAllShapes(); return; }
        if (s.IsCompletable(MinCircleRadiusMetres)) FinaliseShape(s);
        else { shapeInProgress = null; RedrawAllShapes(); }
    }

    // Retained public name (kept for potential Enter-key wiring); finishes the current shape.
    public void FinaliseCurrentShape() => CommitInProgressShape();

    private void EraseShapeAt(MPoint pt)
    {
        const double hitRadius = 20000;
        var toRemove = completedShapes.FirstOrDefault(s =>
        {
            if (s.ShapeType == DrawShapeType.Circle)
            {
                var dx = pt.X - s.Centre.X;
                var dy = pt.Y - s.Centre.Y;
                return Math.Abs(Math.Sqrt(dx * dx + dy * dy) - s.RadiusMetres) < hitRadius;
            }
            return s.Points.Any(p =>
            {
                var dx = pt.X - p.X; var dy = pt.Y - p.Y;
                return Math.Sqrt(dx * dx + dy * dy) < hitRadius;
            });
        });
        if (toRemove is not null) { completedShapes.Remove(toRemove); RedrawAllShapes(); }
    }

    private void FinaliseShape(DrawingShape shape)
    {
        completedShapes.Add(shape);
        shapeInProgress = null;
        drawHoverWorld = null;
        RedrawAllShapes();
    }

    private void RedrawAllShapes()
    {
        if (drawingLayer is null) return;
        var resolution = MapControl.Map.Navigator.Viewport.Resolution;
        if (resolution <= 0) resolution = 1.0;
        var vm = DataContext as MapViewModel;
        bool showMeas = vm?.ShowMeasurements ?? false;
        bool imperial = vm?.MeasurementsImperial ?? true;

        drawingLayer.Clear();
        foreach (var shape in completedShapes)
        {
            var f = BuildShapeFeature(shape, resolution);
            if (f is not null) drawingLayer.Add(f);
            if (showMeas) AddMeasurementLabel(shape, imperial);
        }
        if (shapeInProgress is not null)
        {
            var preview = RenderableInProgress();
            var pf = BuildShapeFeature(preview, resolution);
            if (pf is not null) drawingLayer.Add(pf);
            if (showMeas) AddMeasurementLabel(preview, imperial);
        }
        // Resize handle for the selected text (SelectText mode).
        if (currentDrawMode == DrawMode.SelectText && selectedText is { Points.Count: > 0 })
        {
            var hp = TextHandleWorld(selectedText);
            drawingLayer.Add(BuildHandleFeature(hp, resolution));
        }
        MapControl.Map.RefreshData();
        MapControl.RefreshGraphics();
    }

    // ── Shape measurements (length / diameter / area) ─────────────────────────
    // Shapes are stored in Web Mercator (EPSG:3857), which stretches distance and area away from the
    // equator; multiply by cos(latitude) (and cos² for area) to recover true ground values.

    private void AddMeasurementLabel(DrawingShape shape, bool imperial)
    {
        var m = MeasureShape(shape, imperial);
        if (m is not { } info) return;
        var factory = new NetTopologySuite.Geometries.GeometryFactory();
        var pt = factory.CreatePoint(new NetTopologySuite.Geometries.Coordinate(info.X, info.Y));
        var label = new LabelStyle
        {
            Text      = info.Text,
            ForeColor = new Color(17, 24, 39),
            BackColor = new Brush(new Color(255, 255, 255, 220)),
            Halo      = new Pen(new Color(255, 255, 255, 160), 1),
            Font      = new Font { Size = 12 },
        };
        drawingLayer?.Add(new GeometryFeature { Geometry = pt, Styles = [label] });
    }

    private static (string Text, double X, double Y)? MeasureShape(DrawingShape s, bool imperial)
    {
        switch (s.ShapeType)
        {
            case DrawShapeType.Line when s.Points.Count >= 2:
            {
                var len = ShapeMeasurements.GroundLengthMetres(s.Points);
                var mid = s.Points[s.Points.Count / 2];
                return (ShapeMeasurements.FormatLength(len, imperial), mid.X, mid.Y);
            }
            case DrawShapeType.Polygon when s.Points.Count >= 3:
            {
                var (area, cx, cy) = ShapeMeasurements.GroundAreaAndCentroid(s.Points);
                return (ShapeMeasurements.FormatArea(area, imperial), cx, cy);
            }
            case DrawShapeType.Circle when s.RadiusMetres > 0:
            {
                var r = ShapeMeasurements.GroundRadiusMetres(s.RadiusMetres, s.Centre.X, s.Centre.Y);
                var text = $"Ø {ShapeMeasurements.FormatLength(r * 2, imperial)}  ·  {ShapeMeasurements.FormatArea(Math.PI * r * r, imperial)}";
                return (text, s.Centre.X, s.Centre.Y);
            }
            default:
                return null;
        }
    }

    // A small square handle (~11 px) drawn at a world point, for grabbing to resize text.
    private static GeometryFeature BuildHandleFeature((double X, double Y) pos, double resolution)
    {
        var half = 5.5 * resolution;
        var factory = new NetTopologySuite.Geometries.GeometryFactory();
        var ring = factory.CreateLinearRing(new[]
        {
            new NetTopologySuite.Geometries.Coordinate(pos.X - half, pos.Y - half),
            new NetTopologySuite.Geometries.Coordinate(pos.X + half, pos.Y - half),
            new NetTopologySuite.Geometries.Coordinate(pos.X + half, pos.Y + half),
            new NetTopologySuite.Geometries.Coordinate(pos.X - half, pos.Y + half),
            new NetTopologySuite.Geometries.Coordinate(pos.X - half, pos.Y - half),
        });
        var style = new VectorStyle
        {
            Fill = new Brush(new Color(255, 255, 255)),
            Line = new Pen(new Color(20, 20, 20), 2),
        };
        return new GeometryFeature { Geometry = factory.CreatePolygon(ring), Styles = [style] };
    }

    // The in-progress line/polygon rendered with a temporary trailing point at the cursor,
    // so the user sees the next segment before they click. Circles render as-is (live radius).
    private DrawingShape RenderableInProgress()
    {
        var s = shapeInProgress!;
        if ((s.ShapeType == DrawShapeType.Line || s.ShapeType == DrawShapeType.Polygon)
            && drawHoverWorld is { } hv && s.Points.Count >= 1)
        {
            var clone = new DrawingShape
            {
                ShapeType   = s.ShapeType,
                Color       = s.Color,
                StrokeWidth = s.StrokeWidth,
                Label       = s.Label,
                FillStyle   = s.FillStyle,
            };
            clone.Points.AddRange(s.Points);
            clone.Points.Add(hv);
            return clone;
        }
        return s;
    }

    private static GeometryFeature? BuildShapeFeature(DrawingShape shape, double resolution)
    {
        try
        {
            var hex   = shape.Color.TrimStart('#');
            var r     = Convert.ToByte(hex[..2], 16);
            var g     = Convert.ToByte(hex[2..4], 16);
            var b     = Convert.ToByte(hex[4..6], 16);
            // Mapsui's Color constructor is (red, green, blue, alpha); build an opaque colour.
            var color = new Color(r, g, b);
            var factory = new NetTopologySuite.Geometries.GeometryFactory();

            // Text: a point at the anchor carrying a label style with the user's text.
            if (shape.ShapeType == DrawShapeType.Text)
            {
                if (shape.Points.Count < 1 || string.IsNullOrWhiteSpace(shape.Label)) return null;
                var anchor = factory.CreatePoint(
                    new NetTopologySuite.Geometries.Coordinate(shape.Points[0].X, shape.Points[0].Y));
                var bg = HexColor(shape.BackgroundColorHex);
                // Ground-sized text scales with zoom (pixels = height / resolution); otherwise fixed.
                var fontPx = shape.GroundHeightMetres > 0 && resolution > 0
                    ? Math.Clamp(shape.GroundHeightMetres / resolution, 6.0, 400.0)
                    : shape.FontSize;
                var labelStyle = new LabelStyle
                {
                    Text      = shape.Label,
                    ForeColor = color,
                    BackColor = bg is { } bc ? new Brush(new Color(bc.R, bc.G, bc.B, 215)) : null,
                    // Halo only when there is no background — otherwise it reads as a white glow.
                    Halo      = bg is null ? new Pen(new Color(255, 255, 255, 180), 2) : null,
                    Font      = new Font { Size = fontPx },
                };
                return new GeometryFeature { Geometry = anchor, Styles = [labelStyle] };
            }

            // A thin dark casing drawn under the coloured stroke, so any colour — even white or
            // yellow — reads clearly on any base map. Styles render in order: casing first, colour on top.
            var casing = new VectorStyle
            {
                Line = new Pen(new Color(20, 20, 20), shape.StrokeWidth + 2.0),
                Fill = null,
            };
            var main = new VectorStyle
            {
                Line = new Pen(color, shape.StrokeWidth),
                // Polygons and circles both take a fill; lines never do.
                Fill = shape.ShapeType is DrawShapeType.Polygon or DrawShapeType.Circle
                       ? ShapeFillBrush(shape.FillStyle, color) : null
            };

            NetTopologySuite.Geometries.Geometry? geom = null;
            if (shape.ShapeType == DrawShapeType.Circle && shape.RadiusMetres > 0)
            {
                var centre = new NetTopologySuite.Geometries.Coordinate(shape.Centre.X, shape.Centre.Y);
                geom = factory.CreatePoint(centre).Buffer(shape.RadiusMetres, 32);
            }
            else if (shape.Points.Count >= 2)
            {
                var coords = shape.Points.Select(p => new NetTopologySuite.Geometries.Coordinate(p.X, p.Y)).ToArray();
                if (shape.ShapeType == DrawShapeType.Polygon && coords.Length >= 3)
                    geom = factory.CreatePolygon(factory.CreateLinearRing(coords.Append(coords[0]).ToArray()));
                else
                    geom = factory.CreateLineString(coords);
            }
            if (geom is null) return null;
            return new GeometryFeature { Geometry = geom, Styles = [casing, main] };
        }
        catch { return null; }
    }

    // Parse "#RRGGBB" into a Mapsui colour, or null if empty/invalid.
    private static Color? HexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try
        {
            var h = hex.TrimStart('#');
            return new Color(Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16));
        }
        catch { return null; }
    }

    // None = no fill (outline only); Solid = translucent tint; the rest draw a hatch/dot pattern.
    private static Brush? ShapeFillBrush(DrawFillStyle fill, Color color)
    {
        if (fill == DrawFillStyle.None) return null;
        if (fill == DrawFillStyle.Solid)
            return new Brush(Color.FromArgb(40, color.R, color.G, color.B));
        var fillStyle = fill switch
        {
            DrawFillStyle.DiagonalHatch => FillStyle.BackwardDiagonal,
            DrawFillStyle.CrossHatch    => FillStyle.DiagonalCross,
            DrawFillStyle.Horizontal    => FillStyle.Horizontal,
            DrawFillStyle.Vertical      => FillStyle.Vertical,
            DrawFillStyle.Dotted        => FillStyle.Dotted,
            _                           => FillStyle.Solid,
        };
        return new Brush { FillStyle = fillStyle, Color = color };
    }


    // ── PHG coverage overlays ─────────────────────────────────────────────────

    private WritableLayer? coverageLayer;

    /// <summary>
    /// Renders PHG coverage circles on the map for the given overlays.
    /// Called from the coverage prediction window whenever the overlay list changes.
    /// </summary>
    public void ApplyCoverageOverlays(IReadOnlyList<ViewModels.CoverageOverlayViewModel> overlays)
    {
        if (coverageLayer is null)
        {
            coverageLayer = new WritableLayer { Name = "PHG Coverage", Style = null };
            // Insert below drawing layer
            MapControl.Map.Layers.Add(coverageLayer);
        }

        coverageLayer.Clear();

        foreach (var overlay in overlays)
        {
            try
            {
                // Convert lat/lon centre to world coordinates
                var worldCentre = Mapsui.Projections.SphericalMercator.FromLonLat(
                    overlay.Longitude, overlay.Latitude);

                // Radius in metres (world units ≈ metres at equator, close enough for this use)
                var radiusMetres = overlay.Phg.RangeKm * 1000.0;

                var factory = new NetTopologySuite.Geometries.GeometryFactory();
                var centre  = new NetTopologySuite.Geometries.Coordinate(
                    worldCentre.x, worldCentre.y);
                var circle  = factory.CreatePoint(centre).Buffer(radiusMetres, 48);

                var hex   = overlay.Color.TrimStart('#');
                var r     = Convert.ToByte(hex[..2], 16);
                var g     = Convert.ToByte(hex[2..4], 16);
                var b     = Convert.ToByte(hex[4..6], 16);
                var color = new Color(255, r, g, b);

                var style = new VectorStyle
                {
                    Line = new Pen(color, 1.5),
                    Fill = new Brush(Color.FromArgb(25, color.R, color.G, color.B))
                };

                var feature = new GeometryFeature
                {
                    Geometry = circle,
                    Styles   = [style]
                };
                coverageLayer.Add(feature);
            }
            catch { /* skip invalid overlay */ }
        }

        MapControl.Map.RefreshData();
        MapControl.RefreshGraphics();
    }

}