# APRS Command WebSocket Event Streams

Phase 14.9 adds the optional WebSocket event stream foundation for APRS Command.

The WebSocket stream is intended for approved local tools, dashboards, wall displays, and future plugins that need live notification events from APRS Command.

It is not a command channel and it is not a transmit path.

## Safe Defaults

WebSocket event streams default to conservative local-only settings:

- WebSocket enabled: `false`
- bind address: `127.0.0.1`
- localhost-only: `true`
- token/API key required: `true`
- read-only streaming only: `true`
- transmit capability: `false`
- maximum connected clients: conservative default
- per-client event rate limit: conservative default placeholder

The stream service will not run unless it is explicitly enabled.

## Authentication

Token/API-key authentication is required by default. The Phase 14.9 service uses a token reference field compatible with the local REST API foundation.

Real secrets must not be committed to source control or exposed in event messages.

## Endpoints

The service defines these planned stream endpoints:

- `/ws/events`
- `/ws/stations`
- `/ws/weather`
- `/ws/objects`
- `/ws/messages`
- `/ws/alerts`
- `/ws/raw-packets`
- `/ws/diagnostics`
- `/ws/system`

Phase 14.9 implements the shared service, endpoint metadata, client authorization checks, filtering, and event envelope conversion. A future runtime host can route actual socket connections to this service.

Example connection URL:

```text
ws://127.0.0.1:8766/ws/events?token=YOUR_LOCAL_TOKEN
```

Simple client pseudocode:

```text
connect ws://127.0.0.1:8766/ws/events?token=LOCAL_TOKEN
send {"command":"subscribe","filter":{"eventCategories":["Station","Weather"],"minimumSeverity":"Info"}}
for each message:
  parse envelope
  inspect eventType and payloadType
  update dashboard
on disconnect:
  wait, then reconnect if the operator still has streams enabled
```

## Message Envelope

Every outbound message uses a stable envelope:

```json
{
  "schemaVersion": "1.0",
  "messageId": "8b4f6e6a01d4493f9e4dbad5e96fb4f2",
  "timestamp": "2026-06-12T10:15:30+00:00",
  "streamName": "stations",
  "eventType": "StationUpdated",
  "eventCategory": "Station",
  "sourceMetadata": {
    "sourceName": "Simulation",
    "sourceType": "Simulation",
    "sourceId": "sim",
    "timestamp": "2026-06-12T10:15:30+00:00",
    "origin": "Simulated",
    "trustLevel": "Internal"
  },
  "payloadType": "StationUpdateDto",
  "payload": {
    "schemaVersion": "1.0",
    "callsign": "N0CALL",
    "displayName": "N0CALL",
    "statusText": "StationUpdated summary"
  },
  "warnings": [],
  "errors": []
}
```

Payloads use `AprsCommand.Contracts` DTOs where practical, including:

- `StationUpdateDto`
- `WeatherObservationDto`
- `AprsObjectDto`
- `MessageDto`
- `AlertDto`
- `RawPacketDto`
- `DecodedEventDto`
- `RfDiagnosticDto`
- `SimulationStatusDto`
- `ReplayStatusDto`
- `TrainingScenarioDto`

## Example Alert Message

```json
{
  "schemaVersion": "1.0",
  "streamName": "alerts",
  "eventType": "AlertTriggered",
  "eventCategory": "Alert",
  "payloadType": "AlertDto",
  "payload": {
    "schemaVersion": "1.0",
    "severity": "Warning",
    "summary": "Station entered monitored area",
    "acknowledged": false
  },
  "warnings": [],
  "errors": []
}
```

## Example Weather Message

```json
{
  "schemaVersion": "1.0",
  "streamName": "weather",
  "eventType": "WeatherUpdated",
  "eventCategory": "Weather",
  "payloadType": "WeatherObservationDto",
  "payload": {
    "schemaVersion": "1.0",
    "stationId": "TESTWX",
    "temperature": 72,
    "humidity": 50,
    "sourceMetadata": {
      "sourceType": "WeatherDriver",
      "origin": "Imported"
    }
  },
  "warnings": [],
  "errors": []
}
```

## Example Raw Packet Message

```json
{
  "schemaVersion": "1.0",
  "streamName": "packet",
  "eventType": "RawPacketReceived",
  "eventCategory": "Packet",
  "payloadType": "RawPacketDto",
  "payload": {
    "schemaVersion": "1.0",
    "rawPacket": "N0CALL>APRS:>Test",
    "sourceCallsign": "N0CALL",
    "direction": "Received"
  },
  "warnings": [],
  "errors": []
}
```

## Supported Event Types

The stream service subscribes to the internal event bus and can broadcast representative events such as:

- `RawPacketReceived`
- `AprsPacketParsed`
- `StationCreated`
- `StationUpdated`
- `ObjectCreated`
- `ObjectUpdated`
- `WeatherUpdated`
- `MessageReceived`
- `BulletinReceived`
- `GpsUpdated`
- `PortConnected`
- `PortDisconnected`
- `AlertTriggered`
- `RfDiagnosticUpdated`
- `ReplayStateChanged`
- `ReplayPacketEmitted`
- `SimulationStateChanged`
- `SimulationPacketGenerated`
- `TrainingStateChanged`
- `TrainingScenarioStarted`
- `TrainingScenarioCompleted`

Events without a richer DTO mapping fall back to `DecodedEventDto`.

## Client Filtering

The service supports basic server-side and client-side filtering:

- event category
- event type
- callsign/source name/source ID
- include/exclude raw packet events
- minimum severity

Dedicated endpoints apply a default category filter. For example, `/ws/stations` defaults to station events and `/ws/weather` defaults to weather events.

If a client disconnects unexpectedly, the service drops the client safely. External dashboards should reconnect with backoff and should not assume that missed events are replayed.

Inbound client messages are limited to safe stream controls:

- `ping`
- `subscribe`
- `filter`
- `unsubscribe`
- `close`

Unknown commands are rejected.

## Safety Restrictions

WebSocket streams must not allow:

- APRS-IS transmit
- RF transmit
- iGate transmit
- digipeater transmit
- beacon transmit
- weather beacon transmit
- object transmit
- message transmit
- configuration changes
- command execution

Transmit safety remains centralized in the existing service layer. A WebSocket event may describe a transmit attempt or blocked transmit, but the WebSocket stream service cannot authorize or perform transmit.

## Future Use

Future phases can use this foundation for:

- live dashboard views
- wall displays
- plugin callbacks
- developer tools
- file export monitors
- local automation that only observes APRS Command state

The real runtime WebSocket host should route socket connections into the Phase 14.9 service so authorization, filtering, DTO envelopes, and safety behavior remain consistent.
