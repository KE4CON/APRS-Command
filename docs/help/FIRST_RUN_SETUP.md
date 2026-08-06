# APRS Command First-Run Setup

First-run setup prepares APRS Command for safe receive-first use. It does not enable transmit, start network listeners, load plugins, or create final installers.

## First-Run State

The first-run setup model tracks:

- whether first-run setup is complete
- application data, logs, packet logs, exports, file hooks, plugin, and map cache folders
- station profile, safety, APRS-IS, RF/TNC, and map review flags
- created and updated timestamps

All transmit and extension entry points remain disabled by default:

- APRS-IS transmit disabled
- RF transmit disabled
- iGate disabled
- digipeater disabled
- beaconing disabled
- weather beaconing disabled
- REST API disabled
- WebSocket streams disabled
- file hooks disabled
- plugin loading disabled

## Default Folders

APRS Command prepares a platform-appropriate application data root named `APRS Command`. Operators may choose a different root later.

Default layout:

```text
APRS Command/
  config/
  logs/
  packet-logs/
  event-logs/
  maps/
  map-cache/
  exports/
  file-hooks/
    incoming/
    processed/
    rejected/
    exports/
  plugins/
  backups/
  training/
  replay/
```

The folder service uses .NET application data paths and `Path.Combine`; it does not hardcode Windows-only paths.

## Placeholder Configuration Files

The setup service prepares safe placeholder JSON files under `config/`:

- `appsettings.safe-defaults.json`
- `station-profile.placeholder.json`
- `aprs-is.placeholder.json`
- `rf-tnc.placeholder.json`
- `map-cache.placeholder.json`
- `safety.safe-defaults.json`
- `extensions.safe-defaults.json`

These files must not contain real callsigns, APRS-IS passcodes, API tokens, weather credentials, or plugin credentials.

## Desktop UI

The Settings area includes a First Run tab that shows:

- welcome text
- application data folder
- log folder
- map cache folder
- export folder
- safe transmit and extension defaults
- a placeholder command to mark setup complete

This is intentionally lightweight. Future releases can replace it with a fuller wizard after the folder and safety model stabilizes.

## Step-by-Step First-Run Setup

Use this checklist the first time you open APRS Command.

1. Start APRS Command.
2. Open the Settings area in the lower-right tab group.
3. Select the First Run tab.
4. Review the application data folder. Keep the default unless you need logs, maps, exports, replay files, and packet logs on a different drive.
5. Review the safety defaults. Confirm transmit, APRS-IS transmit, RF transmit, iGate, digipeater, beaconing, weather beaconing, REST API, WebSocket, file hooks, and plugin loading are disabled.
6. Review the station profile placeholder. Use a fake or placeholder callsign such as `N0CALL` while learning the program. Do not configure live transmit until you understand your local regulations and station settings.
7. Review the APRS-IS receive placeholder. Receive-only operation is the recommended first connection mode.
8. Review the RF/TNC placeholder. Leave RF transmit disabled while testing Direwolf, Serial KISS, or AGWPE receive.
9. Review the map cache folder. Make sure the selected location has enough free space for offline map tiles.
10. Finish first-run setup only after the safety defaults are reviewed.

## Choosing Data Folders

APRS Command stores user data outside the application folder. This keeps logs and settings writable on Windows, macOS, Linux, and Raspberry Pi.

Choose a folder that:

- is owned by your user account
- has enough free space for maps and logs
- is backed up if you care about station history
- is not a synchronized folder that may rewrite files while the app is running

Portable drives can work for map cache storage, but they should be connected before APRS Command starts.

## Safety Defaults Review

During first-run setup, treat every transmit-related setting as off until proven otherwise:

- station transmit disabled
- APRS-IS transmit disabled
- RF transmit disabled
- beacon scheduler disabled
- weather beacon scheduler disabled
- object transmit disabled
- message transmit disabled
- iGate and digipeater modes disabled

First-run setup is not a transmit authorization step.

## Station Profile Placeholder

The station profile stores the local callsign, SSID, position, symbol, comment, and beacon path used by later transmit features.

For receive-only testing:

1. Leave the local station profile incomplete, or use `N0CALL` as a placeholder.
2. Leave APRS-IS transmit disabled.
3. Leave RF transmit disabled.
4. Leave beaconing disabled.

Before any real transmit use, replace placeholders with your licensed callsign and review the safety guide.

## APRS-IS Receive Placeholder

APRS-IS receive setup can be tested without enabling transmit:

1. Choose an APRS-IS server such as `rotate.aprs2.net`.
2. Use port `14580`.
3. Keep receive-only mode enabled.
4. Enter a filter only if you understand the APRS-IS filter syntax.
5. Do not store or publish passcodes in screenshots, documentation, issue reports, or commits.

## RF/TNC Placeholder

RF receive can be prepared through Direwolf TCP KISS, Serial KISS, or AGWPE. For first-run setup:

1. Leave RF transmit disabled.
2. Configure only one receive path at a time.
3. Verify the packet monitor shows received packets.
4. Do not connect beaconing, iGate, digipeater, or message transmit while testing receive.

## Map Cache Setup

The map cache stores offline map tiles and downloaded map data. Keep it on storage with enough free space. Raspberry Pi systems should avoid filling the boot SD card with map data.

If the map is blank, verify:

- map cache folder is writable
- selected provider allows the requested tile use
- offline downloads have completed
- internet access is available when using online tiles

## Finishing Setup

When setup is complete, APRS Command should open to the map-first layout:

- map in the main workspace
- station list on the right
- packet monitor below the map
- feature buttons such as Messages, Objects, Weather, Events, Event Bus, Replay, RF Diagnostics, and Alerts in the lower-right area

Use the packet monitor and station list to verify receive behavior before changing any transmit settings.

## Platform Notes

Windows:
- Use the user application data location by default.
- Final installers should create shortcuts and uninstall metadata without changing safe defaults.

macOS:
- Use the user application support location by default.
- Final packages should account for signing and notarization.

Linux:
- Use the user application data location or an equivalent user-writable location.
- Desktop files and package metadata should display `APRS Command`.

Raspberry Pi / Linux ARM64:
- Use the same Linux folder behavior.
- Keep map cache and logs on user-writable storage and document SD-card space considerations.

## Safety

First-run setup is not permission to transmit. Operators must explicitly configure and review station identity, APRS-IS transmit, RF transmit, beaconing, iGate, digipeater, and weather beacon settings before any future transmit path can operate.
