# APRS Command — v2 Feature Backlog (in-app)

Features intended for **this desktop codebase** but deliberately deferred past v1.0. Unlike
`docs/planning/FUTURE_PROJECTS.md` (which holds ideas that would become *separate* projects, e.g. the mobile
app), everything here would ship inside APRS Command itself in a later release.

> Nothing here is scheduled or committed. Recorded so the ideas aren't lost while v1.0 is locked and
> proven.

---

## 1. Voice / spoken announcements (text-to-speech)

**Status:** v2 idea — not scheduled. **Requested trigger:** speak an announcement when an APRS **message
addressed to your callsign** arrives (e.g. *"New message from W1ABC."*, optionally reading the message
text).

**Why it's wanted:** hands-free / eyes-free awareness. When you're driving, running a net, or working
away from the screen, an audible cue means you don't miss traffic meant for you. Also an accessibility
win for low-vision operators.

### Firm requirement — every announcement is independently switchable
**All twelve announcement types below ship, and each one has its own on/off toggle.** No operator is
forced to hear a category they don't want. Specifically:
- A **master voice-announcements on/off** switch (off by default) gates the whole feature.
- Under it, a **per-announcement toggle for every row** in the table (each independently on/off).
- Sensible defaults when the master is first enabled: **messages-to-me (#1)** and **emergency (#3)**
  on; everything else off, so the operator opts in to the rest rather than opting out of noise.

### Candidate announcement triggers (menu of what could be spoken)
Each should be individually toggleable — the goal is *chosen* awareness, never a chatterbox.

| # | Trigger | Example spoken text | Notes |
|---|---|---|---|
| 1 | **Message to your callsign** (requested) | "New message from W1ABC: meet at the EOC." | Core ask. Option: sender only vs. sender + full text. |
| 2 | **Message delivery / ack status** | "Your message to W1ABC was acknowledged." / "…delivery failed." | Closes the loop on your own outgoing messages. |
| 3 | **Emergency / priority packets** | "Priority: emergency beacon from KE4CON." | Distinct urgent earcon; APRS EMERGENCY, Mayday, SOS. Highest priority, hardest to suppress. |
| 4 | **NWS / severe-weather alerts** | "Weather alert: tornado warning." | From received NWS/weather-warning packets. |
| 5 | **APRS bulletins & announcements** | "New bulletin: net starts at 1900 local." | Reads new bulletin lines once. |
| 6 | **Watchlist station on the air** | "KE4CON is now on the air." | For a user-defined list of callsigns of interest; first-heard or after being absent. |
| 7 | **Geofence / range-ring crossing** | "Mobile 1 has entered the 5-mile ring." | Ties into existing EmComm geofence/range-ring tooling; great for tracking a specific tactical station. |
| 8 | **Proximity alert** | "New station within 2 miles: W4XYZ." | Distance-from-you threshold. |
| 9 | **Net check-ins** | "Check-in: W1ABC, Net Control logged." | During an active net, announce each new check-in. |
| 10 | **Connection status changes** | "APRS-IS disconnected." / "TNC connection restored." | Important for an unattended iGate/digipeater — hear when a link drops. |
| 11 | **Your own transmit events** | "Position beacon transmitted." / "Transmit blocked — exercise mode." | Confirms beacons; reinforces transmit-safety state audibly. |
| 12 | **Telemetry threshold alarms** | "Telemetry alarm: KE4CON battery low." | If a watched station's telemetry crosses a set threshold. |

### Design considerations to spec before building
- **Cross-platform TTS is the real work.** `System.Speech` is Windows-only; a proper solution needs a
  TTS abstraction (service/driver pattern, consistent with the app's architecture) mapping to the OS
  engine per platform — Windows SAPI, macOS `say` / `NSSpeechSynthesizer`, Linux `espeak-ng` / speech-
  dispatcher. Ship a null/beep-only fallback where no engine is present.
- **Master switch + per-category toggles** (the table above), plus **volume**, **voice/rate** selection.
- **Anti-spam:** rate limiting, coalescing bursts, **quiet hours / do-not-disturb**, and a max-queue so a
  flood of packets can't back up minutes of speech.
- **Priority tiers with distinct earcons/chimes** before speech (emergency ≠ routine message).
- **Phonetics option:** read callsigns in ITU phonetics ("Whiskey One Alpha Bravo Charlie") vs. literal.
- **Summary vs. full read:** short form ("Message from W1ABC") vs. read the body.
- **Receive-side only — no transmit-safety impact.** Announcements never key the radio; they're purely
  local output and are safe in exercise/simulation/replay modes (could even be useful in training).
- **Accessibility framing:** if built well, this doubles as a screen-reader-style aid for the whole app,
  not just messages. See §10, which builds on this feature.

---

## 2. Bulletin / announcement transmit (BLN)

**Status:** v2 idea. **Gap it closes:** today the app is **receive-only** for bulletins — it displays
incoming `BLN`/announcement traffic but has no way to *send* one. Net control announcing "net starts at
1900 local" is a core EmComm task that currently isn't possible.

**What it is:** compose and transmit APRS bulletins (`BLNn`) and announcements (`BLN` + letter),
addressed the APRS way, with the standard content-length limits.

**Design notes:** route through the existing transmit-safety authority like every other transmit path;
honor Exercise Traffic Marking (a drill bulletin should carry the EXERCISE tag); a small compose UI in
the Message Center; optional scheduled/repeat bulletin (announce every N minutes during an event).

## 3. Map snapshot export (PNG / PDF)

**Status:** v2 idea. **What it is:** export the current map view — *including your drawings and
annotations* — as a PNG or PDF for a briefing, situation report, or an after-action package.

**Why it's wanted:** pairs directly with the ICS-213/214/309 after-action exports already built — an
operator can attach a labeled map picture to the incident record. Also useful for pre-op briefings.

**Design notes:** render the Mapsui canvas plus the drawing layer to a bitmap; add a title block
(event name, date/time, callsign) matching the after-action style; offer "current view" vs. "fit all
stations." Receive-side; no transmit involved.

## 4. Station track recording → GPX / KML export

**Status:** v2 idea. **What it is:** record the actual movement *track* of any station (or your own)
over a session and export it as GPX or KML.

**Why it's wanted:** the app already draws live trails and already imports/exports GPX/KML for shapes —
this is the missing half: a persisted, exportable track for post-event review, SAR debriefs, or
dropping into Google Earth / a GPS.

**Design notes:** reuse the GPX/KML writer from the drawing tools; let the operator pick which station(s)
to record and the time window; store points with timestamps; tie into the after-action export.

## 5. Automatic object refresh

**Status:** v2 idea. **Gap it closes:** APRS objects **time out and disappear** unless periodically
re-transmitted. Right now an operator must manually re-send an object to keep it alive.

**What it is:** an option to automatically re-transmit your owned objects at a chosen interval (e.g.
every 10 minutes) so shelters, staging areas, and hazards stay on everyone's map through an operation.

**Design notes:** per-object or global interval; only re-sends objects you own; respects transmit-safety
and Exercise Traffic Marking; stops automatically when an object is killed.

## 6. Satellite APRS

**Status:** v2 idea — a large, self-contained feature. **What it is:** work APRS through amateur
satellites — pass predictions (ISS, PSAT-class birds), Doppler awareness, and satellite-gateway support.

**Why it's wanted:** broad, enthusiastic ham appeal, and it would draw a new audience to the app.
Distinct enough that it could be a well-scoped v2 headline.

**Design notes:** needs Keplerian/TLE data (a fetch + cache), a pass-prediction engine, and a "satellite
mode" that adjusts paths/timing. Sizable — spec carefully before committing.

## 7. Telemetry history & charts

**Status:** v2 idea. **What it is:** the app already receives APRS telemetry; this plots a station's
analog channels over time as charts, with optional threshold alarms.

**Why it's wanted:** turns raw telemetry numbers into something you can actually read at a glance, and
dovetails with the voice "telemetry alarm" (§1, #12) and with battery/voltage monitoring of remote gear.

**Design notes:** ring-buffer of recent telemetry per station (session-local, consistent with the
stateless model); simple line charts via the native Skia rendering already used for the far-field/2D
plots; user-set high/low thresholds that raise an alert (and optionally a spoken one).

## 8. Message auto-responder / away message

**Status:** v2 idea. **What it is:** an optional, configurable APRS auto-reply to incoming messages
(e.g. "Mobile — will respond when parked").

**Why it's wanted:** common in other APRS clients; useful when operating away from the keyboard.

**Design notes:** **transmit-safety-gated and off by default** (it transmits!); rate-limited so it can't
loop or spam; one reply per sender per cooldown window; honors Exercise Traffic Marking; clear on-screen
indicator that auto-reply is armed (it puts you on the air unattended).

## 9. Recipient groups / distribution lists

**Status:** v2 idea. **What it is:** named groups of callsigns (e.g. "Net Roster," "Shelter Team") so an
operator can send one message to the whole group in a click.

**Why it's wanted:** speeds up net traffic and multi-station coordination. Ties into Net Control roster
and message templates.

**Design notes:** APRS messages are one-to-one on the wire, so a group send fans out to individual
addressed messages (each still tracked/ack'd separately); groups persist in settings; respects
transmit-safety and the 67-char/exercise-marking rules per message.

## 10. Accessibility — making APRS Command usable by the visually impaired

**Status:** v2 initiative (larger than a single feature). **Goal:** a blind or low-vision operator can
install, set up, and *operate* APRS Command productively — because EmComm needs every capable operator,
and APRS is one of the more accessible modes (it's fundamentally text and audio, not visual-only).

**Why it matters:** amateur radio has a long tradition of blind operators; an APRS client they can truly
use is rare and valuable. Built well, this also benefits everyone (keyboard power-users, small screens,
bright-sunlight field conditions).

**The pieces (build toward "operable without seeing the screen"):**
- **Screen-reader support.** Proper Avalonia UI Automation (accessible names, roles, and values on every
  control, list item, and badge) so NVDA/JAWS on Windows, VoiceOver on macOS, and Orca on Linux can read
  the interface. This is the foundation — most other pieces build on it.
- **Full keyboard navigation.** Every action reachable and operable from the keyboard, in a logical tab
  order, with visible focus and documented shortcuts — no mouse-only controls.
- **Spoken awareness (builds on §1).** Voice announcements become the eyes-free layer: new messages,
  emergencies, check-ins, connection changes read aloud. A "read the station list / read this message"
  command.
- **An eyes-free / audio-first operating mode.** A mode tuned for operating by ear: spoken new-station
  and proximity call-outs, spoken bearing/distance to a selected station ("W4XYZ, 12 miles, bearing
  270"), and a way to browse the station list and messages entirely by keyboard + speech.
- **Map described, not just drawn.** Since a map is inherently visual, provide a text/spoken alternative:
  "nearest 5 stations" with distance and bearing, a spoken summary of what's on screen, and keyboard
  cycling through markers with each one announced.
- **Visual accessibility too.** High-contrast theme, colorblind-safe palettes, and adjustable font / UI
  scaling for low-vision (not fully blind) operators.
- **Braille** comes for free once screen-reader support is solid (screen readers drive braille displays).

**Design notes:** this is mostly disciplined UI work (automation peers, focus order, no unlabeled
controls) plus the voice layer from §1 — not a rewrite. Best approached as a sustained pass with a blind
operator testing along the way. Pairs naturally with §1 and §11.

## 11. Localization (multi-language UI)

**Status:** v2 idea. **What it is:** translate the interface (and eventually the docs) so operators
outside English-speaking regions can use APRS Command in their own language.

**Design notes:** externalize UI strings into resource files; right-to-left readiness; keep APRS
protocol tokens (callsigns, paths) untranslated. A large but mechanical effort once the string
extraction is done.

## 12. Settings sync across machines

**Status:** v2 idea. **What it is:** move your station profile and settings between computers easily
(the author runs both macOS and Windows), via a clean export/import bundle — or optional sync.

**Design notes:** settings are already one JSON tree, so a signed/portable export bundle is
straightforward; keep it local-file based (no cloud dependency) to stay consistent with the
no-backend, privacy-first posture; never include secrets/tokens in a shared bundle unless the operator
opts in.

## 13. Local REST API — build the network transport (external integrations)

**Status:** v2 idea. **What it is:** `AprsCommand.Api.LocalRestApiService` already defines the full
integration API — endpoints (`/api/stations`, `/api/stations/{callsign}`, `/api/objects`, `/api/weather`,
`/api/gps`, `/api/alerts`, `/api/rf-diagnostics`, `/api/ports`, plus permissioned POST submit/transmit),
Bearer-token auth, read-only mode, and per-minute rate limiting — but it has **no network transport yet**:
`StartAsync()` only flips a state flag and nothing calls `HandleAsync`. Build the actual HTTP server (an
`HttpListener` or Kestrel host) that binds the configured port (8765), maps requests to
`LocalRestApiRequest`, calls `HandleAsync`, writes `LocalRestApiResponse`, and adds CORS. Pair it with the
parallel `WebSocketEventStreamService` foundation (same status — logic exists, no socket) for live push.

**Why:** this is the *proper* long-term integration surface. Today the FieldCommand IMS tactical map is
fed from the `MobileCompanionServer` in a tokenless fixed-port (8080) LAN mode — pragmatic and working now,
but the companion server is really the phone-companion, not the integration API. Once the Local REST API
is network-live, **move the FieldCommand feed onto it**: richer contract, safer model (explicit enable,
read-only, rate limiting, per-endpoint write/transmit permissions), and WebSocket live updates instead of
polling. On the FieldCommand side the switch is trivial — the tactical map's RF source is just a host:port
(Settings → APRS Sources).

**Design notes:** keep the locked-down defaults (disabled, localhost-only, token required, read-only) and
let the operator opt into a LAN-exposed, tokenless, read-only feed for a *trusted* EMCOMM-NET — the same
no-login posture FieldCommand's own services use on that isolated network. The auth / rate-limit / routing
logic in `HandleAsync` is already there and tested; the HTTP transport is the only missing piece.
