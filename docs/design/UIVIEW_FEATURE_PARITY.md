# UI-View Feature Parity — Target Spec for APRS Command

Goal: reproduce the full feature set of the orphaned UI-View / UI-View32 APRS
client and bring it up to modern, cross-platform C# standards (.NET 10 +
Avalonia). This document inventories UI-View's features, maps each to the current
state of this repository, and gives the modern approach for each gap.

## The encouraging headline

You are much closer to UI-View parity than the running app suggests. Most of
UI-View's feature set already exists in your code at the parser/transport/service
level and is unit-tested. In weather input, replay, simulation, training, alerts,
and extensibility you already **exceed** UI-View. The real gaps are:

1. The engine is not wired to the UI (the design-time bootstrap problem — see
   `DESIGN_PROPOSAL.md` Section 1). Fixing that one thing flips most features from
   "built but dead" to "working."
2. The map is a placeholder (`DESIGN_PROPOSAL.md` Section 3).
3. About seven classic UI-View features are genuinely missing (listed below).

## Legal / sourcing note (important)

UI-View is freeware but **closed-source and copyrighted**. Reproduce its
*features and behavior*, never its code, symbol bitmaps, map calibration files,
help text, or the proprietary Precision Mapping/PMapServer data. Your `AGENTS.md`
already states this; keep to it. Use original code, the open APRS specification,
open symbol sets, and OpenStreetMap tiles.

## Status legend

- **DONE/WIRED** — implemented and connected to the UI.
- **ENGINE** — implemented and tested in the service/parser layer, but not yet
  wired to the running UI (unblocked by the bootstrap work).
- **PLACEHOLDER** — stand-in only; needs real implementation.
- **MISSING** — not present; needs building.
- **BONUS** — modern capability beyond UI-View.

---

## 1. Connectivity and TNC support

| UI-View feature | Status here | Modern approach |
|---|---|---|
| APRS-IS internet connect (servers, passcode/validation, filters) | ENGINE (`AprsIsClient`, `AprsIsServerManager`, `AprsIsLoginLineBuilder`) | Wire to UI; ship a current server list (status.aprs2.net); receive-only default. |
| KISS TNC (serial) | ENGINE (`SerialKissClient`) | Wire; add real serial-port discovery (replace `PlaceholderSerialPortDiscovery`). |
| KISS over TCP / Direwolf | ENGINE (`TcpKissClient`, `DirewolfProfileService`) | Wire; Direwolf is the modern soundcard-TNC standard, covers most users. |
| AGWPE host mode | ENGINE (`AgwpeClient`, `AgwpeFrameCodec`) | Wire. |
| BPQ host mode | MISSING | Optional; LinBPQ is still used. Add a BPQ host client if there is demand. |
| WA8DED / TF host mode | MISSING | Low priority; legacy hardware TNCs. |
| SCS PTC-II / Pactor host | MISSING | Low priority; niche today. |
| Native TNC command mode | MISSING | Low priority; KISS/Direwolf is the modern path. |
| Multiple simultaneous ports | ENGINE (`AprsPortManager`) | Wire to a port-status UI. |

Recommendation: KISS (serial + TCP), Direwolf, AGWPE, and APRS-IS cover the
overwhelming majority of modern stations. Treat BPQ/WA8DED/PTC as optional
later add-ons, not parity blockers.

## 2. Map and display

