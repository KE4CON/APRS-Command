# Bug-Hunting Playbook

**Read this at the start of a debugging / hardening session and follow it.** The goal is to drive
the bug count toward zero *systematically*, not just fix whatever was last noticed. Spec compliance
of the parser is only **one pillar** — most bugs live elsewhere (UI, concurrency, integrations,
feature logic, the generate side). This playbook covers all of them.

---

## The one rule that makes everything stick

> **Every bug found — from any method below — gets a regression test before you move on.**

A fix without a test can silently come back. A fix *with* a test stays dead. This single discipline
is what makes the bug count trend down instead of oscillating. No exceptions.

Corollary: when a test you write to reproduce a bug **passes** unexpectedly, the bug is elsewhere —
keep the test (it documents intent) and keep looking.

---

## The mental model: pillars, not a single dial

Bugs cluster by subsystem. "Fully spec compliant" hardens the first pillar only.

| Pillar | What it covers | Primary method |
|---|---|---|
| Parser / encoder | APRS decode + emit correctness | oracle diff (Dire Wolf), fuzz, round-trip |
| Feature logic | digipeater/iGate rules, replay, messaging, beaconing, GPS | unit tests + scenario tests |
| UI / rendering | Avalonia views, layout, windows, map | exploratory use + headless VM tests |
| Concurrency | transports, ingestion, coordinators, shared state | stress tests + review |
| Integrations / transports | APRS-IS, KISS/serial/AGWPE, RepeaterBook, Winlink, CalTopo, NWS | real-endpoint + fault injection |
| Persistence | SQLite station DB, settings | corruption/round-trip tests |
| Safety-critical (EmComm) | transmit-safety authority, ACK/retry, failover | exhaustive "must never fail" tests |

---

## The method pipeline (ordered by bug-yield per unit of effort)

Work top-down. Tier 1 is cheap and runs forever after; don't skip it to chase a shiny manual bug.

### Tier 1 — Cheap, automated, catches whole *classes*
1. **Static analysis.** Ensure analyzers are on (`EnableNETAnalyzers` + `AnalysisMode=Recommended` in
   `Directory.Build.props`); triage warnings; move toward `TreatWarningsAsErrors`. Catches
   null-derefs, undisposed resources, bad async across the whole codebase for free.
2. **Work the known-latent-bug backlog.** The Decisions Log **P2** already names real ones
   (fire-and-forget connects passing `CancellationToken.None` and dropping faults; undisposed
   `HttpClient`s; the oversized `MapView.axaml.cs`). Pre-identified = just fix them.
3. **Coverage → attack the gaps.** Run coverage over the suite; the *uncovered* lines (error
   handling, reconnect, failover) are where bugs hide. Add tests there.

### Tier 2 — Prove correctness with an oracle / randomness
4. **Dire Wolf corpus diff (parser).** Run a large corpus of real APRS-IS packets through *both* our
   parser and `decode_aprs`, diff every field. Turns "I think it's compliant" into "N discrepancies
   / M packets." Oracle is installed — see *Project hooks* below.
5. **Fuzzing.** Expand the existing fuzz harness: throw millions of malformed/random packets at every
   parser; assert **no crash, no hang**. Finds inputs no human would type.
6. **Generate-side round-trips.** For every emitted packet type: generate → parse → assert equal, and
   generate → `decode_aprs` → compare. Verifies the transmit side (only partly done).

### Tier 3 — Hard-to-trigger, high-consequence
7. **Safety-critical path tests (EmComm).** Exhaustively prove the transmit-safety authority: nothing
   transmits during replay/simulation/training/receive-only, ever. Prove message ACK/retry/delivery
   and failover. These must never be discovered broken during a real deployment.
8. **Concurrency stress.** Hammer ingestion + transports from multiple threads; hunt shared mutable
   state without synchronization. Races won't appear in normal use, then bite in the field.
9. **Soak test.** Run for hours/days on live traffic; watch memory, handles, CPU. Catches leaks and
   slow degradation.

### Tier 4 — Human + real world
10. **Systematic click-through** (not random poking): open *every* window, exercise *every* feature
    once, against `docs/release/FINAL_RELEASE_VALIDATION_CHECKLIST.md`.
