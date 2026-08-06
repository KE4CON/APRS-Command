# APRS Command File Import / Export Hooks

Phase 14.10 adds safe file-based import and export hooks for APRS Command.

The file hook system is intended for local tools, scripts, dashboards, map overlays, and future plugin/driver support that need documented files rather than direct process integration.

It is not a plugin loader, command runner, or transmit path.

## Safe Defaults

File hooks default to conservative settings:

- file hooks enabled: `false`
- import enabled: `false`
- export enabled: `false`
- reject invalid imports: `true`
- imported transmit requests: `false`
- transmit capability: `false`
- conservative import/export file size limits

No import, export, watcher, or transmit behavior runs unless the operator explicitly enables it.

## Folder Layout

The supported layout is:

```text
file-hooks/
  incoming/
    stations/
    weather/
    objects/
    gps/
    raw-packets/
    transmit-requests/
  processed/
  rejected/
  exports/
    stations.json
    weather.json
    objects.geojson
    messages.csv
    alerts.json
    raw-packets.log
    decoded-events.json
    diagnostics.json
```

Phase 14.10 implements manual import/export service methods and folder structure helpers. A future file watcher can call the same service so validation, source tagging, and safety behavior stay consistent.

## Export Envelope

JSON exports use a simple envelope:

```json
{
  "schemaVersion": "1.0",
  "exportedAt": "2026-06-12T10:15:30+00:00",
  "sourceApplicationName": "APRS Command",
  "sourceApplicationVersion": "0.1",
  "itemCount": 1,
  "data": [],
  "validationWarnings": [],
  "validationErrors": []
}
```

Export payloads use `AprsCommand.Contracts` DTOs where practical.

## Supported Exports

- `stations.json`: `StationUpdateDto` records
- `weather.json`: `WeatherObservationDto` records
- `objects.geojson`: GeoJSON `FeatureCollection` with APRS object properties
- `messages.csv`: message rows for spreadsheet-friendly review
- `alerts.json`: `AlertDto` records
- `raw-packets.log`: timestamp plus raw packet text
- `decoded-events.json`: `DecodedEventDto` records
- `diagnostics.json`: `RfDiagnosticDto` records

## Supported Imports

Imports may provide either one DTO, a JSON array of DTOs, or an export-style object with a `data` array where practical.

Supported import types:

- external station updates
- external weather observations
- external objects
- external GPS positions
- external raw APRS packets

Raw packet imports may also be plain text, one raw packet per line.

All accepted imports are tagged:

```json
{
  "sourceType": "FileImport",
  "origin": "Imported",
  "trustLevel": "External"
}
```

If source metadata is missing, APRS Command supplies safe default file-import metadata.

## Validation Behavior

Imports validate:

- `schemaVersion`
- required fields for the import type
- source metadata where present
- DTO validation errors
- raw packet line safety
- maximum import size

Malformed records are rejected with validation errors. When folder scanning is used, accepted files may be moved to `processed/` and rejected files to `rejected/`.

## Transmit Request Blocking

`incoming/transmit-requests/` is a blocked placeholder in Phase 14.10.

Imported transmit requests are rejected by default and must not bypass:

- APRS-IS transmit safety
- RF transmit safety
- iGate safety
- digipeater safety
- beacon safety
- weather beacon safety
- object transmit safety
- message transmit safety

This phase does not transmit any imported packet.

## Example Station Import

```json
{
  "schemaVersion": "1.0",
  "callsign": "N0CALL",
  "displayName": "Net Control",
  "latitude": 39.058333,
  "longitude": -84.508333,
  "sourceMetadata": {
    "sourceName": "External Station Tool",
    "sourceType": "FileImport",
    "origin": "Imported",
    "trustLevel": "External"
  }
}
```

## Example Weather Import

```json
{
  "schemaVersion": "1.0",
  "stationId": "WX9XYZ",
  "temperature": 72,
  "humidity": 50,
  "windDirection": 180,
  "windSpeed": 5,
  "sourceMetadata": {
    "sourceName": "Weather File Export",
    "sourceType": "FileImport",
    "origin": "Imported"
  }
}
```

## Example Object GeoJSON

```json
{
  "type": "FeatureCollection",
  "schemaVersion": "1.0",
  "features": [
    {
      "type": "Feature",
      "geometry": {
        "type": "Point",
        "coordinates": [-84.508333, 39.058333]
      },
      "properties": {
        "objectName": "CHECKPNT1",
        "objectType": "object",
        "symbolTable": "/",
        "symbolCode": "-",
        "comment": "Checkpoint 1"
      }
    }
  ]
}
```

## Example Raw Packet Import

```text
N0CALL>APRS:>Imported status
WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000
```

## Example Stations Export

```json
{
  "schemaVersion": "1.0",
  "itemCount": 1,
  "data": [
    {
      "schemaVersion": "1.0",
      "callsign": "N0CALL",
      "displayName": "Net Control",
      "sourceMetadata": {
        "sourceType": "Simulation",
        "origin": "Simulated"
      }
    }
  ],
  "validationWarnings": [],
  "validationErrors": []
}
```

## Example Weather Export

```json
{
  "schemaVersion": "1.0",
  "itemCount": 1,
  "data": [
    {
      "schemaVersion": "1.0",
      "stationId": "TESTWX",
      "temperature": 72,
      "sourceMetadata": {
        "sourceType": "WeatherDriver",
        "origin": "Imported"
      }
    }
  ],
  "validationWarnings": [],
  "validationErrors": []
}
```

## Example Alerts Export

```json
{
  "schemaVersion": "1.0",
  "itemCount": 1,
  "data": [
    {
      "schemaVersion": "1.0",
      "alertId": "alert-sim-001",
      "severity": "Warning",
      "summary": "SIM001 entered training area"
    }
  ],
  "validationWarnings": [],
  "validationErrors": []
}
```

## Example Diagnostics Export

```json
{
  "schemaVersion": "1.0",
  "itemCount": 1,
  "data": [
    {
      "schemaVersion": "1.0",
      "packetId": "pkt-sim-001",
      "callsign": "SIM001",
      "seenOnRf": true,
      "seenOnAprsIs": false
    }
  ],
  "validationWarnings": [],
  "validationErrors": []
}
```

## Processed and Rejected Files

Manual folder scanning can move accepted imports to `processed/` and rejected imports to `rejected/`. Rejected files should be reviewed by the operator or external tool author; APRS Command should not silently repair unsafe imports.

## External Tool Guidance

External tools should:

- write complete files atomically where possible
- include `schemaVersion`
- include source metadata when known
- keep imported files small
- never place secrets in import/export payloads
- treat rejected imports as operator-reviewable diagnostics
- never assume file import grants transmit authority

Future watcher, plugin, and developer SDK phases should reuse this validation and source-tagging behavior rather than exposing mutable internal models directly.
