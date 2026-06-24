# APRS Command Objects Guide

Objects and items represent points of interest such as checkpoints, hazards, shelters, repeaters, or event locations.

## How to Open

Open the Objects tab in the lower-right feature area.

## What It Does

APRS Command can parse objects and items, track object owners, mark killed objects inactive, edit local object drafts, preview object packets, and prepare map placement.

## Step-by-Step Use

1. Open Objects.
2. Review received objects and items.
3. Select an object to view details.
4. Create a local draft if needed.
5. Enter object name, latitude, longitude, symbol, and comment.
6. Save locally.
7. Do not transmit object packets unless a later explicit transmit setup is complete.

## Safe Defaults

Object transmit is disabled by default. Remote-owned objects should not be moved or adopted silently.

## Common Problems

- Object does not appear: check whether it is killed, expired, or filtered.
- Object cannot be moved: it may be remote-owned and not adopted.
- Packet preview invalid: check object name, coordinates, symbol, and comment.

## Troubleshooting

Check the raw packet monitor for object packets beginning with `;` and item packets beginning with `)`.
