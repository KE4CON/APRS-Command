# APRS Command Plugin / Driver Framework

Phase 14.11 establishes the plugin/driver framework foundation for future APRS Command extensions.

This document is developer-facing guidance for the framework. Runtime plugin loading remains disabled by default.

## Safe Defaults

- plugins disabled by default
- plugin loading disabled by default
- unsigned plugins rejected by default
- operator approval required by default
- transmit permissions denied by default
- plugins cannot bypass central transmit safety

## Plugin Manifest Format

Plugins should declare metadata before APRS Command loads or approves them.

Example fields:

- `schemaVersion`
- `pluginId`
- `name`
- `version`
- `publisher`
- `description`
- `capabilities`
- `requestedPermissions`
- `sourceMetadata`
- `signature`
- `minimumAprsCommandVersion`

See `examples/plugins/plugin-manifest.example.json`.

## Plugin Lifecycle

Expected lifecycle:

1. Discover manifest.
2. Validate schema and signature.
3. Show capabilities and requested permissions to the operator.
4. Require operator approval.
5. Start plugin in a restricted context.
6. Monitor status and health.
7. Stop plugin cleanly.
8. Record errors without crashing APRS Command.

## Capabilities

Example capabilities:

- weather input driver
- station data exporter
- training scenario provider
- alert rule provider
- diagnostics consumer
- read-only dashboard connector

Capabilities describe what a plugin can do. They do not grant permission by themselves.

## Requested Permissions

Plugins should request the minimum permissions needed:

- `ReadOnly`
- `SubmitLocalData`
- `CreateLocalObjects`
- `QueuePackets`
- `TransmitAprsIs`
- `TransmitRf`
- `Admin`

Transmit permissions are denied by default and require central safety checks even if approved.

## Source Metadata

Plugin data should use:

```json
{
  "sourceName": "Example Plugin",
  "sourceType": "Plugin",
  "sourceId": "example-plugin",
  "origin": "Plugin",
  "trustLevel": "External"
}
```

Drivers should use the most specific source type available, such as `WeatherDriver` for local weather observations.

## Status and Health

Plugins should report:

- plugin ID
- current status
- enabled state
- last start time
- last stop time
- last observation or event time
- last error
- validation warnings and errors

## Event Publishing

Plugins may publish notification events through approved service abstractions. Events must use source metadata and public payloads where practical.

Plugin events are not commands and do not authorize transmit.

## Example WeatherInputDriver

A weather input driver should:

- declare weather driver capability
- provide observations in the common weather model or public DTOs
- include source metadata
- report stale/faulted state
- avoid direct UI coupling
- avoid transmit

See `examples/plugins/weather-input-driver-stub.md`.

## Example StationDataExporter

A station data exporter should:

- request read-only permission
- read public `StationUpdateDto` data
- write documented files or call a local dashboard
- not mutate station state
- not transmit

See `examples/plugins/station-exporter-stub.md`.

## Example TrainingScenarioProvider

A training scenario provider should:

- provide fake stations, objects, or tasks
- tag data as `Training` or `Simulation`
- never use real incidents or credentials in examples
- avoid transmit

See `examples/plugins/training-scenario-provider-stub.md`.

## Example AlertRuleProvider

An alert rule provider should:

- declare alert-rule capability
- validate rule inputs
- publish alert events through approved services
- avoid direct UI or transport dependencies
- avoid transmit

See `examples/plugins/alert-rule-provider-stub.md`.

## What Plugins Must Not Do

Plugins must not:

- transmit APRS-IS or RF packets directly
- bypass central transmit safety
- start hidden transports
- hardcode secrets
- log credentials
- mutate internal models directly
- depend on Avalonia views
- require live RF/APRS-IS for tests
- use real personal callsigns in examples