| UI-View feature | Status here | Modern approach |
|---|---|---|
| Real pannable/zoomable map | PLACEHOLDER (`SampleGrid` grid) | Mapsui + OSM tiles (`DESIGN_PROPOSAL.md` Section 3). Replaces Precision Mapping. |
| Calibrated raster maps (.inf) | n/a | Not needed; tile maps supersede this. Optionally support georeferenced overlays later. |
| GPS auto-center / follow | ENGINE (NMEA, gpsd) | Wire follow-me to the Mapsui viewport. |
| Station markers with APRS symbols | ENGINE (`AprsSymbolLookupService`) | Render as a Mapsui symbol layer. |
| Station trails / tracks | ENGINE (`StationTrailPoint`, trail config) | Draw as a line layer. |
| Range rings / ALOHA circle | MISSING | Add a ring overlay layer (configurable radius; ALOHA from heard-station stats). |
| Map overlays (lines, shapes, areas) | MISSING | Add a GeoJSON/overlay layer; ties into area objects (Section 4). |
| Night/day color modes | MISSING | Add a theme toggle (Avalonia supports light/dark). |
| Dead reckoning of moving stations | MISSING | Extrapolate position from last course/speed between packets. |

## 3. Stations

| UI-View feature | Status here | Modern approach |
|---|---|---|
| Live station list, last-heard | ENGINE (`StationDatabase`, `StationListViewModel`) | Wire; persist (SQLite). |
| Station aging / expiry | ENGINE (`StationAgingConfiguration`) | Wire. |
| Tactical labels / aliases | ENGINE (`TacticalLabel`) | Wire. |
| Find / center on station | ENGINE (select) | Wire to map center. |
| Station detail popup | ENGINE (`StationSetupView`, popup) | Wire. |
| Persisted station history | MISSING (in-memory only) | SQLite (`DESIGN_PROPOSAL.md` Section 4). |

## 4. Objects, items, messaging

| UI-View feature | Status here | Modern approach |
|---|---|---|
| APRS messaging with ACK/retry | ENGINE (`message ACK retry engine`) | Wire; transmit stays behind safety. |
| Bulletins / announcements | ENGINE | Wire. |
| Message groups / queries | ENGINE (`StoreMessageQuery`) | Wire. |
| Objects (create/edit/kill) | ENGINE (`ObjectManager`, `ObjectEditor`) | Wire; map placement exists. |
| Items | ENGINE (`ItemAprsPacket`) | Wire. |
| Area objects | MISSING | Add per APRS area-object spec; render on overlay layer. |
| Multiline objects | MISSING | Add per APRS multiline protocol; render as polylines. |
| Auto-reply / auto-answer | MISSING | Add a safe, rate-limited auto-reply (off by default). |

## 5. Beaconing and GPS

| UI-View feature | Status here | Modern approach |
|---|---|---|
| Position/status beacon (fixed/mobile) | ENGINE (`beacon formatter`, `scheduler`) | Wire; transmit behind safety. |
| SmartBeaconing (mobile) | ENGINE (`SmartBeaconing mobile mode`) | Wire. |
| NMEA GPS input | ENGINE (`NMEA GPS parsing`) | Wire. |
| gpsd support | ENGINE | Wire (good for Linux/Pi). |
| Compressed/uncompressed position | ENGINE (parser handles both) | Confirm beacon formatter emits both. |
| Scheduled beacons / commands | MISSING | Add a scheduler (cron-like) for beacons, status, server connects. |

## 6. Weather

| UI-View feature | Status here | Modern approach |
|---|---|---|
| Display APRS weather stations | ENGINE (`WeatherView`, markers) | Wire. |
| Local WX station input | BONUS — broad driver set | WeatherFlow Tempest (UDP+cloud), Peet Bros/ULTIMETER, Davis, Ambient, Ecowitt/GW1000, Cumulus/WeeWX/Weather Display imports. Exceeds UI-View. |
| WX beaconing | ENGINE (`weather beacon scheduler`) | Wire; behind safety. |
| NWS weather bulletins/alerts | MISSING | Pull NWS/Met alerts; show on map + alerts panel. |

## 7. iGate and digipeater

| UI-View feature | Status here | Modern approach |
|---|---|---|
| iGate (RF -> Internet) | ENGINE (`iGate monitor mode`, RF->IS) | Wire; gating rules; off by default. |
| Internet -> RF gating | ENGINE | Wire; conservative defaults to avoid RF flooding. |
| Digipeater (WIDEn-N, alias) | ENGINE (`safe digipeater mode`) | Wire; off by default. |

