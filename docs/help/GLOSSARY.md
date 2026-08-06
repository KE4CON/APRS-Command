# APRS Command Glossary

## APRS

Automatic Packet Reporting System, an amateur-radio packet system for positions, messages, objects, weather, telemetry, and status information.

## APRS-IS

The APRS Internet Service network. It carries APRS packets over the internet.

## Packet

A single APRS line such as a position report, message, weather report, object, or status.

## Callsign

The licensed station identifier, such as `N0CALL`.

## SSID

A suffix after a callsign, such as `N0CALL-7`, often used to identify station type or device.

## Path

The APRS routing path after the destination, often used for RF digipeating.

## Digipeater

An RF station that repeats packets. APRS Command digipeater features are disabled by default.

## iGate

An internet gateway that can move packets between RF and APRS-IS. APRS Command iGate features are disabled by default.

## Beacon

A periodic station position or status packet. Beaconing is disabled by default.

## Object

An APRS point of interest owned or reported by a station.

## Item

An APRS point of interest similar to an object, using a different packet format.

## Tactical Label

A friendly display name that can be shown while preserving the real callsign.

## KISS

Keep It Simple Stupid, a protocol used to move packet frames between software and a TNC or packet engine.

## TNC

Terminal Node Controller, hardware or software that handles packet radio data.

## Direwolf

A popular software TNC that can decode and encode packet radio and expose TCP KISS.

## AGWPE

AGW Packet Engine protocol support for packet radio software.

## GPSD

A Linux GPS service that provides GPS data over a local network interface.

## NMEA

A common text sentence format output by GPS receivers.

## RF

Radio frequency operation. RF transmit is disabled by default in APRS Command.

## Passcode

An APRS-IS passcode used for APRS-IS login/transmit workflows. Do not publish or commit passcodes.

## Filter

An APRS-IS filter limits which packets are sent to the client.

## Symbol

An APRS table/code pair that describes the station, object, item, or weather marker type.

## Telemetry

APRS data carrying numeric or digital sensor values.

## Weather Packet

An APRS packet containing weather data such as wind, temperature, rain, humidity, or pressure.

## Geofence

A geographic area, such as a circle or polygon, used for enter/exit monitoring.
