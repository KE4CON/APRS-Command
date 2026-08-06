# APRS Command Extension Safety Rules

Extensions are read-only by default.

APRS Command is amateur-radio software. Any integration that can affect transmit behavior must be treated cautiously and must pass central safety checks.

## Required Defaults

- REST API disabled by default
- WebSocket streams disabled by default
- file hooks disabled by default
- plugin loading disabled by default
- transmit permissions denied by default
- RF transmit disabled by default
- imported transmit requests blocked by default

## Source Tagging

External data must be source-tagged with `ExternalSourceMetadata`.

Source tags should identify:

- where the data came from
- whether it was imported, generated, simulated, replayed, or received
- whether it is trusted, operator configured, or external

## Transmit Requests

External transmit requests require:

- explicit operator enablement
- explicit transmit permission
- valid callsign and station profile
- valid destination/path/packet format
- central transmit safety approval
- clear logging of accepted or blocked attempts

No extension may silently transmit.

## Surface-Specific Rules

- REST API cannot bypass transmit safety.
- WebSocket streams are notification-only.
- File imports cannot bypass transmit safety.
- Plugins cannot bypass transmit safety.
- Drivers provide observations or packet input; they do not grant transmit authority.

## Secrets

Never log:

- APRS-IS passcodes
- API tokens
- weather API credentials
- GitHub tokens
- plugin secrets
- serial device credentials

Examples must not contain live credentials.

## Test Safety

Tests and examples must:

- run offline
- use fake callsigns and fake data
- require no live RF
- require no APRS-IS connection
- require no weather hardware
- require no serial device
- perform no transmit
