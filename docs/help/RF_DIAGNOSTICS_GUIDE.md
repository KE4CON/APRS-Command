# APRS Command RF Diagnostics Guide

RF Diagnostics helps troubleshoot RF/TNC receive paths.

## How to Open

Open RF Diagnostics in the lower-right feature tab area.

## What It Does

RF Diagnostics can show port state, packet counters, source tags, errors, and status for TCP KISS, Serial KISS, Direwolf, AGWPE, RF, replay, simulation, and unknown sources.

## Step-by-Step Use

1. Open RF Diagnostics.
2. Select the port or source.
3. Check connection state.
4. Check receive enabled.
5. Confirm transmit disabled while testing.
6. Watch packet counters and last packet time.
7. Review last error.

## Safe Defaults

RF transmit is disabled by default on all RF-related ports.

## Common Problems

- Port disconnected: check host, port, serial device, or packet engine.
- No packets: check antenna, audio, squelch, Direwolf, TNC, or AGWPE.
- Duplicate packets: disable extra receive paths and compare source tags.

## Troubleshooting

Use one RF source at a time until receive works reliably. Then add other sources slowly.
