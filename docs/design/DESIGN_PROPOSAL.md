# APRS Command — Design Proposal

This document proposes a design for finishing APRS Command. It covers four
things you asked for — an overall design, moving the feature buttons into a top
menu/command bar, a real plugin/extension hook system, and a Help button linked
to the user manual — plus other improvements found while reviewing the source.

It is written so it can be handed directly to a developer or an AI coding agent.
Each section states the goal, the current state in your repo, the proposed
design, and concrete acceptance criteria.

---

## 0. The one thing that changes everything

Almost all of the hard work in this project is already done and is good:

- `Aprs.Core` has a complete, test-covered APRS packet parser.
- `Aprs.Transport` has working APRS-IS, TCP KISS (Direwolf), Serial KISS, and
  AGWPE clients with real async networking and frame codecs.
- `Aprs.Services` has the station database, beacon scheduler, messaging, object
  manager, weather drivers, alerts, replay, and a centralized transmit-safety
  path (`AprsPortManager` / `AprsPortTransmitSafetyResult`).
- `Aprs.Mapping` has tile math, a tile cache, and an offline download manager.
- `AprsCommand.Contracts` has 35 versioned DTOs and an extension permission model.

The problem is the **last mile was never built**. The desktop app boots like this:

```
App.OnFrameworkInitializationCompleted()
  -> MainWindowViewModel.CreateDesignTime()
     -> new MainWindowViewModel(MapViewModel.CreateDesignTime())
        -> fills every sub-panel with XxxViewModel.CreateDesignTime() sample data
```

`MainWindowViewModel` already has a second, full constructor that takes all the
real sub-view-models — but **nothing ever calls it**, there is **no dependency
injection container**, and nothing in `src/Aprs.Desktop` ever constructs a real
`AprsIsClient`, `StationDatabase`, or KISS client. So the running application is
a UI shell populated with sample data. It cannot connect to APRS-IS or show live
stations, regardless of how complete the lower layers are.

This is also why an AI agent kept "completing" features without the app ever
working: every phase added a feature plus its `CreateDesignTime()` sample data
and green unit tests, but the integration step that wires real services to the
UI was never a task, so it never happened.

**The spine of this design is building that integration layer (Section 1). The
map, persistence, menu bar, plugins, and help all attach to it.**

---

## 1. Application bootstrap and composition root (highest priority)

**Goal.** The running app constructs real services, wires the data pipeline
(transport -> parser -> station database -> map/station list), and uses the real
`MainWindowViewModel` constructor. `CreateDesignTime()` is used only by the XAML
previewer.

**Current state.** No DI; `App` uses `CreateDesignTime()`; sub-VMs default to
sample data.

**Proposed design.**

1. Add `Microsoft.Extensions.DependencyInjection` (and optionally
   `Microsoft.Extensions.Hosting`) to `Aprs.Desktop`.

2. Create `src/Aprs.Desktop/CompositionRoot.cs` that registers every layer:

   ```csharp
   public static class CompositionRoot
   {
       public static IServiceProvider Build()
       {
           var services = new ServiceCollection();

           // Core
           services.AddSingleton<IAprsParser, AprsParser>();

           // Services / domain
           services.AddSingleton<IStationDatabase, StationDatabase>();
           services.AddSingleton<IAprsEventBus, AprsEventBus>();
           services.AddSingleton<IApplicationEventBus, ApplicationEventBus>();
           services.AddSingleton<IAprsPortManager, AprsPortManager>();
           // ... messaging, objects, weather, alerts, replay, beacon scheduler

           // Transport (constructed but idle until the user connects)
           services.AddSingleton<IAprsIsClient, AprsIsClient>();
           services.AddSingleton<ITcpKissClient, TcpKissClient>();
           services.AddSingleton<ISerialKissClient, SerialKissClient>();
           services.AddSingleton<IAgwpeClient, AgwpeClient>();

           // Mapping
           services.AddSingleton<IMapTileCacheService, MapTileCacheService>();
           services.AddSingleton<IOfflineMapDownloadManager, OfflineMapDownloadManager>();

           // View models (real, not design-time)
           services.AddSingleton<MapViewModel>();
           services.AddSingleton<StationListViewModel>();
           // ... one registration per panel VM
           services.AddSingleton<MainWindowViewModel>();

           return services.BuildServiceProvider();
       }
   }
   ```

