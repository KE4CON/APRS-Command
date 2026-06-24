# APRS Command Feature Scope — Ham Radio APRS Only

This project is limited to amateur-radio APRS features.

Out of scope for now:
- ICS forms
- Public-service resource map
- T-card resource tracking
- Non-amateur-radio fleet tracking

In scope:
- APRS station tracking
- APRS-IS
- RF/TNC support
- Messaging
- Objects/items
- Weather packets
- Local/weather station input from common personal weather station systems and weather station software
- Common weather observation normalization for APRS weather display and optional safe weather beaconing
- GPS/mobile operation
- Digipeater/iGate support
- Offline maps
- Logging/replay
- Diagnostics
- Training/simulation
- Extension hooks, local API, WebSocket event streaming, file import/export, and plugin/driver support

Weather scope notes:
- Phase 10 is not only APRS weather display. It also includes local weather station input, common weather data normalization, APRS weather packet formatting, and safe optional weather beacon transmit.
- Phase 10 weather support includes APRS weather display, a common weather model, APRS weather formatting, modular weather driver framework, Tempest UDP, Tempest Cloud, Peet Bros / ULTIMETER, Davis WeatherLink / Davis stations, Ambient Weather, Ecowitt / Fine Offset / GW1000, Cumulus MX / WeeWX / Weather Display / generic weather software imports, safe weather beacon scheduling, Weather Station Setup UI, and offline test/sample data coverage.
- Local/offline-capable weather sources should be preferred when possible.
- Internet/cloud weather APIs are optional and must use user-provided credentials where required.
- Weather credentials/tokens must not be stored in plain text if avoidable.
- Weather transmit features must be disabled by default, keep APRS-IS weather transmit separate from RF weather transmit, and must never transmit stale data.

Architecture scope notes:
- All data models that represent received, generated, imported, replayed, simulated, or transmitted data should preserve source tags.
- Internal app models should remain separate from future public DTOs/contracts.
- Transmit safety should be centralized and shared across APRS-IS, RF/TNC, beaconing, messaging, objects, weather, iGate, and digipeater features.
- Weather, GPS, APRS, replay, simulation, and future inputs should remain modular and driver/service based.
- UI should consume view models/application services rather than hardwiring directly to concrete inputs or transports.
- The desktop shell uses the original map-first layout: map in the main workspace, station list on the right, raw packet monitor below the map, and secondary feature panels in the lower-right tab area.
- Current implementation phases should keep data models modular, tag all incoming packet/weather/GPS/import data with source information, keep transmit safety centralized, and avoid tightly coupling UI directly to packet/weather/GPS input sources.
- Future extension surfaces should expose stable contracts through local APIs, WebSocket events, import/export formats, and plugin/driver abstractions rather than leaking mutable internal models.
- Phase 14.5 through Phase 14.12 complete the extension hook, local API, WebSocket, file import/export, plugin/driver, and developer documentation sequence before packaging. Phase 14.13 upgrades the solution baseline to .NET 10 LTS before Phase 15 packaging begins.
