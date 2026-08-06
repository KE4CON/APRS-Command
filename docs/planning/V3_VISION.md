# APRS Command — v3 Vision (in-app)

Forward-looking ideas for a **third major version** — bigger leaps than the v2 backlog, captured so they
aren't lost. Companion to [`V2_FEATURE_BACKLOG.md`](V2_FEATURE_BACKLOG.md) (the next release's features)
and [`FUTURE_PROJECTS.md`](FUTURE_PROJECTS.md) (ideas that would become *separate* projects, like a mobile
app).

> Nothing here is scheduled, committed, or even fully specced — this is deliberately a crystal-ball
> document. It exists to record direction, not to promise dates.

## The arc

- **v1** made APRS Command a great *display* of the operating picture — see, message, plan, practice.
- **v2** fills the gaps and makes it usable by *everyone* — voice announcements, an accessibility
  initiative, satellite APRS, bulletin transmit, and more (see the v2 backlog).
- **v3** is where it grows from a display into an *intelligent, collaborative, resilient operating
  platform.* Three pillars below.

## Versioning cadence

Point releases (**v1.x, v2.x**) carry bug fixes and small polish between the majors. **2.0** ships the
v2 backlog; **3.0** carries the vision here. Nothing in this file blocks a bug-fix release.

---

## Pillar 1 — Intelligence (from *showing* data to *interpreting* it)

### 1.1 Assisted situational awareness
The app watches for what matters and surfaces it: a station that has **gone silent**, one that **stopped
moving unexpectedly**, or **trajectory / intercept prediction** ("MOBILE-1 and MOBILE-3 are converging on
grid EM73"). Dead-reckoning already exists in v1 — this is the next rung up from it. Runs locally; no
cloud dependency required.

### 1.2 Auto situation summaries
A one-tap (or spoken) plain-language recap of the operating picture: *"Since 1400: 3 new check-ins, the
weather station is reporting 40 mph gusts, MOBILE-1 stopped 8 minutes ago."* Ideal for shift handoffs and
for seeding an after-action report. Could be assisted by a **local** language model so nothing leaves the
machine.

### 1.3 Natural-language & voice command control
Ask the app questions ("where's net control?", "measure to W4XYZ") and command it by voice ("start
after-action logging," "place an object here"). This is the **payoff of the v2 voice + accessibility
work**: hands-free field operating *and* a blind operator running the entire app by speech. The single
most futuristic-feeling item on this list.

---

## Pillar 2 — Collaboration & data

### 2.1 Shared operating picture (LAN / peer, no internet)
A team sees the **same** annotated map — objects, drawings, tactical labels — updating in real time over a
local network or a private server, even with no internet.

> **Scope guardrail:** this must stay **shared awareness / annotation only.** The moment it grows into
> resource tracking, T-cards, or incident/team management, that is **FieldCommand IMS's** job, not this
> app's. Keep APRS Command the *picture*, not the *incident system*.

### 2.2 Opt-in historical archive & analytics
APRS Command is intentionally **stateless** today (no session history — a deliberate privacy and
simplicity choice). v3 could add an *optional* local datastore that unlocks post-event analytics, trend
reports, and multi-session review — while keeping stateless-and-private the **default**, never forced on.

### 2.3 Multi-source data fusion
Overlay other live layers onto the APRS picture: NWS warning polygons, wildfire perimeters, road
closures, and even ADS-B aircraft (for search-and-rescue). A genuine situational-awareness leap for
served-agency work — the map becomes the one screen an operator needs.

---

## Pillar 3 — Resilience & terrain

### 3.1 Mesh / off-grid
Integrate LoRa / Meshtastic-style mesh networking so the operating picture **survives when both the
internet and the normal VHF APRS network are down.** This is the most on-mission idea on the list —
situational awareness when everything else has failed — and it builds on the LoRa support already present
in the codebase.

### 3.2 Line-of-sight & terrain reachability
A terrain-aware layer that shows **who you can actually reach** given the hills between you — turning the
map from "where are they" into "who can I work." Light propagation awareness only; take care not to fully
duplicate the separate ActivationPlanner project's VOACAP/NEC modeling.

### 3.3 A real automation engine
Grow v1's alerts and net-scripts into a visual **"when X, do Y"** builder: *geofence enter → send a
message + place an object + speak an alert.* A rules platform, not just notifications.

---

## Maturity

### 4.1 Plugin ecosystem
The v1 plugin/driver *foundation* grows into a real SDK plus a community catalog, so operators extend the
app themselves — new transports, map overlays, exporters — without touching core. Turns a program into a
platform.

---

## Scope guardrails (the whole point of staying focused)

Keep OUT of APRS Command, no matter how tempting:
- **Incident / resource / team management** (ICS T-cards, resource status, tasking) → **FieldCommand IMS.**
- **Rig control and QSO logging** (CAT frequency/mode, contest/award logging) → **IcomRigControl.**
- **A native mobile app** → a **separate project** (see `FUTURE_PROJECTS.md`).

APRS Command stays the individual operator's *situational-awareness picture.* That focus is why it's
clean; guarding it is a feature, not a limitation.

## If forced to pick two for 3.0

- **Voice-command control (§1.3)** — it makes the entire accessibility/voice investment pay off and feels
  genuinely years-ahead.
- **Mesh / off-grid resilience (§3.1)** — the most "APRS Command mission" item here: a shared operating
  picture that holds up when the grid, the internet, and the repeaters are all gone.
