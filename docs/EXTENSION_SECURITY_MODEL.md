# APRS Command Extension Security Model

Extensions, local APIs, file imports, plugins, and external drivers must be treated as untrusted until explicitly configured by the operator.

## Default Posture

- External permissions default to `ReadOnly`.
- Source metadata defaults to unknown and untrusted.
- Runtime extension systems remain disabled by default and must be explicitly enabled by the operator as the Phase 14.6-14.12 hook sequence is implemented.
- No extension can transmit by default.

## Central Transmit Safety Rule

All transmit-capable actions must pass through centralized transmit safety checks.

This applies to:

- APRS-IS transmit
- RF transmit
- iGate
- digipeater
- local beaconing
- weather beaconing
- object transmit
- message transmit
- future API queued packets
- future plugin requested packets
- future file-imported packets

External hooks must never bypass transmit safety. A valid permission is not enough by itself; the central safety policy must still approve the request using current operator settings, station profile, port state, training/replay lockout state, stale-data checks, and rate/interval limits.

## Permission Expectations

`ReadOnly` allows observing data only.

`SubmitLocalData` may allow future integrations to submit normalized local observations such as weather or GPS data, but submitted data must be source-tagged and validated.

`CreateLocalObjects` may allow future integrations to create local object drafts, but object transmit remains separately gated.

`QueuePackets` may allow future integrations to request packet queueing, but queued packets still require central transmit safety before sending.

`TransmitAprsIs` and `TransmitRf` are high-risk permissions and require explicit operator enablement.

`Admin` is reserved for trusted local administration and must not be granted to ordinary plugins or file imports.

## Source Trust

Trust levels should communicate how much the app should rely on the source:

- `Unknown`
- `Untrusted`
- `External`
- `OperatorConfigured`
- `Local`
- `Internal`

Trust level should not be used as a shortcut around validation or transmit safety.

## UI Coupling Rules

Future extension code should publish events and data through service abstractions. Avalonia views should bind to view models rather than starting transports, file watchers, plugin loaders, or network listeners directly.

## Credentials and Privacy

Extension hooks must not expose:

- APRS-IS passcodes
- API tokens
- weather API credentials
- GitHub tokens
- serial device secrets or other private credentials

Logs may record blocked transmit attempts, but credential values must be redacted or omitted.
