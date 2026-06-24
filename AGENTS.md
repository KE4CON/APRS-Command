# AGENTS.md — Codex Project Instructions

You are working on a C# cross-platform APRS desktop client inspired by UI-View32, but do not copy UI-View code, UI assets, copyrighted text, or proprietary map data.

## Project goals

Build a modern amateur-radio APRS application with:
- APRS-IS receive/transmit support
- Serial KISS and TCP KISS support
- Direwolf compatibility
- Optional AGWPE support
- Live APRS map
- Station list
- APRS messaging with ACK/retry
- APRS objects/items
- Beaconing and GPS support
- Weather packet support
- Offline map cache
- Logs, replay, and diagnostics
- Extension hooks, local API, WebSocket events, file import/export, and plugin/driver support
- Cross-platform desktop UI using Avalonia

## Architecture rules

- Keep APRS protocol parsing in `Aprs.Core` only.
- Keep transport-specific code in `Aprs.Transport` only.
- Keep business logic in `Aprs.Services` only.
- Keep map-specific rendering/cache logic in `Aprs.Mapping` only.
- Keep UI logic in `Aprs.Desktop` only.
- Keep unit tests in `tests/Aprs.Tests`.
- Do not put serial-port, TCP, file-system, or UI dependencies in `Aprs.Core`.
- Prefer interfaces and dependency injection for services.
- Add source tagging to all data models that represent received, generated, imported, replayed, simulated, or transmitted data.
- Keep internal domain models separate from future public DTOs/contracts; use explicit mapping/adapters for public/export/API boundaries.
- Keep transmit safety centralized so all APRS-IS, RF/TNC, beacon, object, message, weather, iGate, and digipeater transmit flows share the same safety gates.
- Keep weather, GPS, APRS, replay, simulation, and future input sources modular behind driver/service abstractions.
- Avoid hardwiring transports, parsers, or services directly into UI views; use view models and service abstractions.
- Current feature phases should preserve modular data models, source-tag incoming data, centralize transmit safety, and avoid tight UI coupling to packet/weather/GPS input sources while the Phase 14.5-14.12 extension work formalizes extension boundaries before packaging.
- Write tests for parser, message ACK handling, object handling, station expiration, and beacon scheduling.
- Do not transmit RF by default. Any RF transmit feature must require explicit user configuration.
- Add safety warnings for high beacon rates, bad paths, and RF transmit enablement.

## Coding standards

- Use C# nullable reference types.
- Use async/await for I/O.
- Use cancellation tokens for long-running loops.
- Use structured logging.
- Prefer immutable records for parsed APRS packet models.
- Validate user input before transmit.
- Include XML comments on public interfaces.

## Testing expectations

Every feature task should include tests when practical. Parser behavior must be test-driven with sample packets.

## Build commands

Use these commands when .NET SDK is installed:

```bash
dotnet restore
dotnet build
dotnet test
```

## Safety and compliance

This software is for licensed amateur-radio operation. Do not implement features that encourage unauthorized transmitting. Include clear visual separation between APRS-IS receive-only, internet transmit, and RF transmit modes.
