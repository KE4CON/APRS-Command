# Settings Menu Specification — APRS Command

Companion to `DESIGN_PROPOSAL.md` and `UIVIEW_FEATURE_PARITY.md`. This document
defines the structure of the **Settings** panel: what sections exist, what each
one configures, which settings APRS Command **owns** versus **delegates** to an
external RF engine (GrayWolf / Direwolf), and the plain-language descriptions
that should appear in the UI next to each connection choice.

It does not change the engine — almost everything below already exists at the
service/transport layer (see status tags). This is a UI-wiring and
information-architecture spec.

---

## Status legend (same as the parity doc)

- **WIRED** — implemented and connected to the UI.
- **ENGINE** — implemented and tested in the service/transport layer, not yet
  wired to a live editor.
- **MONITOR** — surfaced in the UI as a read-only status display (no editing).
- **PLANNED** — agreed in design, not yet built.
- **OPTIONAL** — deliberately out of current scope; recorded so it isn't lost.

---

## Guiding principle: owned vs delegated

APRS Command is the **operator-facing client**. In a GrayWolf or Direwolf
deployment, the RF engine owns the radio side — the AFSK modem, the digipeater,
and the iGate. The client connects to it as a KISS-TCP or AGW client and should
**not** duplicate that configuration.

That splits every setting into one of two roles:

- **OWNED** — APRS Command is the source of truth and provides a full editor.
  Examples: station identity, beaconing, the connection to the TNC, maps,
  messaging, alerts.
- **DELEGATED** — configured in the RF engine, not here. APRS Command shows a
  read-only **monitor** so the operator can see what's happening, but the
  controls live in GrayWolf/Direwolf. Examples: digipeater rules, iGate gating,
  the modem itself.

The one nuance: if APRS Command is run **without** GrayWolf/Direwolf — driving a
plain TNC directly and acting as its own digipeater/iGate — then the delegated
items flip to owned and need editors. The design ships them as monitors by
default, with editing available but clearly gated behind "APRS Command owns the
RF role" (see §Digipeater / iGate below).

---

## Settings panel structure

Proposed sections, replacing the current ad-hoc tab set. Order is top-to-bottom
in the panel.

| # | Section | Role | Status today |
|---|---|---|---|
| 1 | Station | OWNED | WIRED (editor exists) |
| 2 | Connections | OWNED | ENGINE (only KISS-TCP partially wired) |
| 3 | Beaconing | OWNED | ENGINE |
| 4 | GPS | OWNED | ENGINE + MONITOR (status view exists) |
| 5 | Maps & Display | OWNED | Partial (offline-map manager exists; themes PLANNED) |
| 6 | Messaging | OWNED | ENGINE |
| 7 | Alerts, Geofence & Filters | OWNED | ENGINE |
| 8 | Weather | OWNED (bonus) | ENGINE (some setup views exist) |
| 9 | Logging & Diagnostics | OWNED | ENGINE |
| 10 | Digipeater / iGate | DELEGATED | MONITOR (read-only views exist) |
| 11 | Integrations (Advanced) | OWNED (bonus) | ENGINE |
| 12 | First Run | OWNED | WIRED (wizard exists) |

---

## 2. Connections — the core of this revision

This is where DigiRig / SignaLink / "regular old TNC" get resolved. There are
**three** connection types, plus APRS-IS — not five. The reason is a
classification fact worth stating plainly in the docs and the UI:

> **DigiRig and SignaLink are not TNCs.** They are USB sound-card / PTT
> interfaces with no modem of their own. The AFSK demodulation still happens in
> software (Direwolf / GrayWolf), which owns the sound device. APRS Command
> never talks to the DigiRig or SignaLink directly — it connects to the software
> modem in front of them. So both collapse into the **Network TNC (KISS-TCP)**
> path. They do not get their own connection mode.
>
> Trap to call out in the UI: a DigiRig's serial port carries **PTT/CAT, not
> KISS**. Do not point a Serial KISS connection at a DigiRig COM port — it will
> not work.

### The four connection choices, with in-UI helper text

Each description below is written to appear as the helper text under that option
in the Connections section, so the choice isn't a bare label.

---

**Network TNC (KISS over TCP)** — *engine: `TcpKissConfiguration`,
`TcpKissClient`, `DirewolfProfileService`*