11. **Beta testers / field use.** Diverse hardware, real deployments — finds what one user's habits
    never will. Capture findings as issues, then convert each to a test (the one rule).

---

## Per-session workflow

1. **Pick a lane.** One pillar or one tier per session — don't scatter. State which at the top.
2. **Baseline green.** Run the full suite; confirm it passes before changing anything.
3. **Hunt** using the chosen method. When you find a bug:
   - Write a failing test that reproduces it (red).
   - Fix the smallest thing that makes it green.
   - Confirm the full suite is still green (no regressions).
   - Commit on a branch with a message that names the bug and the root cause.
4. **Log findings you won't fix now** as tracked residuals (in the relevant plan doc or an issue),
   with enough detail to act later — never a silent gap. Pin current behavior with a test so a change
   is noticed.
5. **Update the relevant plan/doc** (e.g. `APRS_SPEC_CONFORMANCE_PLAN.md`) so status stays truthful.

---

## Project hooks (this repo)

- **Dire Wolf oracle:** `C:\Dev\direwolf\direwolf-1.8.1-a231971_x86_64\decode_aprs.exe` (installed,
  outside the repo). Feed it TNC2-format lines on stdin; it prints the decoded interpretation. Use as
  the reference decoder for parser diffs.
- **Test suite:** `dotnet test tests/Aprs.Tests/Aprs.Tests.csproj`. Keep it green; it's the safety net.
- **Conformance status & residuals:** `docs/architecture/APRS_SPEC_CONFORMANCE_PLAN.md`.
- **Known latent issues to work:** Decisions Log **P2**.
- **Release/manual validation checklist:** `docs/release/FINAL_RELEASE_VALIDATION_CHECKLIST.md`.
- **On Windows:** the app locks its DLLs while running — close it before `dotnet build`/`test` of the
  desktop project. Shell `git`/`curl` hit SSL cert errors here; use `gh` for network reads.

---

## Progress tracker & session log — DO NOT re-run the whole file each session

This playbook is a *method*, not a script. **Do not** run it top-to-bottom every time. Consult this
tracker first, skip what's already done, then pick **one lane** for the session. Items are of three
kinds:

- **One-time foundations** — do once; they then run forever. Check them off below.
- **Standing / automatic** — run on every build/CI once set up; not per-session work.
- **Continuous lanes** — never "done"; pick one per session and work it.

### One-time foundations (check off as completed)
- [x] Dire Wolf oracle installed — `C:\Dev\direwolf\…\decode_aprs.exe` *(2026-08)*
- [x] Parser type-table conformance + tricky forms verified vs Dire Wolf *(Phase 5, 2026-08)*
- [ ] Static analysis enabled (`EnableNETAnalyzers` + `AnalysisMode=Recommended`) and triaged
- [ ] Decisions-Log **P2** latent-bug backlog worked (fire-and-forget faults, `HttpClient`, MapView split)
- [ ] Dire Wolf **corpus diff** harness built (large real-traffic field-by-field diff)
- [ ] Generate-side round-trip pass complete (every emitted packet type)
- [ ] Safety-critical exhaustive pass (transmit-safety authority, ACK/retry, failover)
- [ ] Concurrency stress pass over ingestion + transports
- [ ] Soak test run (hours/days on live traffic; memory/handles/CPU watched)

### Standing / automatic (keep green — not a session task)
Full test suite on every change · fuzz harness · analyzers (once on) · coverage watched.

### Session log (append ONE line per session; newest last)
Format: `YYYY-MM-DD — lane: <pillar/method> — found/fixed: <summary> — branch/sha`
- _(none yet — first hardening session appends here)_

**So the per-session answer:** read the tracker → the checked foundations are done, skip them → the
standing items are already running → choose one unchecked foundation *or* one continuous lane → work
it via the per-session workflow above → check the box (if a one-time item) and append a session-log
line. The tests you leave behind are the durable record; this tracker is the index of *what class of
work* has been swept.

---

## What "done" honestly means

There is no "all bugs fixed." The realistic target is: **every pillar has an active method pointed at
it, every fix has a test, and known gaps are tracked, not hidden.** When that's true, the app is as
trustworthy as it can be — and for an EmComm tool, *trustworthy* is the whole point.
