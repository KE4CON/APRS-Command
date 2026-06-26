using Avalonia.Controls;
using Aprs.Desktop.ViewModels;
using Aprs.Services;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
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
    private ILayer? currentBaseLayer;
    private int baseMapIndex; // cycles through BaseMapKind values on toggle
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
        }

        currentViewModel = DataContext as MapViewModel;
        if (currentViewModel is not null)
        {
            currentViewModel.PropertyChanged += ViewModel_PropertyChanged;
            currentViewModel.Markers.CollectionChanged += Markers_CollectionChanged;
            currentViewModel.NavigationRequested += OnNavigationRequested;
            currentViewModel.FindStationRequested += OnFindStationRequested;
            currentViewModel.ToggleMapLayerRequested += OnToggleMapLayerRequested;
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
        var table = marker.SymbolTableIdentifier;
        var code = marker.SymbolCode;

        // No usable APRS symbol: fall back to the colored category dot.
        if (table is null || code is null || !TryRegion(code.Value, out var region))
        {
            feature.Styles.Add(DotStyle(StationColor(marker), SymbolType.Ellipse, selected));
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
        feature.Styles.Add(new ImageStyle
        {
            Image = new Mapsui.Styles.Image { Source = sheet, BitmapRegion = region },
            SymbolScale = scale
        });

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
}
