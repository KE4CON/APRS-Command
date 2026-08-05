# APRS Command — User Manual: Locked Outline

**Status legend:** ✅ drafted/built · ⬜ to write · 🔒 gated feature (ships disabled; document with a "not yet enabled" note)

This is the authoritative chapter list. Chapter numbers below are the **final printed sequence** and the
`order` key each JSON/function chapter uses. The builder sorts by `order`, then prints 1…N. Locked
2026-08-04; revisions use the dated-amendment model (add/adjust content without renumbering).

## Front matter
Title page · How this manual works · Table of contents · Amendments register

## Part I — Getting Started
| # | Chapter | Status | Source / tabs it covers |
|---|---|---|---|
| 1 | Welcome to APRS Command | ✅ | — |
| 2 | Installing APRS Command | ✅ | — |
| 3 | First Launch & First-Run Setup | ✅ | Settings: First Run, Station (basics), Readiness (mention) |
| 4 | A Tour of the Map Page | ✅ | — |

## Part II — The Map & Everyday Viewing
| # | Chapter | Status | Source / tabs |
|---|---|---|---|
| 5 | Moving Around the Map & Base Maps | ✅ | — |
| 6 | The Icon Sidebar Tools | ✅ | — |
| 7 | Drawing & Annotating the Map | ✅ | incl. GPX/KML import-export |
| 8 | Stations: the Station List & Details | ✅ | — |
| 9 | The Raw Packet Monitor | ✅ | — |
| 10 | Objects & Items | ⬜ | ObjectManager |
| 11 | Telemetry | ⬜ | View → Telemetry |
| 12 | Weather on the Map (stations, alerts, radar) | ⬜ | Map → Weather |

## Part III — Settings & Configuration (covers all 13 Settings tabs)
| # | Chapter | Status | Settings tab(s) |
|---|---|---|---|
| 13 | The Settings Window & Readiness | ⬜ | Readiness (+ window overview) |
| 14 | Station Identity & Beaconing | ⬜ | Station + Smart Beaconing |
| 15 | Connecting to APRS-IS & Radio | ⬜ | Connections + Ports + Managed Modem |
| 16 | Audio & Sound-Card / Direwolf | ⬜ | Audio |
| 17 | GPS & Location | ⬜ | GPS (Setup/Status) |
| 18 | Offline Maps | ✅ | Offline Maps |
| 19 | iGate | ⬜ | iGate (Setup/Status) |
| 20 | Digipeater | ⬜ | Digipeater (Setup/Status) |

*(First Run tab → Ch. 3; Message Templates tab → Ch. 21.)*

## Part IV — Messaging
| # | Chapter | Status |
|---|---|---|
| 21 | The Message Center & Templates | ⬜ |
| 22 | Broadcast, Scheduled Messages & Receipts | ⬜ |

## Part V — Operating & EmComm
| # | Chapter | Status |
|---|---|---|
| 23 | Net Control & Roster | ⬜ |
| 24 | Geofence Alerts & After-Action Reporting | ⬜ |
| 25 | Replay | ✅ |
| 26 | Simulation & Training | ⬜ |

## Part VI — Planning & Analysis Tools
| # | Chapter | Status |
|---|---|---|
| 27 | Coverage Prediction (PHG) | ⬜ |
| 28 | Elevation Profile | ⬜ |
| 29 | Frequency Reference | ⬜ |
| 30 | Packet Statistics Dashboard | ⬜ |
| 31 | Events Log & Diagnostics | ⬜ |

## Part VII — Integrations
| # | Chapter | Status | Note |
|---|---|---|---|
| 32 | RepeaterBook Repeater Lookup | 🔒 HELD | Not written yet — add if/when RepeaterBook grants permission |
| 33 | Winlink RMS Gateways | 🔒 HELD | Not written yet — add if/when a Winlink key is issued |
| 34 | The Mobile Companion Web View | ⬜ | |
| 35 | Extensions Overview (REST API, WebSocket, Plugins, File Hooks) | ⬜ | User-level only; deep dev docs → Programming Guide |

## Part VIII — Reference
| # | Chapter | Status |
|---|---|---|
| 36 | Keyboard Shortcuts | ⬜ |
| 37 | Menu Reference | ⬜ |
| 38 | Dark Mode & Appearance | ⬜ |
| 39 | Troubleshooting & FAQ | ⬜ |
| 40 | Glossary | ⬜ |
| 41 | Third-Party Licenses & Credits | ⬜ |

---

## Build batches (multi-agent pipeline)
Chapters are drafted as validated JSON by parallel agents that read the real source, then rendered
deterministically by `style.py`/`build.py`. Verify a clean build after each batch.

- **Batch 1** ✅ — 3, 5, 6, 8, 9, 18 (offline)
- **Batch 2** — 10, 11, 12, 13, 14, 15, 16
- **Batch 3** — 17, 19, 20, 21, 22, 23, 24
- **Batch 4** — 26, 27, 28, 29, 30, 31
- **Batch 5** — 34, 35 (Ch. 32 RepeaterBook & 33 Winlink are HELD — not built until permissions land)
- **Batch 6** — 36, 37, 38, 39, 40, 41

*(Chapters 1, 2, 4, 7, 25 are hand-written function chapters in `build.py`.)*
