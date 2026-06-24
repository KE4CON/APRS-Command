# Live Bootstrap — branch `feature/live-bootstrap`

This branch fixes the core problem: the desktop app booted `MainWindowViewModel.CreateDesignTime()`
and ran entirely on sample data, with the real engine never connected. It now boots a real
composition root, constructs the real `MainWindowViewModel`, and runs a live APRS-IS receive
pipeline that feeds the station database, map, station list, and packet monitor.

## What changed

New files:
- `src/Aprs.Services/Runtime/AprsIngestionService.cs` — transport-agnostic receive pipeline:
  raw line -> raw packet log -> parser -> station database -> `PacketIngested` event.
- `src/Aprs.Desktop/Composition/DesktopRuntime.cs` — DI container + real `MainWindowViewModel`
  + `LiveDataCoordinator`. Spine panels are live; other panels still use `CreateDesignTime()`
  and are marked `// TODO` for follow-up wiring.
- `src/Aprs.Desktop/Runtime/LiveDataCoordinator.cs` — UI-thread bridge: coalesced 500 ms refresh
  of the live view models, plus `ConnectAprsIsReceiveOnly(...)`.

Edited files:
- `src/Aprs.Desktop/App.axaml.cs` — boots `DesktopRuntime` at runtime; keeps
  `CreateDesignTime()` only under `Design.IsDesignMode` (the XAML previewer).
- `src/Aprs.Desktop/ViewModels/MapViewModel.cs` — added `UpdateStations(...)` to refresh markers.
- `src/Aprs.Desktop/ViewModels/StationListViewModel.cs` — rebuilds when live markers change.
- `src/Aprs.Desktop/Views/MapView.axaml.cs` — re-renders on `Markers.CollectionChanged`.
- `src/Aprs.Desktop/Aprs.Desktop.csproj` — added `Microsoft.Extensions.DependencyInjection`.

## What is live now

- APRS-IS receive (receive-only, passcode `-1`, never transmits).
- Station database populated from real packets.
- Station list (right panel) — live.
- Raw packet monitor (below the map) — live.
- Map station markers — live (still on the placeholder grid using the placeholder projection;
  real tiles + Web Mercator are the next step, `DESIGN_PROPOSAL.md` Section 3).

## What is still sample data (follow-up)

Every other feature panel (weather, messages, objects, alerts, replay, etc.) is still built from
`CreateDesignTime()`. Each is marked `// TODO` in `DesktopRuntime.Create()`. Wire each to its real
service using the same pattern: construct the real service (or resolve from DI), build the real
view model, and refresh it from the ingestion event or its own service events.

## How to build and verify (on your machine — I had no .NET SDK to compile here)

```bash
git checkout feature/live-bootstrap
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj
```

Expected on launch: the app connects to `rotate.aprs2.net` receive-only and, within a few seconds,
real callsigns appear in the station list and packet monitor, with markers on the map. No sample
stations.

## Notes / things you may need to adjust

- **DI package version.** `Microsoft.Extensions.DependencyInjection` is pinned to `10.0.0`. If
  `dotnet restore` cannot find that exact version, change it to the latest 10.0.x your SDK offers.
- **Auto-connect.** `DesktopRuntime.Start()` auto-connects receive-only with callsign `N0CALL`.
  Replace with your real callsign, or comment out the `ConnectAprsIsReceiveOnly` call to require a
  manual connect. Receive-only never transmits.
- **Feed volume.** No APRS-IS filter is set, so the server default feed applies. Add a filter
  (e.g. a range filter around your location) in `LiveDataCoordinator` if you want less traffic.
- **Map markers** use the existing placeholder projection until the Mapsui work lands, so positions
  are approximate and drawn on the placeholder grid. The station list and packet monitor are exact.
- **Threading** is single-threaded by design: incoming lines are marshalled to the UI thread before
  touching the station database or log, and the refresh timer runs on the UI thread.
