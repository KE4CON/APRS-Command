# APRS Command — Decisions Log

A running record of significant design and engineering decisions: what was decided, and **why**.
Newest entries at the bottom of each section. This log started on 2026-07-31; entries before that
date are reconstructed from the codebase and existing docs to capture the reasoning behind choices
already made.

---

## Foundational decisions (reconstructed)

### F1 — License: GPL v3
APRS Command is GPL v3 so it can never be taken closed-source or lost the way UI-View32 was when its
author passed and the source was destroyed. The license is a promise to the community that the tool
can always be carried forward. All contributions must be GPL-v3-compatible.

### F2 — Strict layered architecture, compiler-enforced
Separate `.csproj` per layer (`Aprs.Core` → `Aprs.Transport` → `Aprs.Services` → `Aprs.Mapping`/
`Aprs.Desktop`, plus `AprsCommand.Api`/`.Contracts`). `ProjectReference` direction enforces the
boundaries at compile time. **Why:** protocol parsing, transport I/O, business logic, and UI evolve
independently; keeping them physically separated prevents the coupling that made older clients
un-portable.

### F3 — Hand-rolled MVVM (no CommunityToolkit / ReactiveUI)
Viewmodels implement `INotifyPropertyChanged` manually and use small `ICommand` types
(`DesktopCommand`, `RelayCommand`). **Why:** minimal dependencies, full control, easy to reason about
on constrained targets (Raspberry Pi). Trade-off: more boilerplate. Accepted.

### F4 — Centralized transmit safety
All transmit flows are meant to pass a single `ITransmitSafetyAuthority` that owns the global inhibit
(exercise/training), identity, passcode, and per-port checks. **Why:** this is licensed-RF software;
a scattered set of ad-hoc checks would eventually let an unsafe transmit through. (See D1 — the wiring
of this was completed on 2026-07-31.)

### F5 — Safe-by-default developer surfaces
The local REST API ships disabled, localhost-only, read-only, with transmit blocked and returning
501. **Why:** an API that can drive a transmitter must never be open by accident.

---

## 2026-07-31 — Codebase review and hardening pass

A structured review of the whole codebase (parsing/transport, services/transmit-safety, UI/tests,
API/security). Build was clean and ~1196 tests passed at the start. The review found one real
safety defect and several smaller issues; all were fixed in this pass. New tests were added for each
behavioral fix (suite grew to ~1200+).

### D1 — Global transmit inhibit is now enforced at the transport chokepoint *(safety-critical)*
**Problem:** `TransmitSafetyAuthority` existed and exercise mode correctly set its global inhibit, but
`Evaluate()` was consulted in only **one** transmit path (`DigipeaterService`). Beacon, object,
message, iGate, and weather paths sent directly to the transport, so **exercise mode did not actually
block them** — it only blocked the digipeater. `ObjectTransmitService` even injected the authority and
never used it.
**Decision:** Add a minimal `ITransmitInhibitGate` interface in `Aprs.Transport`, implemented by
`TransmitSafetyAuthority`. The composition root hands the gate to the two shared transmit clients —
`AprsIsClient` (all APRS-IS transmit, including iGate via the deferred client) and
`KissRfBeaconTransmitClient` (all RF transmit). Each checks the gate before any bytes leave, so the
global inhibit is enforced in one place that no caller can bypass. `ObjectTransmitService` also now
consults the authority for an early, clear "blocked — exercise mode" message.
**Why this shape:** the authority is request-scoped (needs port + destination), and the transport
layer cannot depend on `Aprs.Services`. A tiny inhibit-only interface owned by the transport layer,
implemented by the Services authority, gives a true chokepoint without inverting the dependency —
and re-applies automatically when the client is rebuilt after a settings change.
**Tests:** `TransmitInhibitGateTests` — gate blocks APRS-IS and RF transmit while inhibited, allows
when not, and the authority is usable as the gate.

