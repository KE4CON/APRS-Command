# APRS Command Release Notes Template

Version: `0.0.0-dev`

Date: `TBD`

Supported platforms:

- Windows x64: `APRS-Command-win-x64.zip`
- macOS Apple Silicon: `APRS-Command-osx-arm64.tar.gz`
- macOS Intel: `APRS-Command-osx-x64.tar.gz`
- Linux x64: `APRS-Command-linux-x64.tar.gz`
- Linux ARM64 / Raspberry Pi 5: `APRS-Command-linux-arm64.tar.gz`

## Package Files

- TBD

## Checksums

SHA256 checksum files are stored under `artifacts/checksums/`.

## New Features

- TBD

## Known Issues

- TBD

## Safe Defaults

APRS Command does not transmit by default. APRS-IS transmit, RF transmit, iGate, digipeater, beaconing, weather beaconing, REST API, WebSocket, file hooks, and plugin loading are disabled by default. Replay, simulation, and training cannot transmit.

## Upgrade Notes

- Back up configuration and logs before replacing a portable folder.
- Keep map cache and logs on user-writable storage.
- Review first-run setup and safety settings after upgrading.

## Troubleshooting Links

- `docs/INSTALLATION_GUIDE.md`
- `docs/TROUBLESHOOTING.md`
- `docs/SAFETY_AND_TRANSMIT_GUIDE.md`
