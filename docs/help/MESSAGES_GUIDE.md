# APRS Command Messages Guide

The Messages area organizes APRS private messages, outgoing drafts, ACK/retry status, bulletins, announcements, and queries.

## How to Open

Open the Messages tab in the lower-right feature area.

## What It Does

Messages can be stored, drafted, queued, marked sent, marked failed, acknowledged, rejected, and grouped by remote station. Bulletins, announcements, and queries are shown separately from private messages.

## Step-by-Step Use

1. Open Messages.
2. Review Inbox for incoming private messages.
3. Review Bulletins and Announcements.
4. Select a station conversation.
5. Create a draft only after confirming the recipient callsign.
6. Keep transmit disabled unless a later safe transmit setup is complete.

## Safe Defaults

Message transmit is disabled by default. Drafting or queuing a message does not bypass transmit safety.

## Common Problems

- ACK not received: recipient may be offline or message was not transmitted.
- Wrong addressee: verify the callsign and SSID.
- Message not sending: transmit may be disabled, receive-only may be active, or safety checks may block it.

## Troubleshooting

Use the packet monitor and raw packet logs to compare message packets, ACK packets, and REJ packets.