### D2 — APRS parser: timestamped position-weather and case-sensitive ack/rej
**Problem (a):** `AprsWeatherParser` hard-coded the weather symbol index at 19 and the position offset
at 1 — correct only for timestampless `!`/`=`. For `@`/`/` reports (7-char timestamp) the symbol is at
26 and the position at 8, so **all weather data in timestamped position-weather packets was silently
dropped.** **Problem (b):** ack/rej were matched case-insensitively, so a message body like
"ACKNOWLEDGED…" was misclassified as an acknowledgement.
**Decision:** Compute the position offset (and thus symbol index) from the type char, and capture the
timestamp for `@`/`/`. Match `ack`/`rej` with `StringComparison.Ordinal` per the APRS spec.
**Tests:** added to `AprsSpec101ConformanceTests`.

### D3 — Companion server requires a per-session token; wildcard CORS removed *(security)*
**Problem:** `MobileCompanionServer` binds to all interfaces and exposes callsign, live positions, and
private messages with no auth and `Access-Control-Allow-Origin: *`. Anyone on the same Wi-Fi could
read it.
**Decision:** Generate a per-session token (128-bit, regenerated on `Start()`), carry it as the first
URL path segment (transparent to the operator since it is in the shown URL/QR), validate it in
constant time, and return a bare 404 without it. Remove the wildcard CORS header (the page is
same-origin). Read-only endpoints only; no transmit exposure.

### D4 — REST API token check fails closed *(security)*
**Problem:** When `RequireToken` was true but no token was configured, the comparison was skipped and
**any** non-empty token authenticated. Comparison was also not constant-time.
**Decision:** Reject every request when a token is required but none is configured (fail closed); use
a constant-time comparison. **Test:** `RequireTokenWithNoConfiguredToken_RejectsEveryRequest`.

### D5 — Transport robustness: reconnect stream leak and AGWPE length bound
**Problem (a):** `AprsIsClient` and `TcpKissClient` reassigned the stream on reconnect without
disposing the old one — a socket/stream leak per reconnect. **Problem (b):** `AgwpeFrameCodec` did not
upper-bound the 32-bit payload length; a length near `int.MaxValue` overflowed `HeaderLength + length`
negative and could spin `DecodeMany`/`FindLastCompleteFrameEnd` forever on crafted input.
**Decision:** Dispose the closed stream before replacing it on reconnect. Bound AGWPE length by the
received buffer size in all three read paths. **Test:**
`AgwpeFrameCodec_HostilePayloadLength_DoesNotHangOrThrow`.
**Known limitation (later resolved in D8):** the `stream`/`State` fields on the transport clients were
still read/written across the receive and send tasks without synchronization. Deferred at the time to
avoid a broad locking change in otherwise-working reconnect logic; addressed in D8.

### D6 — UI-thread blocking, silent catches, and duplicate command classes
**Problem:** Several viewmodel command handlers called `…Async().GetAwaiter().GetResult()`, blocking
the UI thread during network/transmit work; a couple of watchdog/failover coordinators swallowed
exceptions with empty `catch {}`; and three near-identical `ICommand` types existed
(`DesktopCommand`, a file-scoped `RelayCommand`, and `RelayCommand2`).
**Decision:** Add `AsyncDesktopCommand` and convert the blocking handlers to `async Task` awaited
without blocking. Surface unexpected exceptions in the watchdog/failover paths via
`Debug.WriteLine` (narrowing the swallow to expected cancellation) until a logging service exists.
Consolidate to one shared parameterized `RelayCommand` and delete the duplicates.

### D7 — Project now has a CLAUDE.md and this Decisions Log
**Decision:** Added `CLAUDE.md` (primary AI-assistant context, consistent with `AGENTS.md`) and
started this log. **Why:** the project predates the author's practice of keeping these; capturing the
architecture rules and the reasoning behind decisions makes future work (by humans or assistants)
faster and safer, and prevents re-litigating settled choices.

## 2026-07-31 — Follow-up: transport thread-safety and logging