3. Rewrite `App.OnFrameworkInitializationCompleted()`:

   ```csharp
   if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
   {
       var provider = CompositionRoot.Build();
       desktop.MainWindow = new MainWindow
       {
           DataContext = provider.GetRequiredService<MainWindowViewModel>()
       };
   }
   ```

4. Build the **runtime data pipeline** as a hosted service / coordinator
   (`AprsIngestionService`):

   ```
   transport raw line/frame
     -> IAprsParser.Parse(...)
        -> tag with AprsPacketSource (Live / Replay / Simulation / Imported)
           -> IStationDatabase.ApplyPacket(...)
              -> raise StationUpdated on IAprsEventBus
                 -> MapViewModel + StationListViewModel + RawPacketLogViewModel subscribe
   ```

   The event bus already exists, so panels subscribe rather than being pushed to.
   Replay and simulation feed the *same* pipeline but carry a non-live source tag
   so the transmit-safety layer rejects any transmit attempt from them (this is
   already a documented rule in `AGENTS.md`).

5. Keep `CreateDesignTime()` strictly for design mode:

   ```csharp
   DataContext = Design.IsDesignMode
       ? MainWindowViewModel.CreateDesignTime()
       : provider.GetRequiredService<MainWindowViewModel>();
   ```

**Acceptance criteria.**
- Launching the app with no sample data shows an empty station list/map.
- Connecting to APRS-IS in receive-only mode causes real stations to appear in
  the list and on the map within seconds.
- An integration test feeds a canned APRS-IS transcript through the pipeline and
  asserts the station database contains the expected callsigns.

---

## 2. Feature buttons → top command bar

**Goal.** Replace the 2x4 button grid in the lower-right `FeaturePanel` with a
horizontal command/menu bar across the top, like a menu bar. Keep the map as the
primary workspace.

**Current state.** `MainWindow.axaml` row 0 is a dark header (title + Help +
status chips). Row 1 is a 2x2 area: Map (top-left), StationList (top-right),
PacketMonitor (bottom-left), and `FeaturePanel` (bottom-right) which holds the
eight buttons (`OpenMessagesCommand` ... `OpenAlertsCommand`) plus the selected
feature content. The view-model side is already correct: `SelectFeature`,
`SelectedFeatureIndex`, and the derived `SelectedFeatureName/Description/Content`
all raise `PropertyChanged`, so only the XAML needs to change.

**Proposed design.** Add a command-bar row between the header and the workspace.
Use `ToggleButton`s in a single group so the active feature is highlighted, each
bound to its existing command. The selected feature's content moves into a
right-hand dock with a `GridSplitter` so it is resizable and never covers the map.

Replacement layout for `MainWindow.axaml` (row structure becomes
`Auto,Auto,*,Auto` = header, command bar, workspace, status bar):

```xml
<!-- Row 1: command bar -->
<Border Grid.Row="1" Background="#0F172A" Padding="8,4">
  <WrapPanel Orientation="Horizontal" ItemSpacing="4">
    <ToggleButton Content="Messages"  Command="{Binding OpenMessagesCommand}"
                  IsChecked="{Binding SelectedFeatureIndex, Converter={StaticResource FeatureIndexToBool}, ConverterParameter=0}"/>
    <ToggleButton Content="Objects"   Command="{Binding OpenObjectsCommand}"   .../>
    <ToggleButton Content="Weather"   Command="{Binding OpenWeatherCommand}"   .../>
    <ToggleButton Content="Events"    Command="{Binding OpenEventsCommand}"    .../>
    <ToggleButton Content="Event Bus" Command="{Binding OpenEventBusCommand}"  .../>
    <ToggleButton Content="Replay"    Command="{Binding OpenReplayCommand}"    .../>
    <ToggleButton Content="RF Diag"   Command="{Binding OpenRfDiagnosticsCommand}" .../>
    <ToggleButton Content="Alerts"    Command="{Binding OpenAlertsCommand}"    .../>
  </WrapPanel>
</Border>

<!-- Row 2: workspace, map-first with a resizable feature dock on the right -->
<Grid Grid.Row="2" Margin="12"
      ColumnDefinitions="*,Auto,340" RowDefinitions="*,240" RowSpacing="10">
  <views:MapView Grid.Column="0" DataContext="{Binding Map}"/>
  <views:PacketMonitorView Grid.Row="1" Grid.Column="0" DataContext="{Binding RawPacketLog}"/>

  <GridSplitter Grid.Column="1" Grid.RowSpan="2" Width="6"/>

  <Grid Grid.Column="2" Grid.RowSpan="2" RowDefinitions="Auto,*" RowSpacing="10">
    <views:StationListView DataContext="{Binding StationList}"/>
    <Border Grid.Row="1" ...>           <!-- selected feature dock -->
      <DockPanel>
        <StackPanel DockPanel.Dock="Top">
          <TextBlock Text="{Binding SelectedFeatureName}" .../>
          <TextBlock Text="{Binding SelectedFeatureDescription}" .../>
        </StackPanel>
        <ScrollViewer>
          <ContentControl Content="{Binding SelectedFeatureContent}"/>
        </ScrollViewer>
      </DockPanel>
    </Border>
  </Grid>
</Grid>
```

