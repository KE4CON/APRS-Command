# APRS Command RF/TNC Setup Guide

RF/TNC support lets APRS Command receive packets from local packet hardware or packet engines. Test receive-only first.

## Direwolf TCP KISS

Direwolf commonly exposes TCP KISS on `127.0.0.1` port `8001`.

1. Start Direwolf separately.
2. Confirm Direwolf is receiving audio and decoding packets.
3. In APRS Command, configure the Direwolf or TCP KISS profile.
4. Use host `127.0.0.1`.
5. Use KISS port `8001`.
6. Keep transmit disabled.
7. Connect and watch the packet monitor.

## Serial KISS

Serial KISS supports hardware TNCs over USB or serial.

1. Connect the TNC.
2. Identify the serial port name.
3. Configure baud rate, data bits, parity, stop bits, and handshake.
4. Keep transmit disabled.
5. Connect and watch for decoded packets.

On Linux, serial permission errors may require adding your user to the serial device group and logging out/in.

## AGWPE

AGWPE-compatible engines commonly listen on a local TCP port.

1. Start the AGWPE-compatible packet engine.
2. Configure host `127.0.0.1` unless using another machine.
3. Configure the AGWPE port.
4. Select the radio port if multiple ports exist.
5. Keep transmit disabled.
6. Watch the packet monitor.

## Multiple Ports

APRS Command can track multiple APRS/RF sources. Each port should show:

- port name
- port type
- receive enabled
- transmit enabled
- connection state
- packet counters
- last packet time
- last error

## RF Transmit Safety

RF transmit remains disabled by default. A port is not transmit-safe unless:

- global transmit safety permits it
- RF transmit is explicitly enabled
- the individual port transmit flag is enabled
- the port is connected
- the packet is valid

## Troubleshooting No Packets

1. Verify audio or RF input outside APRS Command.
2. Confirm the port is connected.
3. Confirm receive is enabled.
4. Check packet monitor.
5. Check Direwolf, TNC, or AGWPE logs.
6. Try one receive port at a time.

## Troubleshooting Duplicate Packets

Duplicate packets can happen when the same RF traffic arrives through more than one input. Temporarily disable extra ports and compare packet source tags.
