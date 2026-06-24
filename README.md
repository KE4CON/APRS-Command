# APRS Command

A cross-platform desktop client for **APRS** (Automatic Packet Reporting System), built with C# / .NET 10 and the Avalonia UI framework. APRS Command aims to reproduce the full feature set of the classic **UI-View32** client and modernize it for Windows, macOS, and Linux — including Raspberry Pi — without using any original UI-View code, UI assets, copyrighted text, or proprietary map data.

Maintainer: **KE4CON**

## Status

- The engine layer (parsers, transports, station database, and services) builds cleanly under .NET 10 with zero NuGet dependencies.
- The desktop UI is wired to the live engine through the composition root (`DesktopRuntime`). The previous design-time-only bootstrap that caused the app to run entirely on sample data has been replaced, so the real engine now drives the UI.
- A first-run setup flow collects the operator's callsign and QTH, so the app works for any station rather than a preset one.
- Real map tile rendering, SQLite persistence, and KISS-TCP / AGWPE connectivity are on the near-term roadmap.

## Building and running

Requires the .NET 10 SDK (the exact pinned version is in `global.json`).

    dotnet restore CrossPlatformAprs.sln
    dotnet build CrossPlatformAprs.sln -c Release
    dotnet run --project src/Aprs.Desktop

See the Installation Guide and Developer Guide below for platform-specific details.

## Documentation

**Getting started**

- [Quick Start](docs/QUICK_START.md)
- [Installation Guide](docs/INSTALLATION_GUIDE.md)
- [First-Run Setup](docs/FIRST_RUN_SETUP.md)
- [User Manual](docs/USER_MANUAL.md)

**Operating**

- [Safety and Transmit Guide](docs/SAFETY_AND_TRANSMIT_GUIDE.md)
- [APRS-IS Setup Guide](docs/APRS_IS_SETUP_GUIDE.md)
- [RF/TNC Setup Guide](docs/RF_TNC_SETUP_GUIDE.md)
- [Map and Offline Maps Guide](docs/MAP_AND_OFFLINE_MAPS_GUIDE.md)
- [Messages Guide](docs/MESSAGES_GUIDE.md)
- [Objects Guide](docs/OBJECTS_GUIDE.md)
- [Weather Guide](docs/WEATHER_GUIDE.md)
- [Alerts and Geofences Guide](docs/ALERTS_AND_GEOFENCES_GUIDE.md)

**Reference and development**

- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Glossary](docs/GLOSSARY.md)
- [Developer Guide](docs/DEVELOPER_GUIDE.md)
- [Build and Publish](docs/BUILD_AND_PUBLISH.md)
- [Installer and Package Plan](docs/INSTALLER_AND_PACKAGE_PLAN.md)
- [Final Release Validation Checklist](docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md)

Design documents for the modernization effort live under [docs/design](docs/design).

## License

To be selected before the repository is made public.
