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
in `docs/APRS_SPEC_CONFORMANCE_PLAN.md`. Canonical references (aprsspec, Dire Wolf, how.aprs.works,
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

### P4 — RepeaterBook API approval is outstanding (awareness)
**Status (2026-08-01):** the Field Repeater Lookup feature (`RepeaterBookService`) needs an app-level
approval from RepeaterBook (distributed-app category); the token was **not approved**, and a reply to
RepeaterBook is awaiting a response. The feature degrades cleanly without a token. **Do not** treat it
as shippable or assume the API works until approval lands. Captured here so the status lives with the
project rather than only in external chat history.
