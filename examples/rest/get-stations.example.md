# Get Stations Example

The local REST API is disabled by default, localhost-only by default, token protected by default, and read-only by default.

```bash
curl -H "Authorization: Bearer LOCAL_TOKEN" \
  http://127.0.0.1:8765/api/stations
```

Example response:

```json
[
  {
    "schemaVersion": "1.0",
    "callsign": "SIM001",
    "displayName": "Sim Station 1",
    "latitude": 39.058333,
    "longitude": -84.508333,
    "sourceMetadata": {
      "sourceName": "Simulation",
      "sourceType": "Simulation",
      "sourceId": "sim",
      "origin": "Simulated",
      "trustLevel": "Internal"
    },
    "validationWarnings": [],
    "validationErrors": []
  }
]
```