## 8. Logging, replay, diagnostics

| UI-View feature | Status here | Modern approach |
|---|---|---|
| Traffic logging | ENGINE (raw + decoded logs) | Wire; persist; rotate. |
| Log replay | ENGINE (`ReplayService`) — BONUS depth | Wire; feeds same pipeline, tagged non-live. |
| Telemetry display | ENGINE (`TelemetryAprsPacket`, parser) | Wire a telemetry chart panel. |
| RF diagnostics | BONUS (`RfDiagnosticsView`) | Wire. |
| Simulation / training | BONUS | Not in UI-View; keep, tag non-live, never transmit. |

## 9. Alerts and statistics

| UI-View feature | Status here | Modern approach |
|---|---|---|
| Proximity / event alerts, sound | ENGINE (alert rules) — BONUS depth | Wire; add sound (replace `SoundPlaceholder`). |
| Geofencing | BONUS (`GeofenceEditor`) | Wire. |
| Heard/DX statistics, band openings | MISSING | Add a stats panel (heard counts, ALOHA radius, paths). |

## 10. Extensibility (UI-View's add-on ecosystem)

UI-View's defining strength was its add-on interface (DDE/WinSock/shared memory),
which spawned dozens of community add-ons. Your modern equivalent is stronger and
safer, and is partly built:

| Capability | Status here | Modern approach |
|---|---|---|
| Public data contracts | DONE (`AprsCommand.Contracts`, 35 DTOs) | Versioned, mapped via adapters. |
| Local REST API | ENGINE (`AprsCommand.Api`) | Out-of-process integrations, any language. |
| WebSocket event streams | ENGINE | Live push to external tools. |
| File import/export hooks | ENGINE | Batch integrations. |
| Internal event bus | DONE | Backbone for the above and for UI panels. |
| In-process plugins (load .NET add-ons) | MISSING (only docs/stubs) | `IAprsPlugin` SDK + `AssemblyLoadContext` host (`DESIGN_PROPOSAL.md` Section 5). |

This combination (REST + WebSocket + file hooks + in-process plugins, all behind
the permission model and unable to bypass transmit safety) is a modern, safer
replacement for the UI-View add-on interface, and lets others build whatever they
want against documented contracts.

---

## What is genuinely missing vs UI-View (the build list)

1. Real map rendering (placeholder today) — highest user-visible gap.
2. Range rings / ALOHA circle.
3. Map overlays + area objects + multiline objects.
4. Scheduled beacons/commands/connections.
5. NWS (and international) weather alerts.
6. Heard/DX statistics panel.
7. In-process plugin loader.
8. Dead reckoning; night/day map modes; auto-reply.
9. Optional legacy TNC host modes (BPQ, WA8DED/TF, PTC) — only if there is demand.

Everything else is already implemented at the engine level and mainly needs to be
connected to a live, real (non-design-time) UI.

## Suggested order (folds into DESIGN_PROPOSAL.md sequencing)

1. Bootstrap + data pipeline (turns the ENGINE column live in one move).
2. Command bar UI.
3. Real map + markers + trails.
4. Persistence.
5. Wire iGate/digipeater/beacon/messaging/objects/weather/GPS panels to live data.
6. Plugin host.
7. The missing classics: range rings, area/multiline objects, scheduler, NWS
   alerts, stats, dead reckoning.

## Validation: "is it really UI-View-class?"

Define done as observable, end-to-end behaviors with integration tests, e.g.:
- Connect APRS-IS receive-only -> live stations on the map within seconds.
- Receive a weather packet -> WX station shows on map with current obs.
- Enable digipeater (test mode) -> a WIDE1-1 packet is correctly digipeated in a
  simulated RF loop, and a non-live (replay) packet is never transmitted.
- Drop in a sample plugin -> it appears, requests permission, and cannot transmit
  without going through centralized safety.
