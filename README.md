# APRS Command

A cross-platform APRS client for amateur radio operators, written in C# / .NET 10 with the Avalonia UI framework. Runs on macOS, Windows, Linux (x64 / ARM64), and Raspberry Pi from a single codebase.

## Philosophy

APRS Command is built on the original vision of **Bob Bruninga WB4APR**, the father of APRS. Bob began developing what became APRS in the early 1980s not as a tracking system, but as a **situational awareness tool** — a way for amateur radio operators to share real-time tactical information about what is happening in an area. Weather, resources, messages, objects, coverage — a common operating picture for emergency communications. That vision — situational awareness for emergency communications, public service events, and any operation where amateur radio operators need a common operating picture — guides every design decision in this program.

APRS Command is also inspired by **Roger Barker G4IDE**, author of UI-View32, a widely used APRS client that many operators in the community came to rely on heavily. When Roger passed in 2004, the source code was destroyed per his wishes, and the program could never be updated, fixed, or ported to new platforms. A tool that many operators depended on was left behind by the operating systems it ran on, because it was closed source.

APRS Command is released under **GPL v3** so that it can live on and be improved by the community indefinitely. No one can ever take it closed source. No one can destroy the source code. If the original author is gone tomorrow, any ham radio operator in the world can pick it up and carry it forward. That is the promise this license makes — to Bob's vision, to Roger's legacy, and to the amateur radio community that depends on these tools when it matters most.

**Developer:** James Rospopo — KE4CON  
**License:** GNU General Public License v3  
**Status:** Alpha (v0.3.0) — functional for daily use, active development

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

## Platforms & Requirements

APRS Command is a 64-bit desktop application built on .NET 10.

| Platform | Minimum version | Architecture |
|---|---|---|
| Windows | Windows 10 or 11 (64-bit) | x64 |
| macOS | macOS 14 (Sonoma) or later | Apple Silicon (M1–M4) & Intel |
| Linux | Ubuntu 22.04+, Debian 12+, Fedora, RHEL 8+ | x64 / ARM64, with an X11 or Wayland desktop |
| Raspberry Pi | Raspberry Pi OS 64-bit (Bookworm) | Pi 3, 4, 5, 400, Zero 2 W — Pi 4/5 recommended |

- **Memory:** 2 GB minimum, 4 GB or more recommended (the live map is the heaviest part).
- **Storage:** ~300 MB, plus space for logs and the offline map cache.
- **Display:** a desktop screen at 1280×768 or larger.

**Not supported:** 32-bit operating systems (32-bit Windows, or the 32-bit Raspberry Pi OS / Linux); older 32-bit ARM boards (the original Pi, Pi 1, first Pi Zero / Zero W, and the early Pi 2 v1.1); Windows 8.1 or earlier; macOS 13 or earlier; Linux distributions from before 2022; phones and tablets; and headless servers with no graphical desktop.

OS minimums follow the [.NET 10 supported-OS matrix](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).

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

The `docs/` folder is organized into a few clearly-named areas. Start with the complete guides below.

**Complete guides** (`docs/published/`) — the polished, print-ready deliverables:

| Document | What it is |
|---|---|
| [User Manual](docs/published/USER_MANUAL.docx) | The full, chapter-by-chapter manual — how to install, set up, and operate every feature |
| [Programming Guide](docs/published/PROGRAMMING_GUIDE.md) | A ground-up walkthrough of how the app is built, for developers |
| [Quick Start Guide](docs/published/QUICK_START_GUIDE.docx) | A one-sitting overview for people who won't read the full manual |
| [Fact Sheet](docs/published/APRS_Command_Fact_Sheet.docx) | One-page overview of what APRS Command is and why it exists |

**In-app Help** (`docs/help/`) — the same short, task-focused topics you can read inside the app under the **Help** menu (Installation, First-Run Setup, Messages, Objects, Weather, Maps, RF/TNC, APRS-IS, Alerts, Replay, Troubleshooting, Glossary, and more). Browse the [`docs/help/`](docs/help/) folder to read them here on GitHub.

**For contributors and maintainers:**

- **Architecture & internals** — [`docs/architecture/`](docs/architecture/) (start with the [Developer Guide](docs/architecture/DEVELOPER_GUIDE.md))
- **Build, packaging & release** — [`docs/release/`](docs/release/), including the [Installer and Package Plan](docs/release/INSTALLER_AND_PACKAGE_PLAN.md) and the [Final Release Validation Checklist](docs/release/FINAL_RELEASE_VALIDATION_CHECKLIST.md)
- **Roadmap & planning** — [`docs/planning/`](docs/planning/) · **Contributing** — [`docs/contributing/`](docs/contributing/)


---

## License

Copyright © 2026 James Rospopo (KE4CON)

APRS Command is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.

You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>.

---

## Code signing policy

**Current status:** Windows builds are being set up for code signing via **Azure Artifact Signing**, under the maintainer's own validated identity. Until that is active, Windows builds ship **unsigned** — see [INSTALLATION_GUIDE.md](docs/help/INSTALLATION_GUIDE.md) for the one-time SmartScreen bypass, which is standard for open-source software distributed outside an app store. This policy documents how signing is governed once it is in place.

- **Committers and reviewers:** [James Rospopo — KE4CON](https://github.com/KE4CON) (repository owner). Changes proposed by non-committers (pull requests) are reviewed by a maintainer before merge.
- **Approvers:** [James Rospopo — KE4CON](https://github.com/KE4CON). Every signing request is manually approved.

**Privacy policy:** APRS Command is a local desktop application with **no telemetry, analytics, crash reporting, account system, or backend server** — nothing is collected from users or sent to the maintainer. Settings and any credentials are stored only in the operator's own local profile. See [PRIVACY.md](PRIVACY.md) (full detail in [SECURITY.md](SECURITY.md)).

The complete policy — signed artifacts, build pipeline, file metadata, and account security — is in [docs/release/CODE_SIGNING_POLICY.md](docs/release/CODE_SIGNING_POLICY.md).

---

## Contributing

Pull requests are welcome. For significant changes, please open an issue first to discuss what you would like to change. All contributions must be compatible with GPL v3.

73 de KE4CON
