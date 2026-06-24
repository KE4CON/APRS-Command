# APRS Command Extension Hook Foundation

Phase 14.5 prepares APRS Command for the full pre-packaging extension hook sequence. The project direction is to complete extension hooks before Phase 15 packaging rather than deferring the runtime work until a later Phase 16. After Phase 14.12, Phase 14.13 upgrades the solution baseline to .NET 10 LTS before packaging.

## What Phase 14.5 Adds

- `AprsCommand.Contracts` for external DTO placeholders.
- Internal source/origin metadata primitives in `Aprs.Services`.
- A lightweight internal application event bus.
- A future-facing extension permission model.
- Documentation for extension safety and architecture boundaries.

This phase is intentionally foundational. The REST API, WebSocket server, file import/export runtime, plugin/driver framework, and developer examples are planned as Phase 14.6 through Phase 14.12, followed by the .NET 10 LTS baseline upgrade in Phase 14.13, all before Phase 15 packaging.

## Public DTO Contract Strategy

External integrations must not bind directly to mutable internal domain models. Public contracts live in `AprsCommand.Contracts` and are intended to be explicit, versionable DTOs.

Initial placeholder DTOs include:

- `StationUpdateDto`
- `WeatherObservationDto`
- `AprsObjectDto`
- `GpsPositionDto`
- `RawPacketDto`
- `MessageDto`
- `PortStatusDto`
- `AlertDto`
- `TransmitLogDto`

Each public DTO carries a `SchemaVersion`, source metadata, validation warnings, validation errors, and notes where appropriate. Phase 14.6 should finalize mapping/adapters between internal models and these contracts rather than exposing internal records directly.

See `docs/PUBLIC_DATA_CONTRACTS.md` for the Phase 14.6 contract strategy, DTO list, serialization expectations, validation pattern, and transmit safety reminder.

## Source Tagging Standard

All incoming, generated, imported, replayed, simulated, training, API, file, plugin, packet, station, object, weather, GPS, message, alert, and transmit-log data should carry source metadata.

Source metadata should identify:

- source name
- source type
- source ID, port ID, driver ID, or integration ID
- received or generated timestamp
- origin
- trust level

Supported source types include:

- APRS-IS
- RF
- TCP KISS
- Serial KISS
- Direwolf
- AGWPE
- Replay
- Simulation
- Training
- Weather driver
- GPS
- Manual entry
- File import
- Local API
- Plugin
- Local generated
- Unknown

Default metadata should be unknown and untrusted until a service or operator configuration proves otherwise.

## Internal Event Bus Purpose

The internal event bus is a small service abstraction for publishing application events without hardwiring future API, WebSocket, plugin, or UI consumers into core services.

Initial event types include:

- `RawPacketReceived`
- `AprsPacketParsed`
- `StationUpdated`
- `ObjectUpdated`
- `WeatherUpdated`
- `MessageReceived`
- `GpsUpdated`
- `PortStatusChanged`
- `AlertTriggered`
- `PacketTransmitted`
- `TransmitBlocked`
- `IGateDecisionMade`
- `DigipeaterDecisionMade`
- `TrainingStateChanged`
- `SimulationStateChanged`
- `ReplayStateChanged`

The event bus is not intended to become an enterprise messaging system. It should stay simple, testable, and isolated from Avalonia views.

See `docs/INTERNAL_EVENT_BUS.md` for the Phase 14.7 event bus model, categories, event types, publishing guidance, event monitor foundation, and transmit-safety boundary.

## Extension Permission Model

External software, future APIs, file imports, and plugins must default to read-only permissions.

Planned permission levels:

- `ReadOnly`
- `SubmitLocalData`
- `CreateLocalObjects`
- `QueuePackets`
- `TransmitAprsIs`
- `TransmitRf`
- `Admin`

Transmit-related permissions must never be granted implicitly. Operator configuration and centralized transmit safety checks are required before any extension can queue or request transmit-capable work.

## Pre-Packaging Extension Sequence

Phase 14.5 through Phase 14.13 complete the pre-packaging integration and runtime baseline:

- Phase 14.5: Extension Hook Foundation
- Phase 14.6: Public Data Contracts
- Phase 14.7: Internal Event Bus
- Phase 14.8: Local REST API
- Phase 14.9: WebSocket Event Streams
- Phase 14.10: File Import/Export Hooks
- Phase 14.11: Plugin/Driver Framework
- Phase 14.12: Developer Documentation and Examples
- Phase 14.13: Upgrade Project Baseline to .NET 10 LTS

Phase 14.5 should not add network listeners, WebSocket endpoints, plugin loading, file watchers, SDK packaging, or public transmit endpoints. Those runtime pieces are now scheduled immediately afterward in Phase 14.8 through Phase 14.12, before Phase 15 packaging.

## Why Hooks Move Before Packaging

Packaging and first-user setup should ship around stable integration boundaries. Completing hooks before packaging reduces later breaking changes to settings, source tagging, external contracts, local API authorization, file schemas, plugin permissions, and transmit safety enforcement.
