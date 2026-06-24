# APRS Command Public Data Contracts

Phase 14.6 establishes `AprsCommand.Contracts` as the stable DTO layer for future extension hooks.

These contracts are for software outside APRS Command, including:

- local REST API clients
- WebSocket dashboards
- file import/export tools
- plugin/driver integrations
- external weather, GPS, station, object, alert, or diagnostics tools

## Why Contracts Are Separate

Internal application models exist to support app behavior and can change as the desktop client grows. External integrations need a stable, versioned shape that does not expose mutable service internals or Avalonia/UI types.

The contracts project must remain safe for future external tools to reference:

- no `Aprs.Desktop` dependency
- no Avalonia dependency
- no direct references to internal service models
- simple records/classes and JSON-friendly collections

Future phases should use explicit mapping/adapters such as `IContractMapper<TInternal, TDto>` instead of returning internal models directly.

## Schema Versioning

Every DTO includes `SchemaVersion`. The current schema version is:

```text
1.0
```

Breaking contract changes should create a new schema version or compatibility adapter. REST routes, WebSocket event payloads, file imports, file exports, and plugin payloads should all preserve schema version information.

## Source Metadata

Every public DTO should include `ExternalSourceMetadata` where practical.

Source metadata identifies:

- source name
- source type
- source ID
- timestamp
- origin
- trust level

Supported source types include APRS-IS, RF, TCP KISS, Serial KISS, Direwolf, AGWPE, Replay, Simulation, Training, Weather Driver, GPS, Manual Entry, File Import, Local API, Plugin, and Unknown.

Default source metadata is unknown and untrusted.

## Validation Pattern

DTOs include:

- `ValidationWarnings`
- `ValidationErrors`
- `Notes`

Warnings and errors use `ValidationMessageDto`, which carries severity, message, optional code, and optional field name. Importers and external submit endpoints should return validation information rather than throwing uncaught exceptions or silently accepting malformed data.

## Extension Permissions

Public contracts define extension permissions:

- `ReadOnly`
- `SubmitLocalData`
- `CreateLocalObjects`
- `QueuePackets`
- `TransmitAprsIs`
- `TransmitRf`
- `Admin`

The default permission set is read-only. Transmit-related permissions are never enabled by default.

## Transmit Safety Reminder

Contracts do not grant transmit authority. Any future REST API, WebSocket command path, file import, plugin, driver, or queued packet request must pass through centralized transmit safety checks.

This applies to:

- APRS-IS transmit
- RF transmit
- iGate
- digipeater
- local beaconing
- weather beaconing
- object transmit
- message transmit
- plugin-requested packets
- API-requested packets
- file-imported packets

External hooks must not bypass transmit safety.

## Current DTOs

Phase 14.6 defines DTOs for:

- `StationUpdateDto`
- `WeatherObservationDto`
- `AprsObjectDto`
- `GpsPositionDto`
- `RawPacketDto`
- `MessageDto`
- `PortStatusDto`
- `AlertDto`
- `TransmitLogDto`
- `DecodedEventDto`
- `RfDiagnosticDto`
- `GeofenceDto`
- `TrainingScenarioDto`
- `SimulationStatusDto`
- `ReplayStatusDto`

## DTO Naming Conventions

Public DTOs use a `Dto` suffix and describe an external contract shape rather than an internal service object. DTO properties should remain JSON-friendly and avoid Avalonia, transport, file handle, serial port, or mutable service dependencies.

## Serialization Notes

Use `System.Text.Json` with web-style JSON names and string enum conversion where practical. Consumers should ignore unknown fields so APRS Command can add optional fields without breaking older integrations.

## Backward Compatibility Notes

Non-breaking changes should add optional fields. Breaking changes should use a new schema version or compatibility adapter. REST responses, WebSocket payloads, file exports, and plugin payloads should all preserve the schema version.

## DTO Examples

### StationUpdateDto

```json
{
  "schemaVersion": "1.0",
  "callsign": "SIM001",
  "displayName": "Sim Station 1",
  "latitude": 39.058333,
  "longitude": -84.508333,
  "sourceMetadata": {
    "sourceName": "Simulation",
    "sourceType": "Simulation",
    "sourceId": "sim",
    "origin": "Simulated",
    "trustLevel": "Internal"
  },
  "validationWarnings": [],
  "validationErrors": []
}
```

### WeatherObservationDto

```json
{
  "schemaVersion": "1.0",
  "stationId": "TESTWX",
  "temperature": 72,
  "humidity": 50,
  "windDirection": 180,
  "windSpeed": 5,
  "sourceMetadata": {
    "sourceName": "Example Weather Driver",
    "sourceType": "WeatherDriver",
    "sourceId": "testwx-driver",
    "origin": "Imported",
    "trustLevel": "External"
  },
  "validationWarnings": [],
  "validationErrors": []
}
```

### AprsObjectDto

```json
{
  "schemaVersion": "1.0",
  "objectName": "OBJTEST",
  "objectType": "object",
  "latitude": 39.06,
  "longitude": -84.51,
  "symbolTable": "/",
  "symbolCode": "-",
  "comment": "Example object",
  "sourceMetadata": {
    "sourceType": "FileImport",
    "origin": "Imported",
    "trustLevel": "External"
  },
  "validationWarnings": [],
  "validationErrors": []
}
```

### RawPacketDto

```json
{
  "schemaVersion": "1.0",
  "rawPacket": "N0CALL-SIM>APRS:>Example packet",
  "sourceCallsign": "N0CALL-SIM",
  "direction": "Received",
  "sourceMetadata": {
    "sourceType": "FileImport",
    "origin": "Imported"
  },
  "validationWarnings": [],
  "validationErrors": []
}
```

### AlertDto

```json
{
  "schemaVersion": "1.0",
  "alertId": "alert-sim-001",
  "alertType": "StationEnteredArea",
  "severity": "Warning",
  "summary": "SIM001 entered training area",
  "acknowledged": false,
  "sourceMetadata": {
    "sourceType": "Simulation",
    "origin": "Simulated"
  },
  "validationWarnings": [],
  "validationErrors": []
}
```

### TransmitLogDto

```json
{
  "schemaVersion": "1.0",
  "transmitId": "tx-sim-001",
  "timestamp": "2026-06-12T10:15:30+00:00",
  "packetText": "N0CALL-SIM>APRS:>Blocked example",
  "transmitType": "APRS-IS",
  "requestedBy": "Simulation",
  "permissionUsed": "ReadOnly",
  "allowed": false,
  "blockReason": "Transmit disabled by policy",
  "result": "Blocked",
  "sourceMetadata": {
    "sourceType": "Simulation",
    "origin": "Simulated"
  },
  "validationWarnings": [],
  "validationErrors": []
}
```

## Future Use

Later pre-packaging phases should use these DTOs as the shared payload model for:

- Phase 14.8 Local REST API
- Phase 14.9 WebSocket Event Streams
- Phase 14.10 File Import/Export Hooks
- Phase 14.11 Plugin/Driver Framework
- Phase 14.12 Developer Documentation and Examples

See `docs/LOCAL_REST_API.md` for the Phase 14.8 local API foundation and endpoint plan.
See `docs/WEBSOCKET_EVENT_STREAMS.md` for the Phase 14.9 WebSocket event envelope and stream plan.
See `docs/FILE_IMPORT_EXPORT_HOOKS.md` for the Phase 14.10 file import/export schemas and safety rules.
See `docs/DEVELOPER_GUIDE.md` for the Phase 14.12 developer entry point.
