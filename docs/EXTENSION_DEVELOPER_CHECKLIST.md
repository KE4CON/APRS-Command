# APRS Command Extension Developer Checklist

Use this checklist before building or sharing an APRS Command integration.

- Choose the integration type: REST, WebSocket, file hooks, plugin, driver, replay, or simulation.
- Use public DTOs from `AprsCommand.Contracts`.
- Include `schemaVersion`.
- Include `sourceMetadata`.
- Tag external data with the correct source type.
- Validate required fields before sending data.
- Preserve validation warnings and errors.
- Request the minimum permissions needed.
- Treat read-only as the default.
- Do not hardcode secrets.
- Do not log tokens, passcodes, or credentials.
- Do not assume transmit is available.
- Do not bypass transmit safety checks.
- Do not create hidden background transmit behavior.
- Handle rejected requests and validation errors gracefully.
- Test offline with fake data.
- Use fake callsigns in examples.
- Document plugin capabilities.
- Document expected source metadata.
- Document any network, serial, file, or hardware requirements.