A tiny value converter (`FeatureIndexToBool`) returns true when
`SelectedFeatureIndex` equals the button's parameter so the active button stays
lit. Notes: a real `Menu` with drop-downs is also possible, but a row of toggle
buttons matches the existing eight-feature model and needs no VM changes. The
`SelectedFeatureContent` `ContentControl` reuses the `DataTemplates` already in
`MainWindow.axaml`. Keep the existing Help button and status chips in the header.

**Acceptance criteria.**
- The eight buttons render across the top; the lower-right `FeaturePanel` button
  grid is gone.
- Clicking a button highlights it, updates the feature title/description, and
  swaps the docked content without hiding the map.
- The feature dock is resizable via the splitter; the map stays primary.

---

## 3. Real map rendering (Mapsui)

**Goal.** Replace the placeholder grid with a real, pannable/zoomable tile map;
draw stations, objects, and weather as markers at correct geographic positions;
support offline tiles via the existing cache.

**Current state.** `MapView.axaml` draws a hard-coded 6x7 grid of border lines on
a green background with a `MarkerCanvas` overlay. `MapViewModel.CreateDefaultProvider`
returns a provider literally named `"SampleGrid"` with an empty URL and the text
"Placeholder grid, no external map tiles loaded." Marker positions come from
`PlaceholderMapCoordinateConverter`, which does a naive linear projection
(`lon = x*360-180`, `lat = 90-y*180`) instead of Web Mercator. The full tile
stack in `Aprs.Mapping` (`MapTileCacheService`, `MapTileCalculationService`,
`OfflineMapDownloadManager`) exists but is never connected to the view.

**Proposed design.**

1. Add `Mapsui.Avalonia` to `Aprs.Mapping` and `Aprs.Desktop`. Mapsui has
   first-class Avalonia 11 support and handles Web Mercator, panning, and zoom.

2. Replace the `MapView` body with a Mapsui `MapControl`:
   - **Base layer:** an OSM `TileLayer`. Point its HTTP fetcher at your existing
     `IMapTileProvider` URL template, and wire Mapsui's persistent cache to your
     `IMapTileCacheService` so already-downloaded tiles work offline. This makes
     the offline-download manager you already built actually useful.
   - **Marker layers:** one `MemoryLayer` each for stations, objects, and weather.
     Convert your `StationMarker`/`ObjectMarker`/`WeatherStationMarker` records to
     Mapsui features at their real lat/lon. Replace
     `PlaceholderMapCoordinateConverter` with `SphericalMercator.FromLonLat`.
   - **Symbols:** render APRS symbols from `AprsSymbolLookupService` (you already
     have the lookup and Maidenhead grid logic).

3. Map interaction: hit-test taps to select a station/object (your
   `MapViewModel.SelectStation` / `HandleMapClick` / `PlaceObjectAt` already
   exist); feed the tap's projected lat/lon into them.

4. Provide a real default provider (OSM) and keep `AllowInternetTileDownload`
   defaulting to the current safe value; respect tile-server usage policy and set
   a proper `User-Agent` and attribution.

**Acceptance criteria.**
- The map shows real OSM tiles, pans, and zooms.
- A station at a known lat/lon renders at the correct pixel after projection.
- With internet disabled but tiles cached, the cached area still renders.

---

## 4. Persistence (SQLite)

**Goal.** Persist stations, trails, messages, objects, and logs across restarts;
honor the README's promise of local storage.

**Current state.** `StationDatabase` keeps everything in in-memory
`Dictionary<>`s; nothing writes station/message/object data to disk. Only folder
setup (`ApplicationFolderSetupService` / `ApplicationFolderLayout`) touches disk.

**Proposed design.**

1. Add `Microsoft.Data.Sqlite` (lightweight) or EF Core (more tooling) to
   `Aprs.Services`. SQLite is recommended for a desktop app this size.

