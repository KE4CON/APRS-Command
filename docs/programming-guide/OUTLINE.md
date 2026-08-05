# APRS-Command Programming Guide Book — Locked Outline

**Audience:** developers, future maintainers, and the curious. Goal: a complete picture of *how and
why* the code was written, so the project can live on after the author. **Standard:** PhD-thesis
thoroughness in everyday-layman's language — every section answers **What it does → Why it was built
this way → How it works**, defines jargon on first use, and grounds everything in the real source.

**Format:** Markdown is the living source of truth (in-repo); styled PDF + Word are generated from it
(navy+gold, matching the User Manual). Stable section numbers; dated amendments (`AMENDS §X`, `ADDS §Z`)
with an Amendments Register — improvements are *added*, never renumbered.

**Staying accurate:** every explanation is drawn from the real source. A freshness manifest (chapter → source files) lets one `git diff` flag exactly which chapters a code change affects, so the guide never drifts from the code it documents.

**Status:** Outline locked 2026-08-05. Calibration chapter (§11, Transmit-Safety Authority) in progress
for voice approval before the rest are generated.

---

## Part I — Orientation
| § | Chapter | Anchor / source |
|---|---|---|
| 1 | What APRS Command Is, and How to Read This Book | — (incl. maintenance/amendment model) |
| 2 | The Big Picture: Architecture at 10,000 Feet | the layers and how data flows end to end |
| 3 | The Solution Layout: Projects & Boundaries | `Aprs.Core`, `Aprs.Transport`, `Aprs.Services`, `Aprs.Desktop`, test projects |

## Part II — The Core Domain (`Aprs.Core`)
| § | Chapter | Anchor / source |
|---|---|---|
| 4 | Packets as Data: the `AprsPacket` Record Hierarchy | `AprsPacket.cs`|
| 5 | Parsing: `IAprsParser` and the Parser Family | `IAprsParser.cs`, `AprsParser.cs`|
| 6 | Decoding Positions (Compressed & Uncompressed) | `AprsCompressedPositionDecoder.cs`, `AprsPositionParser.cs`|
| 7 | The Specialized Parsers | MIC-E, object/item, weather, telemetry, message parsers |

## Part III — Moving Data (`Aprs.Transport`)
| § | Chapter | Anchor / source |
|---|---|---|
| 8 | Transports: APRS-IS, KISS, Direwolf, AGWPE | `IAprsIsClient.cs` and transport clients |
| 9 | Async, Streams & Back-pressure | `ConnectAsync`/`ReadPacketsAsync`, `Channel<T>`|
| 10 | The Event Bus | `PacketParsed` / `RawPacketReceived`|

## Part IV — The Services Layer (`Aprs.Services`)
| § | Chapter | Anchor / source |
|---|---|---|
| 11 | **The Transmit-Safety Authority** (the locked door) | `TransmitSafetyAuthority.cs` — *calibration chapter* |
| 12 | Ingestion & Station State | ingestion + station database services |
| 13 | Replay, Simulation & Training | replay/simulation/training services |
| 14 | Weather, GPS, Digipeater & iGate | the peer feature services |
| 15 | Persistence & Settings | `System.Text.Json` settings store, profiles |

## Part V — The Desktop App (`Aprs.Desktop`)
| § | Chapter | Anchor / source |
|---|---|---|
| 16 | Composition & Startup: Dependency Injection | `DesktopRuntime`|
| 17 | MVVM with Avalonia | Views/ViewModels, `MapViewModel`|
| 18 | The Map: Mapsui, Layers & Drawing | map rendering + drawing engine |
| 19 | Extension Surfaces | REST API, WebSocket, plugins, file hooks |

## Part VI — Quality & Longevity
| § | Chapter | Anchor / source |
|---|---|---|
| 20 | Testing: the xUnit Suite and How to Add to It | test projects|
| 21 | How This Codebase Is Meant to Grow | design principles; adding a feature end-to-end |
| 22 | How This Book Is Maintained + Amendments Register | numbering + amendment discipline |

---

## Build plan
Same source-driven pipeline as the User Manual: agents read the real source and emit validated
structured content; a deterministic renderer produces Markdown (source of truth) + styled docx/PDF.
Generate §11 first, lock the voice, then batch the rest. Then build the guide's freshness manifest.
