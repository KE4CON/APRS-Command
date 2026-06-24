# APRS Command Alerts and Geofences Guide

Alerts and geofences help monitor station activity and geographic areas.

## How to Open

Open the Alerts or Geofence tabs in the lower-right feature area.

## What It Does

Alerts can watch for station, packet, weather, object, and system conditions. Geofences define circle or polygon areas for station enter/exit monitoring.

## Step-by-Step Use

1. Open Geofence.
2. Create a circle geofence with a name, center, and radius.
3. Enable alert on enter or alert on exit.
4. Save the geofence.
5. Open Alerts.
6. Review alert rules and recent alert events.

## Safe Defaults

Alerts do not transmit packets by themselves. Disabled geofences should not trigger.

## Common Problems

- Alert not triggering: check rule enabled state, severity, station source, and geofence geometry.
- Geofence invalid: check radius, latitude, longitude, and polygon point count.
- Event missing: confirm station updates are arriving.

## Troubleshooting

Use fake or replay station updates for testing. Do not use live transmit to test alert behavior.
