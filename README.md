# APRS Command

A cross-platform APRS client for amateur radio operators, written in C# / .NET 10 with the Avalonia UI framework. Runs on macOS, Windows, and Raspberry Pi from a single codebase.

APRS Command is inspired by UI-View32, the beloved APRS client written by Roger Barker G4IDE. When Roger passed in 2004, the source code was destroyed per his wishes and the program could never be updated or ported. APRS Command is released under GPL v3 so that it can live on, be improved by the community, and never suffer the same fate.

**Developer:** James Rospopo — KE4CON  
**License:** GNU General Public License v3  
**Status:** Alpha (v0.1.0) — functional for daily use, active development

---

## Features

- **Live map** with OpenStreetMap, USGS Topo, USGS Imagery tile layers — cached for offline field use
- **APRS-IS** receive and transmit with configurable server, port, and filter
- **Position beaconing** on configurable intervals with symbol picker and PHG support
- **iGate** — RF to APRS-IS gating with packet type filtering
- **Digipeater** — fill-in and full modes, configurable aliases
- **Messages** — inbox, compose, direct message with toast notifications and sound alerts
- **Objects** — create and transmit APRS objects with visual symbol picker
- **Alert rules** — configurable triggers with sound alerts (callsign heard, weather threshold, APRS-IS disconnect, and more)
- **Station list** — all heard stations with click-to-centre on map
- **GPS** — serial NMEA input for live position
- **Exercise mode** — hard-blocks all transmit for drills, red TX badge indicator
- **SQLite station persistence** — heard stations survive restarts
- **Window state persistence** — window positions and sizes remembered between sessions
- **Serial port discovery** — USB TNCs, DigiRig, SignaLink appear automatically

---

## Platforms

| Platform | Status |
|---|---|
| macOS (Apple Silicon) | Primary development platform |
| macOS (Intel) | Supported |
| Windows 10/11 (x64) | Supported |
| Raspberry Pi (ARM64) | Supported |

---

## Building from Source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git

### Clone and build

```bash
git clone https://github.com/KE4CON/APRS-Command.git
cd APRS-Command
dotnet build CrossPlatformAprs.sln -c Release
```

### Run

```bash
dotnet run --project src/Aprs.Desktop
```

### Run tests

```bash
dotnet test tests/Aprs.Tests/Aprs.Tests.csproj
```

---

## Quick Start

1. Launch the app — if this is your first run, the setup wizard will open
2. Enter your callsign, SSID, and position in **Settings → Station**
3. Add an APRS-IS connection in **Settings → Connections** (use `rotate.aprs2.net` port `14580` with your real passcode)
4. Click **Save** — the app connects and starts receiving packets
5. Click the **📡 Beacon Now** button in the sidebar to transmit your position

For iGating and digipeating, configure those in **Settings → iGate** and **Settings → Digipeater**.

---

## Architecture

- **`src/Aprs.Core`** — APRS packet types and parser
- **`src/Aprs.Transport`** — APRS-IS client, serial KISS, TCP KISS, AGWPE transport
- **`src/Aprs.Services`** — Station database, beacon scheduler, iGate, digipeater, alert rules, GPS, weather
- **`src/Aprs.Mapping`** — Map symbols, tile providers, Mapsui integration
- **`src/Aprs.Desktop`** — Avalonia UI, composition root, viewmodels, views
- **`src/AprsCommand.Api`** — Local REST API (optional)
- **`src/AprsCommand.Contracts`** — Shared DTOs
- **`tests/Aprs.Tests`** — 900+ unit tests

---

## Dependencies

- [Avalonia UI](https://avaloniaui.net/) — cross-platform UI framework
- [Mapsui](https://mapsui.com/) — .NET mapping library
- [BruTile](https://github.com/BruTile/BruTile) — tile sources and caching
- [Microsoft.Data.Sqlite](https://docs.microsoft.com/en-us/dotnet/standard/data/sqlite/) — station persistence
- [System.IO.Ports](https://www.nuget.org/packages/System.IO.Ports/) — serial GPS and TNC

---

## Documentation

| Guide | Description |
|---|---|
| [Quick Start](docs/QUICK_START.md) | Get up and running in minutes |
| [Installation Guide](docs/INSTALLATION_GUIDE.md) | Platform-specific install instructions |
| [First-Run Setup](docs/FIRST_RUN_SETUP.md) | First-run wizard and initial configuration |
| [User Manual](docs/USER_MANUAL.md) | Complete feature reference |
| [Safety and Transmit Guide](docs/SAFETY_AND_TRANSMIT_GUIDE.md) | Transmit safety, exercise mode, and best practices |
| [APRS-IS Setup Guide](docs/APRS_IS_SETUP_GUIDE.md) | Connecting to the APRS internet network |
| [RF/TNC Setup Guide](docs/RF_TNC_SETUP_GUIDE.md) | Hardware TNC, DigiRig, and serial connections |
| [Map and Offline Maps Guide](docs/MAP_AND_OFFLINE_MAPS_GUIDE.md) | Tile sources and offline caching |
| [Messages Guide](docs/MESSAGES_GUIDE.md) | Direct messages, bulletins, and announcements |
| [Objects Guide](docs/OBJECTS_GUIDE.md) | Creating and transmitting APRS objects |
| [Weather Guide](docs/WEATHER_GUIDE.md) | Weather station integration |
| [Alerts and Geofences Guide](docs/ALERTS_AND_GEOFENCES_GUIDE.md) | Alert rules and geofence triggers |
| [Replay, Simulation and Training Guide](docs/REPLAY_SIMULATION_TRAINING_GUIDE.md) | Replay logs, simulation, and training mode |
| [RF Diagnostics Guide](docs/RF_DIAGNOSTICS_GUIDE.md) | RF port monitoring and diagnostics |
| [Logs, Events and Exports Guide](docs/LOGS_EVENTS_AND_EXPORTS_GUIDE.md) | Raw packet log, event bus, and CSV export |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Common issues and solutions |
| [Glossary](docs/GLOSSARY.md) | APRS terminology reference |
| [Developer Guide](docs/DEVELOPER_GUIDE.md) | Architecture, contributing, and extension points |
| [Installer and Package Plan](docs/INSTALLER_AND_PACKAGE_PLAN.md) | Distribution and packaging |
| [Final Release Validation Checklist](docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md) | Pre-release checklist |


---

## License

Copyright © 2026 James Rospopo (KE4CON)

APRS Command is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.

---

## Contributing

Pull requests are welcome. For significant changes, please open an issue first to discuss what you would like to change. All contributions must be compatible with GPL v3.

73 de KE4CON
