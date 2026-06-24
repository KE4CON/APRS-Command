# APRS Command Quick Start

This guide gets APRS Command open in receive-only mode. It is written for first-time users.

## 1. Install or Build APRS Command

If you are using a packaged build, download the package for your platform when packages are available.

If you are running from source, install the .NET 10 SDK, then run:

```bash
dotnet restore
dotnet build
dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj
```

## 2. Launch APRS Command

Start the application from your package, shortcut, terminal, or development command. The main screen uses the map-first layout:

- large map in the main workspace
- station list on the right
- packet monitor below the map
- feature buttons in the lower-right area

Click Messages, Objects, Weather, Events, Event Bus, Replay, RF Diag, or Alerts in the lower-right feature button area to show that feature view. Click Help in the application header whenever you need the offline User Manual, Quick Start, Safety and Transmit Guide, Troubleshooting, or Glossary. Use the Help search box to filter topics.

## 3. Complete First-Run Setup

1. Open Settings.
2. Open the First Run tab.
3. Keep the default data folder unless you want maps and logs somewhere else.
4. Review the safety defaults.
5. Confirm transmit is disabled.
6. Confirm APRS-IS transmit is disabled.
7. Confirm RF transmit is disabled.

## 4. Configure a Callsign Placeholder

While learning the program, use a placeholder such as `N0CALL`. Replace it only when you are ready to configure your licensed station.

## 5. Connect to APRS-IS Receive Only

1. Open the APRS-IS settings area when available.
2. Choose a server such as `rotate.aprs2.net`.
3. Use port `14580`.
4. Keep receive-only enabled.
5. Leave APRS-IS transmit disabled.
6. Do not publish or commit APRS-IS passcodes.

## 6. View Stations

After packets arrive:

1. Watch the packet monitor for raw APRS lines.
2. Watch the station list on the right.
3. Select a station to view station details.
4. Check the map for station markers.

## 7. Verify Transmit Is Disabled

Before experimenting:

- APRS-IS transmit should be disabled.
- RF transmit should be disabled.
- beaconing should be disabled.
- iGate and digipeater modes should be disabled.
- message and object transmit should remain unavailable until explicitly configured.

## 8. Exit Safely

1. Stop any receive connections you started.
2. Close APRS Command from the window controls or application menu.
3. Wait for the app to close before unplugging serial devices or removable storage.
