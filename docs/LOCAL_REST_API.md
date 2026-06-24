# APRS Command Local REST API

Phase 14.8 adds the local REST API foundation for APRS Command.

The API is intended for local tools, dashboards, scripts, and future integrations that need to read APRS Command data or submit carefully controlled local data.

Phase 14.9 adds a separate optional WebSocket event stream foundation. This REST API phase does not add file watchers, plugin loading, or public transmit execution.

## Safe Defaults

The API defaults are intentionally conservative:

- API enabled: `false`
- bind address: `127.0.0.1`
- localhost-only: `true`
- token/API key required: `true`
- read-only mode: `true`
- external data submit: `false`
- transmit requests: `false`

The API host/service will not run unless explicitly enabled.

## Authentication

Token/API-key authentication is required by default. Real secrets must not be stored in source code or test fixtures.

Future settings should store only a token reference or use the platform credential store where practical.

## Permissions

External callers default to `ReadOnly`.

Permission expectations:

- `ReadOnly`: read endpoints only
- `SubmitLocalData`: submit external station, weather, GPS, or raw packet data
- `CreateLocalObjects`: submit external object data
- `QueuePackets`: request packet queueing, still blocked by transmit safety
- `TransmitAprsIs`: future APRS-IS transmit permission
- `TransmitRf`: future RF transmit permission
- `Admin`: future dangerous configuration actions

Transmit-related permissions are never enough by themselves. Central transmit safety must still approve any future transmit-capable request.

## Read Endpoints

Planned/read foundation endpoints:

- `GET /api/health`
- `GET /api/version`
- `GET /api/stations`
- `GET /api/stations/{callsign}`
- `GET /api/objects`
- `GET /api/weather`
- `GET /api/messages`
- `GET /api/gps`
- `GET /api/ports`
- `GET /api/alerts`
- `GET /api/raw-packets`
- `GET /api/events`
- `GET /api/rf-diagnostics`
- `GET /api/replay/status`
- `GET /api/simulation/status`
- `GET /api/training/status`

Responses should use `AprsCommand.Contracts` DTOs rather than mutable internal app models.

## Controlled Submit Endpoints

Controlled submit endpoints:

- `POST /api/external/station`
- `POST /api/external/weather`
- `POST /api/external/object`
- `POST /api/external/gps`
- `POST /api/external/raw-packet`

Submit endpoints are rejected unless:

- API is enabled
- token is valid when token auth is required
- read-only mode is disabled
- external data submit is enabled
- caller has the required permission
- submitted DTO validates

Submitted data is tagged with `ExternalSourceType.LocalApi` when no more specific allowed source is supplied, and its origin is set to `LocalApi`.

Submitted data must not trigger transmit by default.

## Transmit Queue Placeholder

`POST /api/transmit/queue` exists only as a blocked placeholder in Phase 14.8.

Default behavior:

- disabled
- blocked unless transmit requests are explicitly allowed
- blocked unless caller has `QueuePackets` and a specific transmit permission
- still blocked as not implemented
- never bypasses APRS-IS/RF/iGate/digipeater/beacon safety

## Event Bus Integration

The local API foundation publishes internal events where practical:

- API started
- API stopped
- API request accepted
- API request rejected
- external station submitted
- external weather submitted
- external object submitted
- external GPS submitted
- external raw packet submitted
- transmit request blocked

WebSocket streams, file exports, dashboards, and plugin callbacks can subscribe to these events. See `docs/WEBSOCKET_EVENT_STREAMS.md` for the Phase 14.9 stream foundation.

## Example GET

```text
GET /api/stations
Authorization: Bearer test-token
```

Example response body is a JSON array of `StationUpdateDto`.

Health check:

```bash
curl -H "Authorization: Bearer LOCAL_TOKEN" http://127.0.0.1:8765/api/health
```

Get stations:

```bash
curl -H "Authorization: Bearer LOCAL_TOKEN" http://127.0.0.1:8765/api/stations
```

Get weather:

```bash
curl -H "Authorization: Bearer LOCAL_TOKEN" http://127.0.0.1:8765/api/weather
```

Get objects:

```bash
curl -H "Authorization: Bearer LOCAL_TOKEN" http://127.0.0.1:8765/api/objects
```

Get alerts:

```bash
curl -H "Authorization: Bearer LOCAL_TOKEN" http://127.0.0.1:8765/api/alerts
```

## Example External Weather Submit

```json
{
  "schemaVersion": "1.0",
  "stationId": "WX9XYZ",
  "temperature": 72,
  "humidity": 50,
  "sourceMetadata": {
    "sourceName": "External Weather Tool",
    "sourceType": "LocalApi",
    "origin": "LocalApi",
    "trustLevel": "External"
  }
}
```

## Example External Station Submit

```json
{
  "schemaVersion": "1.0",
  "callsign": "N0CALL",
  "tacticalLabel": "Net Control",
  "latitude": 39.058333,
  "longitude": -84.508333,
  "sourceMetadata": {
    "sourceType": "LocalApi",
    "origin": "LocalApi"
  }
}
```

Example external station submit:

```bash
curl -X POST \
  -H "Authorization: Bearer LOCAL_TOKEN" \
  -H "Content-Type: application/json" \
  --data @examples/rest/submit-station.example.json \
  http://127.0.0.1:8765/api/external/station
```

Example external weather submit:

```bash
curl -X POST \
  -H "Authorization: Bearer LOCAL_TOKEN" \
  -H "Content-Type: application/json" \
  --data @examples/rest/submit-weather.example.json \
  http://127.0.0.1:8765/api/external/weather
```

Example external object submit:

```json
{
  "schemaVersion": "1.0",
  "objectName": "OBJTEST",
  "objectType": "object",
  "latitude": 39.06,
  "longitude": -84.51,
  "sourceMetadata": {
    "sourceType": "LocalApi",
    "origin": "LocalApi"
  }
}
```

## Example Blocked Transmit Request

```text
POST /api/transmit/queue
```

Expected Phase 14.8 behavior is a policy error such as:

```json
{
  "success": false,
  "error": "Transmit queue endpoint is disabled."
}
```

Example blocked transmit request body:

```json
{
  "schemaVersion": "1.0",
  "rawPacket": "N0CALL-SIM>APRS:>Blocked example",
  "requestedTransport": "APRS-IS"
}
```

The expected result is a policy rejection unless future operator settings and central transmit safety explicitly allow the request.

## Runtime Hosting Note

Phase 14.8 adds the API service/configuration/endpoint foundation and tests. The service is structured so a future ASP.NET Core minimal API host can route HTTP requests into the same authorization, DTO, event, and safety logic.