2. Introduce repository interfaces (`IStationRepository`, `IMessageRepository`,
   `IObjectRepository`, `IPacketLogRepository`) and SQLite implementations. Store
   the database file under `ApplicationFolderLayout` (the data folder you already
   compute), e.g. `data/aprs-command.db`.

3. Keep `StationDatabase` as an in-memory **hot cache** for speed, but have it
   write-through to the repository and hydrate from it on startup. This preserves
   the existing fast read path and the existing tests.

4. Add schema migration on startup (a simple version table is enough). Add a
   retention/pruning policy for packet logs so the DB does not grow without bound.

**Acceptance criteria.**
- Stations and trails received in one session are present after a restart.
- Messages and objects persist.
- A repository round-trip test inserts, restarts (new connection), and reads back.

---

## 5. Extension / plugin hooks (so others can build add-ons)

**Goal.** Let third parties develop and drop in extensions without recompiling the
app, while keeping the safety model (extensions can never bypass transmit safety).

**Current state.** You have a strong *contract* layer (`AprsCommand.Contracts`),
an `ExtensionPermission` model, a local REST API foundation, WebSocket event
streams, file import/export hooks, and an internal event bus. You have plugin
*documentation* and example stubs (`examples/plugins/*.md`,
`plugin-manifest.example.json`). What is missing is the actual in-process
**plugin host** — there is no `IAprsPlugin` interface and no assembly loader.

There are two complementary hook surfaces; the design keeps both:

- **Out-of-process hooks (already built):** REST API, WebSocket events, file
  import/export. Best for integrations in any language, and the safest because
  they cannot touch app internals. Recommend these as the default path.
- **In-process plugins (to build):** .NET assemblies the app loads at runtime for
  richer capabilities (custom weather drivers, exporters, alert-rule providers,
  training-scenario providers). This is what is proposed below.

**Proposed design.**

1. New project `AprsCommand.Plugins.Sdk` (depends only on
   `AprsCommand.Contracts`). Define the entry interface and capability
   interfaces:

   ```csharp
   public interface IAprsPlugin
   {
       string Id { get; }
       string DisplayName { get; }
       ContractSchemaVersion SupportedSchema { get; }
       void Initialize(IPluginHostContext host);
   }

   // capability interfaces a plugin may also implement:
   public interface IWeatherInputDriverPlugin   { /* push WeatherObservationDto */ }
   public interface IStationExporterPlugin       { /* export StationUpdateDto[]   */ }
   public interface IAlertRuleProviderPlugin     { /* supply AlertDto rules        */ }
   public interface ITrainingScenarioProviderPlugin { /* supply TrainingScenarioDto */ }
   ```

   `IPluginHostContext` exposes only safe, contract-typed surfaces: a read-only
   event feed (subscribe to station/weather/alert DTOs), a logger, the plugin's
   granted `ExtensionPermission` set, and a **request-transmit** method that
   routes through the existing centralized safety (`AprsPortManager`) and returns
   a `AprsPortTransmitSafetyResult`. There is no direct transport access.

2. **Discovery & manifest.** Reuse your `plugin-manifest.example.json` schema.
   The host scans a `plugins/` folder under the app data dir; each plugin is a
   subfolder with a `manifest.json` (id, version, entry assembly, requested
   permissions, supported schema version).

3. **Isolation.** Load each plugin in its own collectible `AssemblyLoadContext`
   so plugins can be enabled/disabled/unloaded at runtime and cannot clash on
   dependency versions. Validate the manifest's schema version against
   `ContractSchemaVersion` and refuse incompatible plugins with a clear message.

4. **Permission gating.** A plugin only receives the capabilities its manifest
   requests *and* the user has approved, expressed through the existing
   `ExtensionPermission` / `ExtensionPermissionDefaults`. Default-deny. Transmit
   permission, even if granted, still passes through centralized safety, so
   "plugins cannot bypass transmit safety" remains literally true.

5. **UI.** Add an Extensions/Plugins feature panel (fits the new command bar):
   list discovered plugins, show requested vs granted permissions, enable/disable,
   and view per-plugin logs. Keep plugin loading **disabled by default**, matching
   your current safety posture.

6. **Versioning.** Treat the SDK + Contracts as your public API. Bump
   `ContractSchemaVersion` only with explicit mapping/adapters, as `AGENTS.md`
   already requires.

