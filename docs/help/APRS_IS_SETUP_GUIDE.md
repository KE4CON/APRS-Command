# APRS Command APRS-IS Setup Guide

APRS-IS connects APRS Command to internet APRS servers. Start with receive-only operation.

## Receive-Only Setup

1. Open Settings.
2. Open the APRS-IS area when available.
3. Choose a server such as `rotate.aprs2.net`.
4. Use port `14580`.
5. Keep receive-only enabled.
6. Keep APRS-IS transmit disabled.
7. Connect.
8. Watch the packet monitor for received lines.

## Server Selection

Common server names include:

- `rotate.aprs2.net`
- `noam.aprs2.net`
- `euro.aprs2.net`
- `soam.aprs2.net`
- `aunz.aprs2.net`
- `asia.aprs2.net`

Choose a regional server when practical.

## Filter Setup

APRS-IS filters reduce traffic. Leave the filter blank or conservative while learning. Incorrect filters can make it look like APRS-IS is not working.

## Connection Status

Use connection status and the packet monitor together:

- connected but no packets may mean the filter is too narrow
- disconnected means server, network, or credential settings need review
- repeated reconnects may indicate network instability

## Packet Monitor

The packet monitor is the first place to verify receive. It should show raw APRS packet lines and server comments. Server comment lines are not APRS packets.

## Safe Transmit Placeholder

APRS-IS transmit must remain disabled until you intentionally configure it. APRS Command should block transmit if receive-only is enabled, passcode is missing, callsign is invalid, the client is disconnected, or safety checks fail.

## Passcode Warning

Do not publish or commit passcodes. Do not place passcodes in documentation, issue reports, screenshots, examples, or public logs.

## Troubleshooting

If APRS-IS will not connect:

1. Check internet access.
2. Check server hostname.
3. Check port `14580`.
4. Try a regional server.
5. Remove the filter temporarily.
6. Confirm firewall rules allow outbound TCP.
7. Confirm receive-only is still enabled while testing.