Completing the two items D5/D6 explicitly deferred.

### D8 — Transport clients are now thread-safe (resolves D5's deferred limitation)
**Problem:** The `stream`/`connection` reference and the `State`/`LastError` fields on all three
transport clients (`AprsIsClient`, `TcpKissClient`, `SerialKissClient`) were read by the send path
while the receive loop reassigned them during a reconnect — a send could observe a disposed or
half-swapped stream, and the send even re-read the `stream` field between its write and flush (a torn
read that could split a packet across two streams).
**Decision:** Add a per-client `sync` lock guarding the connection reference and `State`/`LastError`
(via `SetState`/`Fault`/`SetStream`/`Snapshot` helpers). The lock is **never held across an await**:
sends take a consistent `(stream, state)` snapshot under the lock and do all I/O on that snapshot, and
the receive loop snapshots the stream per iteration and disposes exactly the stream it was reading
from on reconnect. `State`/`LastError` getters read under the lock so other threads (coordinators, UI)
never see a torn value. Behavior is otherwise unchanged (all 71 existing transport tests still pass).
**Test:** `AprsIsClientTests.State_And_Send_AreSafeUnderConcurrentStateReads` hammers `State`/
`LastError` from four threads while sends run.
**Residual note:** a send racing a reconnect can still fail (writing to a stream the reconnect is
disposing) — but that now fails cleanly through the existing catch → `Faulted`, rather than via a data
race on the field itself. That is the correct, expected behavior for lock-free I/O.

### D9 — Added a diagnostic logging abstraction (`ILogService`)
**Problem:** Error-surfacing added in D6 used `Debug.WriteLine`, which is invisible in a normal build.
**Decision:** Add `ILogService` (`Aprs.Services/Logging/`) with a default `LogService` — a thread-safe
bounded ring of recent entries plus an `EntryLogged` event (so a future UI log view / export can show
them) that also mirrors to the debugger. It lives in `Aprs.Services` because its consumers
(coordinators in `Aprs.Desktop`) sit above it; the transport layer is intentionally **not** wired to
it (it already exposes faults via `LastError`, and wiring it would invert the layer dependency). The
composition root registers one shared instance and injects it into `ConnectionHealthWatchdog` and
`AprsIsFailoverCoordinator`, replacing their `Debug.WriteLine` calls; `DesktopRuntime.LogService`
exposes it. **Tests:** `LogServiceTests` (records + raises event, ring-buffer bound, thread safety).
**Next step (not done):** surface these logs in the existing Logs/Events UI area, and consider routing
more services through `ILogService` over time.

---

## 2026-08-01 — MIC-E radio identification (device-ID)

### D10 — MIC-E mobiles are identified from the comment, gated on a real MIC-E flag
**Problem:** Device identification (the "Device: …" line) matched only the destination **tocall**. MIC-E
packets — among the most common RF formats, and precisely the mobile radios worth naming — encode their
position *in the destination field*, so they never match a tocall and always showed nothing. The APRS
Foundation database's `mice`/`micelegacy` sections identify these radios instead from a marker carried
in the **comment**.
**Decision:** Extend `DeviceIdentificationService` to parse both MIC-E sections and add
`IdentifyMicE(comment)` plus a combined `Identify(dest, comment)`. Matching, per the database's two
styles: modern radios end the comment with an unusual two-character code (`mice`, e.g. `_"` = Yaesu
FTM-350); legacy Kenwoods start it with a prefix char and optionally end with a suffix char
(`micelegacy`, e.g. `]` = TM-D700, `]=` = TM-D710). Order is **modern trailing code → legacy
prefix+suffix → bare prefix**, and the decoder's `[status] ` comment prefix is stripped first so the
legacy prefix is exposed.
**Correctness gate:** because a legacy match keys on a single leading char (`]`/`>`), an ordinary
station whose comment happened to start that way could be mislabelled a Kenwood — and mis-identifying a
radio is worse than showing nothing. So the comment is consulted **only for genuine MIC-E packets**:
`StationSnapshot.IsMicE` is set from the MIC-E data-type indicator (`0x60`/`0x27`/`0x1C`/`0x1D`) in
`StationDatabase`, persisted across a station's later non-MIC-E packets, threaded through
`StationMarker.IsMicE`, and gated on in `StationMarkerViewModel` (tocall-only unless `IsMicE`).
**Tests:** MIC-E matching + status-prefix stripping + tocall-vs-comment precedence in
`DeviceIdentificationServiceTests`; the non-MIC-E false-positive guard in
`StationDeviceIdentificationTests`; and the full raw-packet → parser → database → marker → viewmodel
spine in `MicEDeviceEndToEndTests`. Full suite green (1275).
**Next step (not done):** device-ID slice 3 — weekly refresh + manual "update now", consolidating the
marker VM's shared default into a single DI singleton the refresher updates.

