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