**Acceptance criteria.**
- A sample "echo weather driver" plugin dropped in `plugins/`, enabled by the
  user, pushes a `WeatherObservationDto` that appears in the weather panel.
- A plugin requesting transmit without user grant is blocked; with grant, its
  transmit still goes through `AprsPortManager` safety and is logged.
- Disabling a plugin unloads it without restarting the app.

---

## 6. Help button and User Manual

**Goal.** A Help button linked to the user manual.

**Current state — mostly done already.** The Help button exists in the header and
is bound to `OpenHelpCommand`; `HelpView`/`HelpWindow` render an offline help
viewer; `HelpDocumentService` already loads `docs/USER_MANUAL.md` plus ~16 other
guides, and `docs/USER_MANUAL.md` already exists (160 lines, full outline). Missing
topics fall back to a friendly "not available in this build" message instead of
crashing.

So the remaining work is small:

1. Confirm the Help button works once the app boots the real view model
   (Section 1). It is wired correctly; it just needs the app to run the real VM.
2. **Revise the manual to match reality after Sections 1–3 land.** The current
   manual says things like "show stations on a map," which only becomes true once
   the pipeline and real map exist. Update wording from aspirational to accurate,
   and add short sections for the new command bar and the plugins panel.
3. Optionally add a Help entry point in the command bar too (not just the header)
   for discoverability.

**Acceptance criteria.**
- Clicking Help opens the viewer with the User Manual selected and readable.
- The manual's feature descriptions match what the app actually does.

---

## 7. Other improvements worth making

- **Bind the header status chips.** "APRS-IS Offline" and "TX Disabled" are
  hard-coded `TextBlock`s. Bind them to real connection state and the transmit
  safety state so they reflect reality. (Otherwise users cannot trust them.)
- **Real serial-port discovery.** Replace `PlaceholderSerialPortDiscovery` with
  `SerialPort.GetPortNames()` plus platform device scanning so the TNC port list
  populates instead of requiring a typed device name.
- **Structured logging to disk.** The docs reference a `logs/` folder; add
  `Microsoft.Extensions.Logging` (or Serilog) writing to that folder, and route
  `StartupDiagnostics` through it.
- **Integration tests for the data pipeline.** This is the single best defense
  against the regression pattern that has dogged the project: a test that pushes a
  canned APRS-IS transcript end-to-end and asserts stations/markers appear. Unit
  tests alone let placeholders pass as "done."
- **Stop shipping duplicate packages.** The `-test` archives are byte-for-byte
  identical to the release archives; either make them meaningfully different
  (e.g., debug symbols, sample data) or drop them.
- **Set a real version and license.** Replace `0.0.0-dev` and
  `LICENSE_PLACEHOLDER.txt`. Because the app is "inspired by UI-View32," keep code
  original (you already note this) and pick a license deliberately; GPL-family and
  MIT are both common in ham-radio software — this is your call, not a technical one.
- **Add CI (GitHub Actions).** Build + `dotnet test` on push, and produce the
  per-platform publishes so releases are reproducible.
- **Replace the remaining service placeholders** (`SoundPlaceholder`,
  `SmsPlaceholder`, `EmailPlaceholder`, `FileImportPlaceholder`) with real
  implementations or remove them from the UI until implemented, so users are not
  offered features that do nothing.

---

## 8. Suggested sequencing

1. **Bootstrap + composition root + data pipeline (Section 1).** Nothing else is
   verifiable until the real app runs. Land the end-to-end integration test here.
2. **Command bar (Section 2).** Small, self-contained, no VM changes; good early win.
3. **Real map (Section 3).** Now that live stations exist, the map has something
   real to show.
4. **Persistence (Section 4).** Make that live data survive restarts.
5. **Plugin host (Section 5).** Build on the now-working pipeline and safety path.
6. **Manual revision + polish (Sections 6–7).**

---

## 9. How to make an AI agent succeed this time

The recurring failure was task-by-task work where a placeholder satisfied a
task's acceptance criteria and the agent moved on. To break that loop:

- Give the agent **one verifiable goal at a time**, phrased as an observable
  behavior, not a component. Example: "When the app connects to a fake APRS-IS
  feed, station N0CALL appears in the station list and as a marker on the map,"
  with a test that asserts it — not "add a map view."
- Require an **integration test** for any feature that claims to display live
  data. A feature is not done until a test exercises it through the real pipeline.
- Forbid `CreateDesignTime()` as a runtime data source in review.
- Work against the repo with `dotnet build` and `dotnet test` green on every step.