### D11 — Status reports (`>`) are decomposed into structured fields
**Problem:** Status reports were stored as one raw text blob. Per APRS Protocol Reference §16 the body
can carry a leading DHM-zulu **timestamp** *or* a **Maidenhead grid locator + symbol**, and a trailing
**beam-heading + ERP** (`^`) extension — none of which we surfaced, and the timestamp/beam codes leaked
into the display text.
**Decision:** New `AprsStatusReport.Parse` (Aprs.Core) decomposes the body, validated against Dire
Wolf's decoder (exact rules fetched from `decode_aprs.c`): timestamp = 6 digits + `z`; Maidenhead = a
4- or 6-char grid then a symbol table id + code, requiring end-of-string or a space before any comment
(so ordinary text starting with grid-like chars isn't misread); beam heading `0`–`9` → 0–90°, `A`–`Z` →
100–350°; ERP `(c-'0')²·10` W for `1`–`K`; the `^` extension is only honoured when **both** chars
decode. `StatusAprsPacket` gained `Timestamp`, `MaidenheadLocator`, `SymbolTableIdentifier`,
`SymbolCode`, `BeamHeadingDegrees`, `EffectiveRadiatedPowerWatts` (all optional). `RawStatusText` stays
verbatim; `StatusText` is now the cleaned display message (a plain status is unchanged, so existing
consumers/tests are unaffected). **Tests:** `AprsStatusReportTests` (each form + combinations + the
grid-like-but-not-a-locator guard + the full-parser path). Full suite green (1292). Closes the last
⚠️-partial receive-side item in the conformance plan.

### D12 — Object emit timestamp fixed; object + message round-trips added *(conformance)*
**Problem:** Phase 2 round-trip testing (generate → parse → assert equal) surfaced a generate-side bug:
`AprsObjectEditorService.BuildObjectPacket` emitted the timestamp as `HHmmss` + a `z` suffix. But per
APRS §11 the `z` suffix means **DDHHMM** (day/hour/minute), so time-of-day data was mis-framed — any
object beaconed at minute ≥ 24 produced an invalid "hour" field (e.g. 14:25:30 → `142530z`, read as day
14, **hour 25**). The parser stores the 7-char timestamp verbatim without semantic validation, so a raw
string round-trip hid it — only asserting a valid in-range hour/minute caught it.
**Decision:** Emit DHM-zulu in UTC (`now.UtcDateTime.ToString("ddHHmm") + "z"`) — the widely-parsed
form, and now internally consistent with the `z` suffix. Added round-trips: object live + killed
(`AprsRoundTripTests`, asserting the kill indicator is `_` on the wire and the timestamp hour/minute are
in range) and message (`AprsMessageRetryEngineTests`, addressee/body/id survive). **Tests:** +3, full
suite green (1295). No existing test locked the old format (the `092345z`/`111111z` in parser tests are
valid DHM *inputs*, not emitter output).

### D13 — Network-behavior conformance: digipeater dupe/loop + iGate mandatory gating rules *(safety-critical)*
**Digipeater (`DigipeaterService`):** the New-N WIDEn-N mechanics (callsign insertion + decrement) were
verified correct against the algorithm, but two gaps were found and fixed:
- **Duplicate fingerprint included the path.** The same original packet reaches a digi via multiple
  neighbours (and echoes back with our own callsign inserted) — each with a *different* path — so a
  path-inclusive fingerprint failed to recognise them as one packet. Now source + destination + payload
  (the standard APRS dupe basis).
- **No used-flag loop guard.** A digi that heard its own retransmission would process the still-unused
  trailing WIDE and repeat the packet again. Added `AlreadyRepeatedByUs`: never repeat a packet already
  carrying our callsign as a used hop.
- Also trap malformed `WIDEn-N` where N>total (a QRM pattern that can't occur in valid traffic).

**iGate (`IGateService`):** RF→IS loop-prevention/routing directives were only *default, user-editable*
`BlockedPathPatterns` (`TCPIP*`/`TCPXX*`/`q*`) plus a monitor candidate-state check gated behind the
duplicate-suppression toggle — and **`NOGATE`/`RFONLY` were not enforced at all**. Added
`MandatoryNoGateReason`, checked unconditionally: never gate `NOGATE`/`RFONLY` (the sender's explicit
"keep off the Internet" directive) or `TCPIP`/`TCPXX`/a q-construct (already traversed APRS-IS → gating
back loops). `IGateMonitorService` advisory made consistent (NOGATE/RFONLY shown as rejected, not a
candidate). **Tests:** `DigipeaterServiceTests` (same-packet-different-path dupe, self-echo loop, N>total
trap), `IGateServiceTests` (mandatory-no-gate theory, config-independent). Full suite green (1301).

### D14 — Device-ID database auto-refresh (slice 3 engine)
**Problem:** The device-ID database was a bundled, build-frozen snapshot. New radios/software get tocalls
assigned continually, so a way to refresh it (without a new app release) was the last device-ID slice.
**Decision:** A hot-swappable `RefreshableDeviceIdentificationService` wraps the immutable
`DeviceIdentificationService` and swaps its inner instance atomically (a single volatile reference
assignment, so concurrent lookups never see a half-built DB). `DeviceIdDatabaseUpdateService` orchestrates
a **weekly-gated** refresh: download (`HttpDeviceIdDatabaseDownloader`) → validate (a candidate that
parses to zero patterns is rejected) → swap → cache (`FileDeviceIdDatabaseStore`, under
`{config}/device-id/`). Every failure mode is **non-fatal** — offline, HTTP error, or corrupt payload all
keep the last good (or bundled) database, because identification is a nicety, not a critical path. The
marker VM's per-class lazy default was consolidated into one app-wide `DeviceIdentificationProvider.Current`
that the composition root points at the single refreshable instance (`DesktopRuntime`), which also loads
the cached DB and fires a background refresh at startup. **Tests:** `DeviceIdDatabaseUpdateServiceTests`
(hot-swap, skip-when-fresh, refresh-when-stale, force, download/validation failure keeps current, cached
load tolerates corruption). Full suite green (1311). **Deferred to the UI polish pass:** the visible
"updated <date> · Update now" status/button and an in-session periodic re-check (the engine and the
runtime hook are in place; only the view surface remains).

---

### D15 — Complete APRS symbol tables adopted from the authoritative aprs.fi index *(conformance)*
**Problem:** The symbol lookup service (`AprsSymbolLookupService`) shipped a hand-curated shortlist of
~61 symbols, so the object symbol picker was missing most of the spec's symbols and a few hand-written
descriptions were wrong (e.g. `/C` was labelled "Coast Guard" — it is **Canoe**; `\C` is Coast Guard).
**Decision:** Replace the shortlist with the **complete** APRS primary (`/`) and alternate (`\`) tables —
**159 defined symbols** (reserved/undefined code positions omitted) — with descriptions taken verbatim
from the authoritative aprs.fi symbol index (`hessu/aprs-symbol-index`, CC BY-SA 4.0), which is also the
source of the bundled icon sheets. `Category`, the marker-dot key, and the short letter designation are
**derived** from the description so the table stays a single source of truth. The object symbol picker
now offers **both** tables (primary listed first), scrolls, and shows each symbol's real icon (cropped
from the embedded sheets via `AprsSymbolIconConverter`) beside its letter. Attribution added to
`APRS-SYMBOLS-NOTICE.txt`. **Tests:** `AprsSymbolLookupServiceTests` updated to the authoritative
descriptions and locks the full set (86 primary + 73 alternate = 159); `MapViewModelTests` /
`StationListViewModelTests` description assertions updated to match.

---

## 2026-08-07 — FieldCommand IMS integration

### D16 — FieldCommand tactical-map station feed rides the Mobile Companion server (tokenless LAN mode)

FieldCommand IMS (the sibling Raspberry-Pi incident-management platform) drives a big-screen **tactical
APRS map** that needs live stations off the radio. In the new radio chain the RF TNC is **Direwolf**
(KISS/AGW, no HTTP), so the "serve stations to the map" job moves to APRS Command. The map is a plain
browser `fetch` of `http://<host>:<port>/api/stations` with no auth header.

**Decision:** serve that feed from the existing **`MobileCompanionServer`** in a new **LAN feed mode** —
`Start(port, requireToken: false)` binds a **fixed port (8080)** with **no per-session token** and adds a
**wildcard CORS** header. Exposed via `DesktopRuntime.StartFieldCommandFeed()` and a **View → "FieldCommand
Tactical-Map Feed (LAN)"** menu item. The phone-companion token mode is unchanged.

**Why the companion server and not the Local REST API:** `AprsCommand.Api.LocalRestApiService` is the
*intended* long-term integration surface (richer contract, read-only, rate-limited, per-endpoint
permissions) — but it is **not network-live**: `StartAsync()` only sets a state flag and nothing binds a
socket or calls `HandleAsync`. The companion server is a real, working `HttpListener` today, so it is the
pragmatic home for now.

**Why tokenless is acceptable here:** the feed is used only on the isolated, trusted **EMCOMM-NET** — the
same no-login posture FieldCommand's own services use on that network. Do **not** enable tokenless mode on
an untrusted network. Read-only: the LAN feed only serves the existing GET endpoints.

**Planned follow-up (v2 backlog §13):** build the HTTP transport for `LocalRestApiService` (+ the parallel
`WebSocketEventStreamService`), then move the FieldCommand feed onto it — richer contract and live push.
The FieldCommand side is a trivial host:port change.

---

## Future / planned (not yet done)

Decisions made about work intended for later, so the reasoning is captured before it is scheduled.

### P1 — Upgrade Avalonia 11.3.7 → Avalonia 12
**Decision:** Plan a deliberate upgrade to Avalonia 12, done on its own branch and merged only after a
manual UI pass — **not** urgent, and not bundled with feature work.
**Why it is now feasible:** the blocking dependency was Mapsui (the live map is a core feature).
Mapsui ships a dedicated `Mapsui.Avalonia12` package (identical XAML/C#, only the package name
differs), so the map is no longer a blocker. Avalonia 12 is GA (12.1.x as of mid-2026) with a
published breaking-changes list. No other dependency is Avalonia-coupled (BruTile, SQLite,
System.IO.Ports, Velopack, MS.DI are all independent).
**Why do it at all:** the sister project **Activation Planner is already on Avalonia 12** — aligning
both projects on the same major reduces context-switching and lets patterns transfer between them, and
keeps us on the current supported line.
**Why not yet:** 11.3.x is stable and supported; 12 has breaking changes to work through; nothing is
currently blocked on 12. Schedule it deliberately rather than rushing.
**Scope when done:** bump Avalonia packages 11.3.7 → 12.x; swap `Mapsui.Avalonia` →
`Mapsui.Avalonia12`; work through the Avalonia-12 breaking-changes list until it builds clean; run the
full test suite (note: the ~1208 tests cover Core/Services/Transport, **not** the Avalonia views, so a
manual UI smoke test is required); verify on Raspberry Pi ARM64; update the Avalonia version in
`CLAUDE.md`.

### P2 — Candidate improvements identified during the 2026-07-31 review (not yet scheduled)
Captured so they are not lost; ranked by value. None is a defect — the critical tier was already
fixed (D1–D9).
- **MIC-E decoding** (functional gap, high impact): the parser handles only `! = / @` position
  formats and treats MIC-E as Unknown, yet MIC-E is one of the most common RF formats (mobile
  trackers, Kenwood/Yaesu radios). Decoding it would parse a large slice of currently-Unknown traffic.
- **Receive-loop allocation churn** (efficiency, most visible on Raspberry Pi): the KISS receive loops
  (`pending.AddRange(readBuffer.Take(...))`, `List<byte>.RemoveRange(0,n)`) and `AgwpeFrameCodec`
  (`Skip/Take/ToArray`) allocate on every read; move to spans / a sliding buffer / `ArrayPool<byte>`.
- **Shared transport base class**: `AprsIsClient`/`TcpKissClient`/`SerialKissClient` are ~90% identical
  after the thread-safety pass; extracting a base fixes-once the plumbing that previously diverged and
  caused per-client bugs.
- **Decompose `MapView.axaml.cs`** (~1451-line code-behind doing tile/radar/file-IO/drawing logic that
  belongs in services/VM).
- **Fire-and-forget connects** in `BeaconService`/`LiveDataCoordinator` pass `CancellationToken.None`
  and drop faults; thread real tokens and log via `ILogService`.
- **Shared `HttpClient`** for the weather clients (currently one `new HttpClient()` each, some
  undisposed).
- **Surface `ILogService` in the Logs UI** (the D9 next-step).
- **Enable analyzers** (`EnableNETAnalyzers` + `AnalysisMode=Recommended` in `Directory.Build.props`;
  consider `TreatWarningsAsErrors` after triaging).
- Minor parser completeness: course/speed edge cases, preserving the AX.25 digipeater via-path,
  compressed-weather.

### P3 — Match the APRS specification exactly (plan started)
**Decision:** Pursue exact conformance to the current APRS spec (APRS 1.2 / `wb2osz/aprsspec`), tracked
in `docs/architecture/APRS_SPEC_CONFORMANCE_PLAN.md`. Canonical references (aprsspec, Dire Wolf, how.aprs.works,
aprs-deviceid) are now recorded in `CLAUDE.md`; build all parser/encoder work against them, not the
obsolete APRS101.pdf.
**Phase 0 audit — key findings (see the plan for the full matrix):** the parser is more complete than
first thought (position both formats, weather, message, object/item, telemetry PARM/UNIT/EQNS/BITS,
status, capability, query all dispatched). Real gaps: **MIC-E** (missing → Unknown), **third-party `}`**
(encapsulated packets not unwrapped — matters on APRS-IS), and depth items (query/status decomposition,
DAO, verify compressed/telemetry variants). **One concrete discrepancy to resolve:** the object
kill-indicator — code treats `_` as killed (`AprsObjectItemParser.cs:31`), our primer §7.3 says `/`;
reconcile against aprsspec §11 (probably accept both). Generate-side: verify the **area-object
`√(offset/1500)`** encoding matches the aprs11 errata.
**Sequencing:** Phase 0 vector-verification → MIC-E → third-party `}` → area-object generate check.

### P4 — RepeaterBook: feature removed from UI; deferred to v2.0 pending written permission
**Status (2026-08-01):** the Field Repeater Lookup feature (`RepeaterBookService`) needs an app-level
approval from RepeaterBook (distributed-app category); the token was **not approved**, and a reply to
RepeaterBook is awaiting a response. The feature degrades cleanly without a token. **Do not** treat it
as shippable or assume the API works until approval lands. Captured here so the status lives with the
project rather than only in external chat history.

**Update (2026-08-01):** removed **Field Repeater Lookup** from the Map menu until this is settled (the
`RepeaterBookService`/`RepeaterDirectoryWindow`/viewmodel stay in the code, dormant — no menu entry).

**Considered and deferred — a "download-your-own-export, show-as-a-map-layer" design.** Idea: the
operator downloads their **own** RepeaterBook member CSV and the app imports it into a **toggleable,
offline, never-transmitted** map layer (sidesteps the API-token approval entirely). **But** RepeaterBook's
published API/data terms appear to restrict exactly this: the prohibited-use list names **"public
directories, maps, nearby-finder tools"** and "alternative … repeater finder experiences," and
**"offline bundling / redistribution / mirroring"** of their data requires **written permission**; public
access "does not grant … export rights." Attribution ("Data courtesy of RepeaterBook.com") is required
where permitted. **Decision:** move the repeater map layer to a **v2.0** item, gated on **explicit written
permission from RepeaterBook** for the member-import / personal-offline-display use case. Do **not** build
it before that permission is in hand. (Reading of their terms, not legal advice — their written OK is the
gate.) A permission-request email draft is to be prepared when Jim is ready to send it from his account.

### P5 — Winlink RMS gateways: feature removed from UI; deferred to v2.0 pending the API key
**Status (2026-08-01):** the Winlink RMS Gateways feature (`WinlinkRmsGatewayService`) queries
`api.winlink.org/gateway/query`, which requires a **per-application API key issued by a Winlink
administrator** (not self-service). Jim has requested one and is **awaiting a response**. Removed the
**Winlink RMS Gateways** entry from the Messages menu until then (service/window/viewmodel stay in the
code, dormant — no menu entry).

**Planned design (identical to the deferred repeater layer):** plot each RMS gateway's location as a
**toggleable, never-transmitted** map overlay — a pure Mapsui layer with no path to any transmit client.

**Key difference from RepeaterBook (P4):** Winlink's authorization mechanism **is** the per-app key
issued by an admin, so the key itself is the permission — once granted for APRS Command, that authorizes
querying and on-map display; no separate terms carve-out is needed the way RepeaterBook required. When the
key lands, verify the live request/response shape against `ParseResponse()` (never tested against a real
key) and confirm Winlink's data-use/attribution expectations before shipping. **Do not** assume the API
works until the key is in hand.

### D17 — APRS-Command is VHF-only (1200-baud); HF APRS is not, and never was, in this app
**Recorded 2026-08-15 (retroactive — this was never a stated decision, only an implicit one).** APRS-Command
supports **VHF APRS only (1200-baud AFSK; 9600 G3RUH planned)**. It has **no HF (300-baud) support** and no
built-in modem — all RF modulation is delegated to **Direwolf**, and the generated config hardcodes
`MODEM 1200` (`DirewolfProcessManager.cs`, `WriteConfigFile()`), with no baud/mode selector exposed anywhere.

**Why VHF-only:** there is **no documented rationale** — HF APRS was simply never built. It is a *byproduct*
of the Direwolf-1200 dependency, not a deliberate "we won't do HF" choice. (The app *does* list the HF APRS
frequencies — 30m 10.151, 20m 14.105, USB/300 baud — in its frequency reference for the operator to dial by
hand, but there is no modem behind them.) Captured here so future work doesn't have to re-derive it.

**Division of labor with IcomRigControl (KE4CON's Icom control/logging app):** HF APRS lives in
**IcomRigControl**, which has its own self-contained 300-baud AFSK/AX.25 engine (no Direwolf). APRS-Command
owns **VHF** APRS. The two are deliberately separate programs; they divide APRS by **band**, not by handoff.
If HF ever becomes wanted in APRS-Command, it would need a real 300-baud modem path (Direwolf HF tone config
or an internal modem) — a genuine new feature, not a config tweak.
