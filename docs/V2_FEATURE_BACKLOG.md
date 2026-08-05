# APRS Command — v2 Feature Backlog (in-app)

Features intended for **this desktop codebase** but deliberately deferred past v1.0. Unlike
`docs/FUTURE_PROJECTS.md` (which holds ideas that would become *separate* projects, e.g. the mobile
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
  not just messages.
