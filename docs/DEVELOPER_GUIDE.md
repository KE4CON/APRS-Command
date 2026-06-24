# APRS Command Developer Guide

This guide is the entry point for third-party developers who want to integrate safely with APRS Command.

APRS Command extension hooks are the documented boundaries that let outside tools observe APRS data, submit local data, export records, or provide future drivers without linking against mutable internal application models.

## Build Baseline

APRS Command targets .NET 10 LTS. Install the .NET 10 SDK and use the repository `global.json` when building, testing, or running examples.

```bash
dotnet --version
dotnet restore
dotnet build
dotnet test
```

## Supported Integration Methods

- Public DTO contracts in `AprsCommand.Contracts`
- Local REST API foundation
- WebSocket event streams
- File import/export hooks
- Plugin/driver framework interfaces

The safest integration is read-only. Write or submit paths are disabled by default and must validate source metadata, schema version, and permissions.

## Public Contracts

External tools should use DTOs from `AprsCommand.Contracts`, not internal service classes.

Common DTOs:

- `StationUpdateDto`
- `WeatherObservationDto`
- `AprsObjectDto`
- `GpsPositionDto`
- `RawPacketDto`
- `MessageDto`
- `AlertDto`
- `DecodedEventDto`
- `RfDiagnosticDto`
- `TransmitLogDto`

Every DTO should include:

- `schemaVersion`
- `sourceMetadata`
- `validationWarnings`
- `validationErrors`

See `docs/PUBLIC_DATA_CONTRACTS.md`.

## Source Metadata

External data must be source-tagged. Source metadata tells APRS Command where data came from and how much trust to place in it.

Use `ExternalSourceMetadata` fields:

- `sourceName`
- `sourceType`
- `sourceId`
- `timestamp`
- `origin`
- `trustLevel`

Examples of source types include `LocalApi`, `FileImport`, `Plugin`, `WeatherDriver`, `Simulation`, and `Unknown`.

## Permission Model

Extensions default to read-only.

Permissions include:

- `ReadOnly`
- `SubmitLocalData`
- `CreateLocalObjects`
- `QueuePackets`
- `TransmitAprsIs`
- `TransmitRf`
- `Admin`

Transmit-related permissions are never granted by default.

## Transmit Safety

Contracts, REST calls, WebSocket messages, file imports, and plugins cannot authorize transmit by themselves. Any future transmit request must pass central transmit safety gates.

This applies to:

- APRS-IS transmit
- RF transmit
- iGate
- digipeater
- beaconing
- weather beaconing
- object transmit
- message transmit

See `docs/EXTENSION_SAFETY_RULES.md`.

## REST API Overview

The local REST API is disabled by default, localhost-only by default, token-protected by default, and read-only by default.

Use it for local tools that need current state snapshots or carefully controlled external data submission.

See `docs/LOCAL_REST_API.md` and `examples/rest/`.

## WebSocket Overview

WebSocket streams are disabled by default and notification-only. They are for live event dashboards and developer tools.

Inbound messages are limited to safe stream controls such as ping, subscribe/filter, unsubscribe, and close.

See `docs/WEBSOCKET_EVENT_STREAMS.md` and `examples/websocket/`.

## File Import / Export Overview

File hooks are disabled by default. They support documented JSON, GeoJSON, CSV, and log/text files for local data exchange.

Imported data is tagged as `FileImport`. Imported transmit requests are blocked by default.

See `docs/FILE_IMPORT_EXPORT_HOOKS.md` and `examples/file-hooks/`.

## Plugin / Driver Framework Overview

The plugin/driver framework is a foundation for future operator-approved local extensions. Plugin loading remains disabled by default. Unsigned plugins and unapproved plugins are rejected by default.

See `docs/PLUGIN_DRIVER_FRAMEWORK.md` and `examples/plugins/`.

## Examples

Examples live in:

```text
examples/
  rest/
  websocket/
  file-hooks/
  plugins/
```

Examples use fake callsigns and fake data only.

## Safe Development Guidelines

- Use public DTOs.
- Include `schemaVersion`.
- Include `sourceMetadata`.
- Validate inputs before submitting data.
- Request the minimum permissions needed.
- Do not hardcode secrets.
- Do not log API tokens or passcodes.
- Do not assume transmit is available.
- Test offline with fake data.
- Use fake callsigns in docs and tests.
