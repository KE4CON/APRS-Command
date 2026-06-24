# Weather Test Data

All files in this folder are fake deterministic test fixtures for parser, normalization, diagnostics, stale-data, and safety tests.

No real API keys, tokens, credentials, user serial numbers, private station identifiers, or private station locations are included.

## Folder Map

- `Tempest/` — WeatherFlow Tempest local UDP and Cloud REST sample payloads.
- `PeetBros/` — Peet Bros / ULTIMETER serial/key-value and APRS-style weather text samples.
- `Davis/` — Davis WeatherLink-style JSON samples.
- `Ambient/` — Ambient Weather API-style JSON samples.
- `Ecowitt/` — Ecowitt / Fine Offset / GW1000 JSON and form-upload samples.
- `WeatherSoftware/` — Cumulus MX, WeeWX, Weather Display, realtime.txt, JSON, CSV, key-value, and local HTTP-style samples.
- `AprsWeather/` — APRS weather packet examples used for formatter/parser-oriented tests.
- `Invalid/` — intentionally malformed or incomplete samples used to prove parsers fail safely.

## Units

Samples are intentionally mixed to exercise normalization:

- Tempest local/cloud payloads use metric fields where WeatherFlow commonly does: meters/second, Celsius, millimeters, millibars/hPa.
- Davis, Ambient, Ecowitt, and generic weather software samples mostly use US-style fields such as mph, Fahrenheit, inches, and inHg where those formats commonly provide them.
- Normalized `CommonWeatherObservation` values are expected to use mph, Fahrenheit, inches, millibars, degrees, and UTC timestamps.

## Validity

Files under source-specific folders are valid unless their file name includes `stale` or they are documented by the test as partial.
Files under `Invalid/` are intentionally malformed or incomplete.
