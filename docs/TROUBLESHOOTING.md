# APRS Command Troubleshooting

Use this guide when APRS Command does not behave as expected.

## App Will Not Start

1. Run from a terminal.
2. Check for missing .NET 10 SDK/runtime when running from source.
3. Confirm the app folder is readable.
4. Confirm the data folder is writable.
5. Check `logs/`.

## Map Is Blank

1. Check internet access.
2. Check map provider settings.
3. Check map cache folder permissions.
4. Check offline tile download status.
5. Try a different zoom level.

## APRS-IS Will Not Connect

1. Check server hostname.
2. Check port `14580`.
3. Check firewall rules.
4. Try `rotate.aprs2.net`.
5. Remove filters temporarily.
6. Keep receive-only enabled while testing.

## No Stations Appear

1. Check packet monitor.
2. Confirm packets are valid APRS packets.
3. Check station filters.
4. Check age/hidden station settings.
5. Confirm the source is not simulation/replay if expecting live data.

## TNC Not Detected

1. Check USB cable.
2. Check serial port name.
3. Check baud rate.
4. Check permissions.
5. Confirm the TNC is in KISS mode.

## Serial Port Permission Denied

On Linux, add your user to the correct serial group for your distribution, then log out and back in. Do not run APRS Command as root just to bypass permissions.

## Direwolf Not Connecting

1. Start Direwolf first.
2. Confirm TCP KISS is enabled.
3. Confirm host `127.0.0.1`.
4. Confirm port `8001`.
5. Check Direwolf console output.

## AGWPE Not Connecting

1. Start AGWPE-compatible software.
2. Confirm host and port.
3. Confirm selected radio port.
4. Check AGWPE logs.

## Messages Not Sending

Message transmit is disabled by default. Check:

- local callsign
- recipient callsign
- APRS-IS and RF transmit setting state
- receive-only state
- connection state
- safety block reason

## Feature Buttons Do Not Change Views

The lower-right feature area should show one clean set of feature buttons for Messages, Objects, Weather, Events, Event Bus, Replay, RF Diag, and Alerts. Clicking one of these buttons should update the selected feature title, description, and content without hiding the map. If nothing changes:

1. Confirm you are running a build that includes the restored map-first layout.
2. Confirm the lower-right feature panel is visible and not clipped by the window size.
3. Click Messages, Objects, Weather, Events, Event Bus, Replay, RF Diag, and Alerts.
4. Confirm each button changes the selected feature view.
5. Restart APRS Command from a terminal and capture errors with `./Aprs.Desktop 2>&1 | tee launch-error.txt`.

## Help Button Missing Or Does Not Open

The Help button should be visible in the application header. It opens the offline Help viewer with User Manual, Quick Start, Safety and Transmit Guide, Troubleshooting, and Glossary topics. If Help does not open:

1. Confirm the `docs/` folder exists beside the published app or in the development repository.
2. Start APRS Command from a terminal and capture errors.
3. Missing Help topics should show `This help topic is not available in this build` instead of crashing.

## Weather Station Not Updating

1. Check source status.
2. Check last update time.
3. Check stale threshold.
4. Check serial, UDP, HTTP, file, or driver settings.
5. Review raw payload preview.

## Replay Not Loading

1. Check file path.
2. Check file format.
3. Check permissions.
4. Check logs for parse errors.

## Alerts Not Triggering

1. Confirm alert rule is enabled.
2. Confirm geofence is enabled and valid.
3. Confirm station updates are arriving.
4. Check alert severity and filters.

## File Hooks Not Importing

1. Confirm file hooks are enabled only if you intend to use them.
2. Put files in `file-hooks/incoming/`.
3. Check `file-hooks/rejected/`.
4. Check file schema and source tags.

## REST API Not Reachable

REST API is disabled by default. Confirm you intentionally enabled it, then check host, port, firewall, and safety settings.

## WebSocket Not Reachable

WebSocket event streams are disabled by default. Confirm enabled state, endpoint, firewall, and event stream settings.

## Plugin Not Loading

Plugin loading is disabled by default. Check plugin folder, manifest, safety permissions, and logs. Plugins cannot bypass transmit safety.

## Where To Find Logs

Look in the configured application data folder:

- `logs/`
- `packet-logs/`
- `event-logs/`
- `file-hooks/rejected/`
- `backups/`
