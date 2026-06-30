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
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using NetTopologySuite.Geometries;
using Aprs.Desktop.Configuration;
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
    private DrawMode currentDrawMode = DrawMode.None;
    private TileLayer? radarLayer;           // single static layer (legacy, kept for compat)
    private Mapping.WmsRadarUrlBuilder? radarUrlBuilder;
    private readonly List<TileLayer> radarFrameLayers = [];                          // animation frames
    private readonly List<Mapping.WmsRadarUrlBuilder> radarFrameUrlBuilders = [];     // one builder per frame
    private Avalonia.Threading.DispatcherTimer? radarAnimTimer;
    private int currentFrameIndex;
    private ILayer? currentBaseLayer;
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
    }

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
            currentViewModel.RingCenterChanged += OnRingCenterChanged;
        }

        UpdatePanels();
        RefreshFeatures();
    }

    private void Markers_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshFeatures();
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

    /// <summary>Draws range rings at 10, 25, and 50 mile intervals around the operator's station.</summary>
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
        }

        // Ring distances in miles
        var ringMiles = new[] { (10, "#60a5fa", "10 mi"), (25, "#34d399", "25 mi"), (50, "#f87171", "50 mi") };

        foreach (var (miles, color, _) in ringMiles)
        {
            var radiusMeters = miles * 1609.344;
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

        // Handle draw tool clicks before station selection.
        if (currentDrawMode != DrawMode.None && e.GetMapInfo([])?.WorldPosition is { } worldPos)
        {
            OnDrawToolClick(new MPoint(worldPos.X, worldPos.Y));
            return;
        }

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
        currentDrawMode = mode;
        shapeInProgress = null;
        circleFirstClick = true;
        MapControl.Cursor = mode == DrawMode.None
            ? Avalonia.Input.Cursor.Default
            : new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Cross);
    }

    private void OnClearDrawings(object? sender, EventArgs e)
    {
        completedShapes.Clear();
        shapeInProgress = null;
        drawingLayer?.Clear();
        MapControl.Map.RefreshData();
        MapControl.RefreshGraphics();
    }

    private void OnDrawToolClick(MPoint worldPoint)
    {
        if (currentDrawMode == DrawMode.None || drawingLayer is null) return;
        switch (currentDrawMode)
        {
            case DrawMode.Line:    HandleLineClick(worldPoint);    break;
            case DrawMode.Polygon: HandlePolygonClick(worldPoint); break;
            case DrawMode.Circle:  HandleCircleClick(worldPoint);  break;
            case DrawMode.Erase:   EraseShapeAt(worldPoint);       break;
        }
    }

    private void HandleLineClick(MPoint pt)
    {
        if (shapeInProgress is null)
        {
            shapeInProgress = new DrawingShape { ShapeType = DrawShapeType.Line };
            shapeInProgress.Points.Add((pt.X, pt.Y));
        }
        else
        {
            shapeInProgress.Points.Add((pt.X, pt.Y));
            RenderShapePreview();
        }
    }

    private void HandlePolygonClick(MPoint pt)
    {
        shapeInProgress ??= new DrawingShape { ShapeType = DrawShapeType.Polygon };
        shapeInProgress.Points.Add((pt.X, pt.Y));
        RenderShapePreview();
    }

    private bool circleFirstClick = true;
    private void HandleCircleClick(MPoint pt)
    {
        if (circleFirstClick)
        {
            shapeInProgress = new DrawingShape { ShapeType = DrawShapeType.Circle };
            shapeInProgress.Centre = (pt.X, pt.Y);
            circleFirstClick = false;
        }
        else
        {
            var dx = pt.X - shapeInProgress!.Centre.X;
            var dy = pt.Y - shapeInProgress.Centre.Y;
            shapeInProgress.RadiusMetres = Math.Sqrt(dx * dx + dy * dy);
            FinaliseShape(shapeInProgress);
            circleFirstClick = true;
        }
    }

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

    public void FinaliseCurrentShape()
    {
        if (shapeInProgress is null || shapeInProgress.Points.Count < 2) { shapeInProgress = null; return; }
        FinaliseShape(shapeInProgress);
    }

    private void FinaliseShape(DrawingShape shape) { completedShapes.Add(shape); shapeInProgress = null; RedrawAllShapes(); }
    private void RenderShapePreview() => RedrawAllShapes();

    private void RedrawAllShapes()
    {
        if (drawingLayer is null) return;
        drawingLayer.Clear();
        var all = completedShapes.ToList();
        if (shapeInProgress is not null) all.Add(shapeInProgress);
        foreach (var shape in all) { var f = BuildShapeFeature(shape); if (f is not null) drawingLayer.Add(f); }
        MapControl.Map.RefreshData();
        MapControl.RefreshGraphics();
    }

    private static GeometryFeature? BuildShapeFeature(DrawingShape shape)
    {
        try
        {
            var hex   = shape.Color.TrimStart('#');
            var r     = Convert.ToByte(hex[..2], 16);
            var g     = Convert.ToByte(hex[2..4], 16);
            var b     = Convert.ToByte(hex[4..6], 16);
            var color = new Color(255, r, g, b);
            var style = new VectorStyle
            {
                Line = new Pen(color, shape.StrokeWidth),
                Fill = shape.ShapeType == DrawShapeType.Polygon
                       ? new Brush(Color.FromArgb(40, color.R, color.G, color.B))
                       : null
            };
            var factory = new NetTopologySuite.Geometries.GeometryFactory();
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
            return new GeometryFeature { Geometry = geom, Styles = [style] };
        }
        catch { return null; }
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