# APRS Command Safety and Transmit Guide

APRS Command is receive-first. APRS Command does not transmit by default. Transmit features must remain disabled until an operator explicitly configures them and understands the result.

## Disabled By Default

APRS Command starts with these safety defaults:

- station transmit disabled
- APRS-IS transmit disabled
- RF transmit disabled
- iGate disabled
- digipeater disabled
- beaconing disabled
- weather beaconing disabled
- object transmit disabled until configured
- message transmit disabled until configured
- REST API disabled
- WebSocket disabled
- file hooks disabled
- plugin loading disabled
- replay, simulation, and training cannot transmit
- REST API, WebSocket, file hooks, and plugins cannot bypass safety

## Operator Responsibility

You are responsible for legal operation. Before any transmit:

1. Use your licensed callsign.
2. Set the correct SSID.
3. Confirm your station position.
4. Confirm symbol and comment text.
5. Confirm APRS-IS settings.
6. Confirm RF/TNC hardware and audio/PTT configuration.
7. Confirm beacon interval and path are appropriate.
8. Confirm local regulations and band plans.

## APRS-IS Transmit

APRS-IS transmit is separate from receive. Do not publish, commit, screenshot, or share passcodes. APRS-IS receive can work without enabling transmit.

## RF Transmit

RF transmit is separate from APRS-IS transmit. RF transmit requires:

- explicit RF transmit enablement
- valid station profile
- valid path
- connected and transmit-enabled RF port
- centralized transmit safety approval

## Beaconing

Beaconing is disabled by default. Do not use short beacon intervals on RF. Mobile beacon decisions should respect SmartBeaconing safety limits and minimum intervals.

## iGate and Digipeater Modes

iGate and digipeater features are disabled by default. Monitor modes may analyze packets, but monitor mode is not permission to gate or retransmit.

## Objects, Messages, and Weather

Object transmit, message transmit, and weather beacon transmit are disabled until configured. Editing an object or composing a message does not automatically transmit.

## Replay, Simulation, and Training

Replay, simulation, and training data are tagged as non-live sources. They must not be allowed to transmit packets.

## Extension Safety

REST API, WebSocket streams, file hooks, plugins, and drivers must go through the same centralized safety checks as built-in features. They must not bypass transmit controls.

## Safe Testing Checklist

1. Start APRS Command.
2. Confirm transmit is disabled.
3. Confirm APRS-IS transmit is disabled.
4. Confirm RF transmit is disabled.
5. Connect receive-only APRS-IS or RF input.
6. Watch the packet monitor.
7. Verify no generated packets are transmitted.
8. Review logs for blocked attempts if you intentionally test safety gates.
