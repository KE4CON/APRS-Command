# APRS Command Internal Event Bus

Phase 14.7 adds a lightweight internal event bus for APRS Command.

The event bus lets services publish important application events and lets other internal components subscribe without tight coupling. It prepares the application for later REST API hooks, WebSocket streams, plugin callbacks, file exports, dashboards, diagnostics, logs, and developer tooling.

## Purpose

The event bus is for notifications only.

It is not:

- a transmit path
- a command bus
- a plugin loader
- a WebSocket server
- a REST API
- a file watcher
- an enterprise message broker

Publishers describe that something happened. Subscribers may observe, log, display, or export the event. Subscribers must not use the bus to bypass service-layer validation or transmit safety.

## Main Types

- `IAprsEventBus`
- `IAprsEvent`
- `AprsEventBase`
- `AprsEventEnvelope<TPayload>`
- `AprsEventMetadata`
- `AprsEventSubscription`
- `AprsEventHandlerResult`
- `AprsEventPublishResult`

The bus supports:

- publishing events
- async publishing
- subscribing by event type
- subscribing to all events
- unsubscribe through `IDisposable`
- safe publish when no subscribers exist
- subscriber exception isolation
- recent event history

## Event Metadata

Each event carries `AprsEventMetadata`:

- event ID
- event type
- event category
- timestamp
- source metadata
- severity
- related callsign
- related object name
- related message ID
- related packet ID
- summary
- notes

Source metadata uses the Phase 14.6 public contract source model where practical.

## Categories

Supported categories:

- Packet
- Station
- Object
- Weather
- Message
- GPS
- Port
- APRS-IS
- RF
- Beacon
- iGate
- Digipeater
- Alert
- Diagnostics
- Replay
- Simulation
- Training
- Extension
- System

## Event Types

Supported event types include:

- `RawPacketReceived`
- `RawPacketTransmitted`
- `AprsPacketParsed`
- `AprsPacketParseFailed`
- `StationCreated`
- `StationUpdated`
- `StationExpired`
- `ObjectCreated`
- `ObjectUpdated`
- `ObjectKilled`
- `WeatherUpdated`
- `MessageReceived`
- `MessageSent`
- `MessageAcknowledged`
- `MessageRejected`
- `BulletinReceived`
- `GpsUpdated`
- `PortConnected`
- `PortDisconnected`
- `PortError`
- `AprsIsConnected`
- `AprsIsDisconnected`
- `BeaconGenerated`
- `BeaconTransmitted`
- `WeatherBeaconGenerated`
- `WeatherBeaconTransmitted`
- `PacketTransmitRequested`
- `PacketTransmitted`
- `PacketTransmitBlocked`
- `IGateCandidateDetected`
- `IGatePacketGated`
- `IGatePacketBlocked`
- `DigipeaterPacketRepeated`
- `DigipeaterPacketBlocked`
- `AlertTriggered`
- `AlertAcknowledged`
- `RfDiagnosticUpdated`
- `ReplayStateChanged`
- `ReplayPacketEmitted`
- `SimulationStateChanged`
- `SimulationPacketGenerated`
- `TrainingStateChanged`
- `TrainingScenarioStarted`
- `TrainingScenarioCompleted`
- `ExtensionEvent`

## Publishing Guidance

Services should publish events after state changes are accepted and validated. Event publishing should not replace existing service results, validation results, logs, or safety checks.

Recommended publisher pattern:

1. Validate the input.
2. Apply the service-layer state change.
3. Log or store normal service records as needed.
4. Publish an event with source metadata and a short summary.

Phase 14.7 wires decoded event logging into the bus as the first low-risk publisher. Further services can be connected incrementally.

## Subscriber Exception Handling

Subscriber exceptions are caught and returned as failed `AprsEventHandlerResult` entries. A failing subscriber should not prevent the event bus from returning a publish result or keep other subscribers from receiving the event.

Subscribers should still avoid throwing for expected validation cases.

## Recent Event History

`AprsEventBus` keeps a bounded in-memory recent event list. This exists for diagnostics and the event monitor view. It is not long-term persistence.

## Event Monitor UI

`EventMonitorView` and `EventMonitorViewModel` provide a simple UI foundation for recent events.

Displayed fields:

- timestamp
- category
- event type
- severity
- source
- summary

Basic filters:

- search text
- category
- severity
- event type

## Future Hook Usage

Later phases can subscribe to the event bus to feed:

- local REST API diagnostics
- WebSocket event streams
- file exports
- plugin callbacks
- external dashboards
- developer tools
- logs

Those integrations must use public DTO contracts at external boundaries.

Phase 14.9 adds `WebSocketEventStreamService` as a read-only subscriber that converts internal events into public WebSocket envelopes. It observes the bus only; it does not transmit, execute commands, or mutate configuration.

Phase 14.10 adds `FileHookService` for safe manual file import/export. It publishes import/export lifecycle events and imported data notifications where practical, but imported transmit requests remain blocked by policy.

## Transmit Safety

The event bus must never transmit by itself.

Publishing an event must not bypass:

- APRS-IS transmit safety
- RF transmit safety
- iGate safety
- digipeater safety
- beacon safety
- weather beacon safety
- object transmit safety
- message transmit safety

Transmit-capable services must continue to use central safety checks. Events may describe transmit requests, allowed transmits, or blocked transmits, but they do not authorize or perform transmit.
