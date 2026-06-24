# APRS Command Weather Guide

The Weather area displays APRS weather stations and future local weather station driver observations.

## How to Open

Open the Weather tab in the lower-right feature area.

## What It Does

Weather display can show:

- wind direction, speed, and gust
- temperature
- rain values
- humidity
- barometric pressure
- luminosity or solar data
- UV
- snow
- lightning/event information
- source type and raw payload

## Step-by-Step Use

1. Open Weather.
2. Review station/source names.
3. Check last update time and stale state.
4. Select a weather station to view details.
5. Use raw payload preview for troubleshooting.

## Safe Defaults

Weather beaconing is disabled by default. Weather drivers receive and normalize data; they do not transmit APRS weather packets by themselves.

## Common Problems

- Weather station stale: no recent update arrived.
- Values missing: the source may not provide that field.
- Tempest, Peet Bros, Davis, Ambient, Ecowitt, or weather software driver not updating: check driver status, permissions, network, serial port, or input file.

## Troubleshooting

Use Weather, packet monitor, logs, and driver status together. Verify source tags show whether data came from APRS-IS, RF, local driver, replay, simulation, or unknown.
