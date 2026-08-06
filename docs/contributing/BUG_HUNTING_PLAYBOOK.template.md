# Bug-Hunting Playbook — TEMPLATE

**Project-agnostic template.** Copy this into any project as `docs/contributing/BUG_HUNTING_PLAYBOOK.md`, then fill
the four `<FILL IN>` spots (pillar table, project hooks, latent-bug backlog, tracker). Everything else
is universal and should stay as-is. Add a pointer to it from that project's `CLAUDE.md` (Testing
section). The APRS Command instance is the reference example.

**Read this at the start of a debugging / hardening session and follow it.** The goal is to drive the
bug count toward zero *systematically*, not reactively. Correctness of your core domain logic is only
**one pillar** — most bugs live in UI, concurrency, integrations, and feature logic.

---

## The one rule that makes everything stick

> **Every bug found — from any method below — gets a regression test before you move on.**

A fix without a test can silently regress. A fix *with* a test stays dead. No exceptions. When a test
you wrote to reproduce a bug *passes* unexpectedly, the bug is elsewhere — keep the test and keep looking.

---

## The mental model: pillars, not a single dial

Bugs cluster by subsystem. Hardening one pillar does not harden the others.

<FILL IN: a pillar table for THIS project. Rows are your subsystems; last column is the primary
method. Generic starting set — edit to fit:>

| Pillar | What it covers | Primary method |
|---|---|---|
| Core domain logic | your parser/engine/algorithm correctness | oracle diff, fuzz, round-trip |
| Feature logic | the app's features and rules | unit + scenario tests |
| UI / rendering | views, layout, windows | exploratory use + headless VM tests |
| Concurrency | threads, shared state, async | stress tests + review |
| Integrations / I/O | network, serial, external tools, APIs | real-endpoint + fault injection |
| Persistence | database / settings / files | corruption + round-trip tests |
| Safety / critical paths | whatever must never fail | exhaustive "must never fail" tests |

---

## The method pipeline (ordered by bug-yield per unit of effort)

Work top-down. Tier 1 is cheap and runs forever after; don't skip it to chase a manual bug.

### Tier 1 — Cheap, automated, catches whole *classes*
1. **Static analysis.** Turn on the platform's analyzers (for .NET: `EnableNETAnalyzers` +
   `AnalysisMode=Recommended` in `Directory.Build.props`); triage; move toward warnings-as-errors.
   Catches null-derefs, undisposed resources, bad async across the whole codebase for free.
2. **Work the known-latent-bug backlog.** If you've already written down suspect code, just fix it.
3. **Coverage → attack the gaps.** The *uncovered* lines (error handling, reconnect, failover) are
   where bugs hide. Add tests there.

### Tier 2 — Prove correctness with an oracle / randomness
4. **Oracle diff.** Run a large real-input corpus through *both* your code and a trusted reference
   (`<FILL IN: the reference oracle for this project>`); diff every field/output. Turns "I think it's
   correct" into "N discrepancies / M cases."
5. **Fuzzing.** Throw millions of malformed/random inputs at every parser/entry point; assert **no
   crash, no hang**.
6. **Round-trips.** For anything you *emit/produce*: produce → parse → assert equal (and, if there's an
   oracle, produce → oracle → compare).

### Tier 3 — Hard-to-trigger, high-consequence
7. **Critical-path tests.** Exhaustively prove whatever must never fail in this app.
8. **Concurrency stress.** Hammer shared paths from multiple threads; hunt unsynchronized state.
9. **Soak test.** Run for hours/days under load; watch memory, handles, CPU.

### Tier 4 — Human + real world
10. **Systematic click-through** (not random poking): exercise every screen/feature once, on a checklist.
11. **Beta testers / field use.** Diverse hardware, real deployments; convert every finding to a test.

---

## Per-session workflow

1. **Pick a lane** — one pillar or one tier per session. State it at the top.
2. **Baseline green** — run the full suite; confirm it passes before changing anything.
3. **Hunt.** On finding a bug: failing test (red) → smallest fix (green) → full suite still green →
   commit on a branch naming the bug + root cause.
4. **Log residuals** you won't fix now (tracked, with detail; pin current behavior with a test).
5. **Update status docs** so they stay truthful.

---

## Progress tracker & session log — DO NOT re-run the whole file each session

Consult this first, skip what's done, pick **one lane**. Items are of three kinds: **one-time
foundations** (do once, then automatic), **standing/automatic** (every build), **continuous lanes**
(never done; one per session).

### One-time foundations (check off as completed)
- [ ] Reference oracle installed/available — `<FILL IN>`
- [ ] Static analysis enabled and triaged
- [ ] `<FILL IN: this project's latent-bug backlog>` worked
- [ ] Oracle corpus diff run (large real-input diff)
- [ ] Generate/produce-side round-trip pass complete
- [ ] Safety / critical-path exhaustive pass
- [ ] Concurrency stress pass
- [ ] Soak test run

### Standing / automatic (keep green — not a session task)
Full test suite on every change · fuzz harness · analyzers (once on) · coverage watched.

### Session log (append ONE line per session; newest last)
Format: `YYYY-MM-DD — lane: <pillar/method> — found/fixed: <summary> — branch/sha`
- _(none yet)_

---

## Project hooks (`<FILL IN>` for this project)

- **Reference oracle:** `<FILL IN — e.g. a spec, a reference implementation, real hardware/tool output>`
- **Run tests:** `<FILL IN test command>`
- **Status / conformance docs:** `<FILL IN>`
- **Known latent issues:** `<FILL IN — decisions log / backlog>`
- **Validation checklist:** `<FILL IN>`
- **Environment gotchas:** `<FILL IN — build/run locks, network/SSL quirks, etc.>`

---

## What "done" honestly means

There is no "all bugs fixed." The realistic target: **every pillar has an active method pointed at it,
every fix has a test, and known gaps are tracked, not hidden.**
