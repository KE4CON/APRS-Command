# APRS Command User Manual

## A Note on Philosophy

APRS Command exists because of two people and one promise.

**Bob Bruninga WB4APR** invented APRS in the early 1990s. He spent decades building and refining it, and he was always clear about what it was for. APRS is not a tracking system. It is a **situational awareness tool** — a way for amateur radio operators to share real-time tactical information about what is happening in an area. Weather, resources, objects, messages, coverage — a common operating picture. Bob wanted operators — whether at an emergency scene, a public service event, or a Field Day site — to be able to look at a map and immediately understand the situation: where people are, what conditions are like, what communications are active. That vision is the foundation of this program.

**Roger Barker G4IDE** built UI-View32, a widely used APRS client that many operators came to rely on heavily over the years. When Roger passed in 2004, the source code was destroyed per his wishes, and UI-View32 could never be updated, fixed, or ported to new platforms. A program that many operators depended on was frozen in time and slowly left behind by the operating systems it ran on. The community lost access to a tool it had built workflows around, because it was closed source.

APRS Command is released under **GPL v3** — the GNU General Public License, version 3. This means the source code is always available, anyone can study it, fix it, improve it, and distribute it. No one can ever take it closed source. No one can ever destroy the source code. If the original author is gone tomorrow, any ham radio operator in the world can pick it up and carry it forward. That is the promise this license makes — to Bob's vision, to Roger's legacy, and to the amateur radio community that depends on these tools when it matters most.

Use this program as Bob intended: not just to watch dots move on a map, but to understand what is happening in your operating area. Use it during emergency activations and exercises to build and share a common operating picture. Use it at public service events — hamfests, Field Day, parades, marathons, search and rescue operations — wherever amateur radio operators need to coordinate and communicate. Use it to serve your community. That is what APRS was made for.

73 de KE4CON

---

## Overview

APRS Command is a cross-platform desktop application for amateur-radio APRS operation. It is designed around safe receive-first operation, clear station display, packet monitoring, weather display, logging, replay, and future controlled transmit workflows.

## What APRS Command Is

APRS Command is an APRS client for Windows, macOS, Linux, and Raspberry Pi. It can parse APRS packets, show stations and weather information, prepare offline map data, monitor RF/TNC inputs, and organize messages, objects, alerts, logs, replay data, and diagnostics.

## What APRS Command Can Do

APRS Command can:

- receive APRS packets from APRS-IS
- receive packet data from TCP KISS, Serial KISS, Direwolf, and AGWPE foundations
- parse positions, messages, objects, items, weather, telemetry, status, and capability packets
- show stations on a map
- show a station list
- show raw packet logs
- manage tactical labels
- show weather stations
- prepare offline map cache downloads
- store messages, objects, bulletins, announcements, and queries
- monitor alerts and geofences
- run replay, simulation, and training flows without live transmit

## What APRS Command Does Not Do By Default

APRS Command does not transmit by default. It does not enable APRS-IS transmit, RF transmit, beaconing, iGate, digipeater, object transmit, message transmit, weather beaconing, REST API, WebSocket streams, file hooks, or plugins by default.

## Important Transmit Safety Warning

You are responsible for legal amateur-radio operation. Do not enable transmit until you understand your callsign, SSID, path, beacon interval, RF hardware, APRS-IS settings, and local rules. Test receive-only first.

## System Requirements

Recommended:

- .NET 10 SDK for source builds
- Windows, macOS, Linux, or Raspberry Pi 5/Linux ARM64
- user-writable storage for logs, maps, exports, replay, and configuration
- optional internet access for APRS-IS receive and online maps
- optional serial or TCP access for TNC/Direwolf/AGWPE receive

## Installing APRS Command

Packaged installers are planned for a future release. For now, use a published folder from `artifacts/publish/<runtime-identifier>/` or run from source:

```bash
dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj
```

See `docs/INSTALLATION_GUIDE.md` for platform steps.

## First Launch

On first launch, APRS Command opens to a map-first desktop shell. The main map is the primary workspace, the station list is on the right, the packet monitor is below the map, and feature buttons are in the lower-right area.

## First-Run Setup

Open Settings, then First Run. Review:

- application data folder
- log folder
- map cache folder
- export folder
- station profile placeholder
- APRS-IS receive placeholder
- RF/TNC placeholder
- transmit-disabled safety defaults

Do not enable transmit during first-run setup.

## Main Screen Overview

The main screen is arranged for monitoring:

- map in the center
- station list on the right
- raw packet monitor below the map
- lower-right feature buttons for Messages, Objects, Weather, Events, Event Bus, Replay, RF Diagnostics, and Alerts
- status bar for application status

## In-App Help

Click the visible Help button in the application header to open the APRS Command Help window. The Help window lists topics such as User Manual, Quick Start, setup guides, Safety and Transmit Guide, Troubleshooting, and Glossary. Choose a topic from the list, or use the search box to filter by topic title or document content.

Help documents are copied into the published application under `docs/`, so they are available offline where practical. Development runs fall back to the repository `docs/` folder.

## Map Overview

The map shows station markers, object markers, weather marker preparation, selected station details, and offline map cache preparation. Select a marker to view details. If the map is blank, check storage, provider settings, internet access, and offline tile downloads.

## Station List Overview

The station list shows heard stations, display names, symbols, age state, packet source, speed, course, comments, and status text where available. Selecting a row updates the selected station details.

## Packet Monitor Overview

The packet monitor shows raw APRS packets and log entries. Use it to verify receive paths before trusting the map. Search and filter by packet text, direction, source, and packet type where available.

## Messages Overview

The Messages area organizes private messages, drafts, outbox entries, ACK/retry status, bulletins, announcements, and queries. Message transmit remains disabled until explicitly configured through safe transmit paths.

## Objects Overview

The Objects area shows APRS objects and items. Locally created object drafts can be edited and previewed, but object transmit is not automatic. Remote-owned objects should not be silently adopted or moved.

## Weather Overview

The Weather area displays APRS weather packets and future local weather station observations. Weather beacon transmit is disabled by default. Weather data may be marked stale if it stops updating.

## Events and Event Bus Overview

Events and Event Bus areas show internal application events, decoded packet events, and extension-ready event flow. These views help troubleshoot what the app received or processed.

## Replay Overview

Replay allows recorded packet data to be reviewed. Replay and simulation data are source-tagged and cannot bypass transmit safety.

## RF Diagnostics Overview

RF Diagnostics shows receive and transport information for KISS, Direwolf, Serial KISS, AGWPE, and RF-related foundations. Use receive-only testing before enabling any RF transmit feature.

## Alerts Overview

Alerts and geofences can monitor station activity, packet conditions, and area enter/exit conditions. Alerts do not transmit packets by themselves.

## Settings Overview

Settings contains first-run setup, station placeholders, safety review areas, map/cache settings, weather setup foundations, file hooks, plugins, and other configuration panels as they are implemented.

## Logs and Exports Overview

APRS Command stores packet logs, decoded event logs, exports, file hook data, replay files, and backups under the configured application data folder. Do not put credentials in exported examples or support logs.

## Where Files Are Stored

The application data folder contains:

- `config/`
- `logs/`
- `packet-logs/`
- `event-logs/`
- `maps/`
- `map-cache/`
- `exports/`
- `file-hooks/`
- `plugins/`
- `backups/`
- `training/`
- `replay/`

## How to Safely Shut Down

1. Stop APRS-IS, RF/TNC, replay, simulation, or weather receive inputs if they are running.
2. Let packet processing settle.
3. Close APRS Command.
4. Wait for the window to close.
5. Unplug serial devices or removable storage only after the app exits.