> Connect to a software TNC that's already running and reachable over the
> network — on this computer or another on your LAN. APRS Command opens a TCP
> connection and exchanges KISS data with it; the software TNC does the actual
> radio audio work. **This is the path for GrayWolf and Direwolf** — and
> therefore also the path when your radio interface is a **DigiRig or SignaLink**,
> because those run behind Direwolf. You connect to Direwolf, not to the
> interface.
>
> Settings: Host (default `127.0.0.1` for the same computer), Port (Direwolf
> default `8001`).
> Pick this if: you run GrayWolf / Direwolf, or any USB sound-card interface
> behind them.

**AGWPE (TCP)** — *engine: `AgwpeConfiguration`, `AgwpeClient`,
`AgwpeFrameCodec`*

> The same idea as KISS-over-TCP — a network connection to a software TNC — but
> speaking the older AGWPE host-mode protocol instead of KISS. GrayWolf's AGW
> interface, the classic AGW Packet Engine, and UZ7HO SoundModem all offer this.
>
> Settings: Host, Port (AGW default `8000`).
> Pick this if: your modem exposes AGW rather than (or in addition to) KISS-TCP.

**Serial KISS TNC (hardware TNC)** — *engine: `SerialKissConfiguration`,
`SerialKissClient`*

> Talk **directly** to a hardware TNC over a serial port — a real COM/tty port,
> a USB-to-serial adapter, or Bluetooth serial. A hardware TNC has its own modem
> built in, so there's no software modem in the chain; APRS Command exchanges
> KISS data straight over the wire. **This is your "regular old TNC" path**:
> Kantronics KPC-3+, MFJ, TNC-X, a Kenwood TM-D710's internal TNC, a Mobilinkd,
> and similar.
>
> Settings: Serial port, baud rate (commonly `9600`; some run `1200`), and the
> usual line settings (data bits / parity / stop bits / handshake — typically
> 8-N-1, no handshake).
> Pick this if: you have a real hardware TNC on a serial / USB-serial /
> Bluetooth port.
> **Not** for a DigiRig serial port (that's PTT/CAT, not KISS).

**APRS-IS (internet)** — *engine: `AprsIsClient`, `AprsIsServerManager`,
`AprsIsLoginLineBuilder`*

> Connect over the internet to the global APRS Internet Service — a worldwide
> network of servers that relay APRS traffic. No radio involved. You log in with
> your callsign and a passcode (validation number) and can apply a server-side
> filter to limit what you receive (for example, stations within a set distance
> of you). Receiving works without a passcode; transmitting your own position to
> the internet requires it.
>
> Settings: Server (e.g. a rotate from `status.aprs2.net`), Port (commonly
> `14580` for a filtered feed), Passcode, Filter.
> Pick this if: you want internet APRS data — for monitoring, or as the internet
> side of an iGate. Receive-only by default.

### Multiple simultaneous ports

`AprsPortManager` already supports more than one active port (parity §1). The
Connections section should let the operator enable several at once — e.g. a
Serial KISS radio port **and** an APRS-IS feed — and the existing read-only Ports
monitor shows the health of each. Transmit safety is per-port (the engine models
`ReceiveEnabled` / `TransmitEnabled` separately on every connection).

### OPTIONAL future: "Managed local modem"

A convenience mode where APRS Command **launches and configures Direwolf** for
the operator — so a DigiRig / SignaLink owner picks an audio device and PTT line
in this UI instead of hand-editing `direwolf.conf`. This is the only scenario in
which DigiRig / SignaLink would become first-class, named choices with audio +
PTT settings.

Status: **OPTIONAL, not started.** There is currently no modem-launching or
audio-device code in the client (the "Direwolf" tab today is a connection
profile plus setup notes, not a launcher). Recommendation: **defer.** It
duplicates what Direwolf/GrayWolf config already does and pulls the client back
toward owning RF concerns that were deliberately pushed out. Recorded here so the
idea isn't lost, not committed to.

---

## 1. Station

Identity and position: callsign, SSID, APRS symbol (table + code + overlay),
position (manual lat/lon or from GPS), station comment / status text, and beacon
path (e.g. `WIDE1-1,WIDE2-1`). Editor already exists and is wired. Keep, and add
an inline symbol picker (currently table/code are typed).

## 3. Beaconing

Fixed-interval position/status beacons, SmartBeaconing for mobile, compressed vs
uncompressed format, and (parity gap) a scheduler for timed beacons / status /
server connects. All engine-side (`BeaconSchedulerConfiguration`,
`SmartBeaconingConfiguration`). **Transmit safety lives here and must stay
explicit** — transmit disabled by default, APRS-IS TX and RF TX as separate
switches, matching what the Station editor already states.

## 4. GPS

Source selection: NMEA serial or gpsd (good on Linux/Pi). A read-only fix status
view already exists; add the source editor in front of it.

## 5. Maps & Display

Tile source and offline/cached maps (offline-map manager exists), layer toggles,
units (miles / km), coordinate format (decimal / DMS / Maidenhead), and the three
themes (Light default, Dark and High Contrast opt-in — see UI design proposal).
Units / coordinate format / themes are PLANNED editors.

## 6. Messaging

Message groups, auto-ack, and retry behaviour (`AprsMessageRetryConfiguration`).

## 7. Alerts, Geofence & Filters

Proximity / rule-based alerts (`AlertRuleService`), geofences (`GeofenceService`),
station filtering, and audio alarms. All engine-side.

## 8. Weather

WX station input drivers (Tempest, Peet Bros/ULTIMETER, Davis, Ambient,
Ecowitt/GW1000, plus Cumulus/WeeWX/Weather Display imports) and WX beaconing
behind transmit safety. Exceeds UI-View (parity §6); some setup views already
exist.

## 9. Logging & Diagnostics

Raw + decoded traffic logs (persist + rotate), log replay, telemetry, and RF
diagnostics. All engine-side; several are bonus depth beyond UI-View (parity §8).

## 10. Digipeater / iGate — DELEGATED

In a GrayWolf / Direwolf deployment these are configured **in the RF engine**,
not here. APRS Command ships the existing **read-only monitors**
(`DigipeaterStatusView`, `IGateStatusView`) so the operator can watch behaviour —
which is the correct posture and should be the default.

The engine does contain a safe digipeater and an iGate (`DigipeaterService`,
`IGateService`, `IGateMonitorService`; parity §7). Those editors should be
exposed **only** when APRS Command is the RF owner — i.e. running a plain
TNC/modem with no GrayWolf/Direwolf doing the job. Recommended UI: the section
defaults to monitor mode with a clearly labelled "APRS Command is handling
digipeating / iGating itself" switch that reveals the editors. Off by default;
conservative defaults to avoid RF flooding.

## 11. Integrations (Advanced)

Local REST API, WebSocket event stream, and file hooks (`AprsCommand.Api`), plus
the training / simulation / replay modes. Bonus capabilities; group under an
Advanced heading so they don't clutter the common path.

## 12. First Run

The setup wizard that already exists. Should walk a new operator through the
minimum to get on the air: Station identity → one Connection → confirm
receive-only → done. Everything else is reachable later from the sections above.

---

## Mapping to the parity doc

- Connections → parity **§1 Connectivity and TNC support** (APRS-IS, serial KISS,
  KISS-TCP/Direwolf, AGWPE, multi-port — all ENGINE today).
- Beaconing / GPS → parity **§5**.
- Weather → parity **§6**.
- Digipeater / iGate → parity **§7** (note the delegated posture above).
- Logging & Diagnostics → parity **§8**.
- Alerts → parity **§9**.

## Suggested build order

1. **Connections** section with the three types + APRS-IS and the helper text
   above. This is the load-bearing one — it's also the GrayWolf integration path
   (KISS-TCP / AGW) the project already identified as the next milestone. Rename
   the current "Direwolf" tab to **Network TNC (KISS-TCP)**; it is not
   Direwolf-specific.
2. Real serial-port discovery to replace `PlaceholderSerialPortDiscovery`
   (needed before Serial KISS is usable).
3. Station symbol picker; Beaconing editor (behind transmit safety).
4. GPS source editor; Maps & Display (units / coordinates / themes).
5. Messaging; Alerts / Geofence / Filters.
6. Leave Digipeater / iGate as monitors; build their editors only if/when a
   no-GrayWolf RF-owner mode is wanted.
7. Advanced / Integrations last.
