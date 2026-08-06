# APRS Command Logs, Events, and Exports Guide

Logs, events, and exports help you troubleshoot and move data out of APRS Command.

## How to Open

Use Packet Monitor, Events, Event Bus, Files, and Settings in the lower-right feature area.

## What It Does

APRS Command can maintain:

- raw packet logs
- decoded event logs
- internal event bus views
- exports
- file hook input/output folders
- replay and training files

## Step-by-Step Use

1. Open Packet Monitor to see raw packet logs.
2. Open Events or Event Bus to see decoded/internal events.
3. Open Files to review file hook placeholders.
4. Open Settings to review folder paths.
5. Export only data you intend to share.

## Safe Defaults

File hooks, REST API, WebSocket streams, and plugin loading are disabled by default. They cannot bypass transmit safety.

## Common Problems

- Log is empty: packet logging may be disabled or no packets arrived.
- Export missing: check the configured `exports/` folder.
- File hook not importing: file hooks may be disabled or the file may be invalid.

## Troubleshooting

Check:

- `logs/`
- `packet-logs/`
- `event-logs/`
- `exports/`
- `file-hooks/incoming/`
- `file-hooks/rejected/`

Do not include private credentials in exported logs.
