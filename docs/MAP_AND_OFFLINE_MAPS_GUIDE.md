# APRS Command Map and Offline Maps Guide

The map is the main APRS Command workspace. It shows stations, selected station details, object markers, and weather marker preparation.

## Map View Overview

The map sits in the center of the main screen. The station list is on the right and the packet monitor is below the map.

## Station Symbols

APRS symbols describe stations such as home stations, mobiles, digipeaters, repeaters, weather stations, objects, and unknown/default stations. If a symbol is not recognized, APRS Command uses a safe fallback marker.

## Station Details

Select a station marker or station list row to see details:

- callsign and display name
- symbol description
- latitude and longitude
- Maidenhead grid
- last heard
- age state
- speed and course
- altitude
- packet source
- last path
- comment/status
- last raw packet
- packet count

## Trails

Station trails show recent position history. Trails are built from position packets only. Status and message packets do not add trail points.

## Tactical Labels

Tactical labels let you display a friendly name while preserving the real callsign. For example, `KD8ABC-7` can display as a field team label while the callsign remains available.

## Offline Map Cache

The map cache stores tile files under the configured `map-cache/` folder. Cached tiles can be reused later.

## Map Download Manager

The offline map download foundation lets you define an area, choose zoom levels, estimate tiles, and prepare downloads. Respect provider rules and attribution.

## Storage Requirements

Offline maps can become large quickly. Use a folder with enough free space, especially on Raspberry Pi systems.

## USGS, Image, and Hybrid Map Notes

Provider-specific map types may be added later. Use only providers that permit the intended access, caching, and attribution.

## Troubleshooting Blank Maps

1. Check internet access for online tiles.
2. Check whether the provider allows download.
3. Check the map cache folder permissions.
4. Check zoom level and area.
5. Confirm the app can write to `map-cache/`.
