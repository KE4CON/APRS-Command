# APRS Command v0.4.0 Release Notes

**Released:** July 2026
**Test callsign:** KE4CON-1
**License:** GPL v3

---

## What's New in v0.4.0

This release completes the first full operating milestone. APRS Command now supports end-to-end RF operation on all four connection paths, a comprehensive operator toolset, and a 44-page beginner-friendly user manual.

### RF Transmit and Receive (PRs #118, #119)

APRS Command can now transmit beacons and messages on RF in addition to receiving. All four connection paths are complete and can be active simultaneously:

- **APRS-IS** — internet-connected receive and transmit
- **KISS-TCP** — GrayWolf, Direwolf standalone, any TCP KISS source (receive + transmit)
- **Serial KISS** — hardware TNCs via USB/RS-232: Kantronics KPC-3+, TNC-Pi, Mobilinkd, DigiRig, Kenwood TM-D710 (receive + transmit)
- **Managed Modem** — Direwolf managed by the app with SignaLink/DigiRig

Beacons and messages fan out to all enabled paths simultaneously. AX.25 encoding is handled internally — no external tools required.

### iGate, Digipeater, and Weather Beacon (PR #120)

- **iGate** — RF to APRS-IS gating now active with live APRS-IS client
- **Digipeater** — RF retransmit wired to live RF client
- **Weather beacon** — transmits on RF in addition to APRS-IS when RF is enabled
- **Messages on RF** — Message Center sends on both APRS-IS and RF simultaneously
- **First-run setup wizard** — wired to live service

### Operator Tools (PRs #112-#117)

- **Packet Statistics Dashboard** — live session stats with 24-hour hourly chart, top-15 stations, packet type breakdown
- **Session Templates** — five built-in presets (Public Service, EmComm, Weekly Net, SOTA/POTA, Skywarn) plus unlimited custom templates
- **Voice Readout** — OS-native TTS with per-alert-type toggles; macOS: say, Windows: System.Speech, Linux/Pi: espeak-ng
- **Club/Net Repeater Directory** — RepeaterBook API integration (API token required; see Settings -> Connections)
- **Mobile Companion Web View** — embedded web server with live map, stations, net roster, messages, and stats on any device on the same network

### Station Setup (PRs #108-#111)

- Distance unit preference (Miles / Kilometres) — defaults to Miles
- Customizable range ring distances stored in station profile
- Station Setup layout fixes and tooltips on all settings fields

### User Manual

A 44-page beginner-friendly user manual covering all features with no assumed APRS knowledge. Includes Chapter 8: RF and TNC Connection covering all four connection paths, hardware TNC setup, GrayWolf integration, transmit settings, path selection, iGate operation, and troubleshooting.

---

## Installation

1. Download the zip for your platform below
2. Unzip to a folder of your choice
3. Run Aprs.Desktop (macOS/Linux) or Aprs.Desktop.exe (Windows)

**macOS:** If Gatekeeper warns you, right-click the app and choose Open.
**Windows:** If SmartScreen warns you, click "More info" then "Run anyway".
**Linux/Pi:** You may need to mark the binary executable: chmod +x Aprs.Desktop

---

## Platform Downloads

| Platform | File |
|---|---|
| macOS (Apple Silicon M1/M2/M3) | APRSCommand-v0.4.0-macos-arm64.zip |
| macOS (Intel) | APRSCommand-v0.4.0-macos-x64.zip |
| Windows 10/11 (64-bit) | APRSCommand-v0.4.0-windows-x64.zip |
| Linux / Raspberry Pi (ARM64) | APRSCommand-v0.4.0-linux-arm64.zip |
| Linux (x64 desktop/laptop/server) | APRSCommand-v0.4.0-linux-x64.zip |

---

## Pull Requests in This Release

| PR | Description |
|---|---|
| #108 | Distance unit preference (Miles / Kilometres) |
| #109 | Station Setup layout fix |
| #110 | Tooltips and placeholder text on all settings fields |
| #111 | Customizable range ring distances |
| #112 | Packet Statistics Dashboard |
| #113 | Detailed tooltips on all dashboard elements |
| #114 | Session Templates (5 built-in + custom creator) |
| #115 | Voice Readout — OS-native TTS with per-alert toggles |
| #116 | Club/Net Repeater Directory (RepeaterBook API) |
| #117 | Mobile Companion Web View |
| #118 | Serial KISS Coordinator — hardware TNC support |
| #119 | RF Transmit Wiring — AX.25 encoder, beacon and message transmit on RF |
| #120 | Wire null stubs — iGate, messages RF, weather beacon RF, first-run wizard |

---

## Known Limitations

- RepeaterBook directory requires an API token from repeaterbook.com — feature is fully built, token pending approval
- Winlink RMS Gateways requires an API key from Winlink administrators — feature is fully built, key pending
- RF/TNC chapter screenshots — manual chapter is written; screenshots will be added in a future point release after operator testing

---

Copyright 2026 James Rospopo (KE4CON) — Released under the GNU General Public License v3
