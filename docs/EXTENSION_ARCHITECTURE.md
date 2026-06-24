# APRS Command Extension Architecture

APRS Command keeps internal application models separate from external integration contracts.

The extension architecture is built around stable public DTOs, source tagging, event notifications, and central transmit safety.

## Layers

```text
External tools
  REST clients / WebSocket dashboards / file tools / plugins
        |
        v
Public contracts
  AprsCommand.Contracts DTOs
        |
        v
Application boundary services
  Local REST API / WebSocket streams / file hooks / plugin driver framework
        |
        v
Internal services
  station database / weather display / messaging / objects / alerts / diagnostics
        |
        v
Core and transport
  APRS parser / APRS-IS / KISS / AGWPE / GPS / weather drivers
```

## Internal Services

Internal services own application behavior. They may change as APRS Command grows and should not be exposed directly to external integrations.

Examples:

- station database
- weather display service
- message store
- object manager
- alert rules
- raw packet log
- diagnostics
- training and simulation

## Public Contracts Layer

`AprsCommand.Contracts` is the stable external shape. It contains JSON-friendly DTOs with schema versions and source metadata.

External integrations should map to DTOs rather than reference mutable internal service models.

## Internal Event Bus

The event bus is notification-only.

It feeds:

- local API diagnostics
- WebSocket streams
- file export hooks
- plugin callbacks
- developer tools

It is not a command bus and not a transmit path.

## Data Flow In

```text
External source
  -> public DTO or documented file
  -> validation
  -> source tagging
  -> service-layer accept/reject
  -> internal event bus notification
  -> UI/view models observe application state
```

External data may come from REST, file import, plugin/driver input, replay, simulation, or manual tools.

## Data Flow Out

```text
Internal service state
  -> DTO mapping
  -> REST response / WebSocket envelope / file export / plugin callback
  -> external tool
```

Outgoing integration data should include schema version and source metadata.

## Central Transmit Safety

No extension surface bypasses transmit safety.

REST requests, WebSocket messages, file imports, plugin events, and driver updates cannot transmit directly. Future transmit-capable requests must pass explicit operator settings, permissions, validation, and central transmit safety checks.

## Source Tagging

All received, generated, imported, replayed, simulated, training, plugin, API, and file data should carry source metadata.

This allows APRS Command to distinguish:

- live APRS-IS data
- RF/TNC data
- local API data
- file imports
- plugins
- weather drivers
- GPS sources
- replay or simulation data

## UI Coupling

Avalonia views should bind to view models and application services. Views should not start transports, file watchers, plugin loaders, or transmit paths directly.
