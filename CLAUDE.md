# APRS Command — Project Context

> This file is the primary context for AI coding assistants (Claude Code and others).
> A companion `AGENTS.md` holds the original Codex-format instructions; the two are kept
> consistent. When they disagree, this file wins. Full design history lives in
> `Activation_Planner`-style docs under `docs/` and in `APRS-Command_Decisions_Log.md`.

## Project Identity
Name: APRS Command
Author: James Rospopo, KE4CON
License: **GPL v3** (chosen deliberately so the tool can never be taken closed-source or lost the way UI-View32 was — see README).
Language: C# (.NET 10, SDK pinned via `global.json` to 10.0.203, `RollForward: Major`)
UI Framework: **Avalonia 11.3.7** (cross-platform — macOS, Windows, Linux x64, Raspberry Pi ARM64)
Status: Alpha (v0.3.0) — functional for daily use, active development.
Purpose: A cross-platform APRS client built on Bob Bruninga WB4APR's original vision of APRS as a **situational-awareness tool** for emergency communications, public service, and any operation needing a common operating picture — not merely vehicle tracking. Live map, APRS-IS + RF (KISS/AGWPE), messaging, objects, weather, iGate, digipeater, alerts, GPS, replay/simulation/training.

## Related Programs — Do Not Merge
- **Activation Planner** — separate pre-operation planning tool (bands/antennas/checklists via VOACAP). Shares the author and the general tech stack/architecture pattern, **no shared code**.
- **IcomRigControl**, **FieldCommand IMS** — separate programs. No integration.

## Architecture Layers (compiler-enforced by `ProjectReference`; never mix concerns)
Dependencies flow downward only. Each layer sees only the layers below it.

- **`Aprs.Core`** — APRS packet types and the parser. **Pure protocol logic only.** No serial, TCP, file-system, or UI dependencies. Immutable records for parsed packets.
- **`Aprs.Transport`** — APRS-IS client, serial KISS, TCP KISS, AGWPE frame codec. Transport-specific I/O only. Owns `ITransmitInhibitGate` (see Transmit Safety).
- **`Aprs.Services`** — business logic: station database, beacon scheduler, iGate, digipeater, alert rules, GPS, weather, message ACK/retry, objects, replay/simulation, transmit-safety authority. Consumes Core + Transport via interfaces.
- **`Aprs.Mapping`** — map symbols, tile providers/cache, Mapsui integration.
- **`Aprs.Desktop`** — Avalonia UI: views, viewmodels, runtime coordinators, and the composition root (`Composition/DesktopRuntime.cs`). Consumes Services/Mapping.
- **`AprsCommand.Api`** — optional local REST API + WebSocket event stream (developer/extension surface).
- **`AprsCommand.Contracts`** — shared public DTOs. Keep internal domain models separate from these; map explicitly at the boundary.
- **`tests/Aprs.Tests`** — ~1200 xUnit tests. **`tests/Aprs.FuzzHarness`** — parser fuzzing.

## Coding Standards
- C# latest, `Nullable` reference types **enabled** in every project.
- `async`/`await` for all I/O (sockets, serial, HTTP). **Never block the UI thread** — no `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` in viewmodels; use `AsyncDesktopCommand` for async command handlers.
- Pass `CancellationToken` through long-running loops; catch `OperationCanceledException` at loop/await boundaries only.
- No `Thread.Sleep` — use `Task.Delay(..., cancellationToken)`.
- **Do not swallow exceptions silently.** Surface unexpected exceptions through `ILogService` (`Aprs.Services/Logging/`) — the shared diagnostic log (bounded ring + `EntryLogged` event, mirrored to the debugger) — rather than an empty `catch {}`. Narrow a catch to the expected type (e.g. `OperationCanceledException`) and log the rest. (Not yet surfaced in the UI Logs area — a wanted follow-up.)
- Immutable `record` types for parsed packets and value data.
- **MVVM is hand-rolled** (manual `INotifyPropertyChanged`, `DesktopCommand`/`AsyncDesktopCommand`/`RelayCommand`). This project does **not** use CommunityToolkit.Mvvm or ReactiveUI — do not introduce them without discussion. Use the one shared `RelayCommand` (parameterized) / `DesktopCommand` (parameterless) / `AsyncDesktopCommand` (async) — do not add new per-file command classes.
- Views/viewmodels talk to **services and abstractions**, never directly to transports or the parser.
- Source-tag data models by origin (received / generated / imported / replayed / simulated / transmitted).
- **American English everywhere** — user-facing UI strings, tooltips, all docs and the User Manual, and code comments (color not colour, center not centre, meters not metres, gray not grey, `-ize` endings). Match the app's own labels. (Don't rename existing internal identifiers like the `Centre` property just for spelling.)

## Key Domain Rules

### Transmit safety is centralized — this is safety-critical
This software can key a real transmitter (RF via KISS/AGWPE) and post to the APRS-IS internet network. **Every** transmit path — APRS-IS, RF/TNC, beacon, object, message, weather, iGate, digipeater — must honor the same safety gates.

- **`ITransmitSafetyAuthority`** (`Aprs.Services/TransmitSafetyAuthority.cs`) is the single authority. `Evaluate(TransmitRequest)` applies, in priority order: global inhibit → valid callsign (never N0CALL/placeholder) → APRS-IS passcode (the `-1` sentinel is receive-only) → per-port checks.
- **Exercise / training mode** flips the global inhibit via `Inhibit(...)` / `Release()`. It must **hard-block all transmit**.
- The global inhibit is enforced at the **transport chokepoint** so no path can bypass it: `TransmitSafetyAuthority` implements `Aprs.Transport.ITransmitInhibitGate`, and the composition root hands that gate to the shared `AprsIsClient` (APRS-IS) and `KissRfBeaconTransmitClient` (RF). Both check it before any bytes leave. **When you add a new transmit path, route it through those shared clients (or consult the authority) — never construct a raw socket write.**
- RF must never transmit by default; RF transmit requires explicit per-port opt-in. Warn on high beacon rates and bad paths.
- Keep clear visual separation between APRS-IS receive-only, APRS-IS transmit, and RF transmit.

### APRS parsing (Aprs.Core)
- Fixed-format protocol — mind column offsets. Position data starts at index 1 for `!`/`=` and index 8 for timestamped `@`/`/` (7-char timestamp). The uncompressed symbol code sits 18 chars into the position data.
- `ack`/`rej` are **lowercase literals** (`StringComparison.Ordinal`) — never case-insensitive, or "ACKNOWLEDGED" is misread as an ack.
- Parsers must never throw on malformed/truncated/hostile input — return validation errors. The fuzz harness guards this; length fields (e.g. AGWPE) must be upper-bounded to avoid overflow/spin.

### Other
- Station persistence via SQLite survives restarts; window state persists.
- Replanning/replay is session-local where noted; keep it consistent with existing state rules.

## Security Posture
- **Local REST API** (`AprsCommand.Api`): safe-by-default — disabled, localhost-only, read-only, transmit blocked. If `RequireToken` is set, it **fails closed** when no token is configured; tokens compared in constant time. The transmit endpoint returns 501 by policy.
- **Mobile companion server** (`Aprs.Desktop/Services/MobileCompanionServer.cs`): binds to the LAN and exposes callsign/positions/messages, so it is gated by a per-session token carried as the first URL path segment (embedded in the shown URL/QR). No wildcard CORS.
- **Auto-update** (Velopack + GitHub source, HTTPS): trust rests on GitHub + TLS + Velopack SHA verification. Package signing is a wanted hardening.
- Never log secrets — APRS-IS passcodes, API tokens. Redaction exists in the log services; keep it.

## Testing
- xUnit. Inject clocks/time — **no `DateTime.Now`/`UtcNow` in tests**. Prefer fakes over real network/sockets; bound any fake-server waits.
- Required coverage: parser (with sample packets), message ACK/retry, object handling, station expiration, beacon scheduling, **transmit-safety gating** (including the global-inhibit chokepoint), REST API auth.
- Run: `dotnet test tests/Aprs.Tests/Aprs.Tests.csproj -c Debug`.
- **Debugging / hardening sessions: read and follow `docs/contributing/BUG_HUNTING_PLAYBOOK.md`.** It defines the
  method pipeline (static analysis → oracle/fuzz → safety-critical/concurrency → human), the pillars,
  and the one rule — **every bug found gets a regression test before moving on.** Parser spec
  compliance is only one pillar; most bugs live in UI, concurrency, integrations, and feature logic.
- **Documentation initiative: read `docs/planning/DOCUMENTATION_PLAN.md`.** Plan-of-record for the two big
  deliverables — the operator **User Manual** (Word → screenshots → PDF, every feature, step-by-step)
  and the **APRS-Command Programming Guide Book** (Markdown + PDF + Word; who/what/why/how of the code).
  Both use a stable-numbering + dated-amendment model, and both must look "wow"-professional.

## Approved NuGet Packages (list here before adding)
- **Avalonia** 11.3.7 (`Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Avalonia.Controls.ColorPicker` — round ColorView wheel; needs its Fluent StyleInclude in App.axaml)
- **Mapsui** 5.1.0 (`Mapsui.Avalonia`, `Mapsui.Tiling`, `Mapsui.Nts`) + **BruTile.MbTiles** 6.0.0 — mapping/tiles
- **Microsoft.Data.Sqlite** 9.0.0 + **SQLitePCLRaw.bundle_e_sqlite3** 3.0.3 — station persistence
- **Microsoft.Extensions.DependencyInjection** 10.0.0 — composition root
- **System.IO.Ports** 9.0.0 — serial KISS/GPS; **System.Device.Gpio** 3.2.0 — Raspberry Pi PTT; **Tmds.DBus.Protocol** 0.94.1 — Linux integration
- **Velopack** 1.2.0 — auto-update
- **xUnit** (+ runner, `Microsoft.NET.Test.Sdk`) — test projects only
- Network I/O uses framework `HttpClient`; persistence uses framework `System.Text.Json` — no packages needed.

## What NOT to Do
- Do not put serial/TCP/file/UI dependencies in `Aprs.Core`; do not put UI code in Services/Transport/Core.
- Do not add a transmit path that bypasses `ITransmitSafetyAuthority` / the transport inhibit gate.
- Do not block the UI thread; do not use `Thread.Sleep`; do not swallow exceptions silently.
- Do not introduce a new MVVM framework or new per-file `ICommand` classes.
- Do not add NuGet packages without listing them above first.
- Do not copy UI-View32 code, assets, copyrighted text, or proprietary map data.
- Do not implement features that encourage unlicensed/unauthorized transmitting.

## Build & Run
```bash
dotnet build CrossPlatformAprs.sln -c Release
dotnet run --project src/Aprs.Desktop
dotnet test tests/Aprs.Tests/Aprs.Tests.csproj
```

## Canonical APRS Specification References
Build **all** parser/encoder work against the current specification — **not** the obsolete APRS101.pdf (2000).
- **APRS Protocol Reference 1.2** — `github.com/wb2osz/aprsspec` (APRS101 + the aprs11 errata + the aprs12 proposals that were actually implemented). This is the authoritative spec, maintained by John Langner WB2OSZ, who technically reviewed our own primer.
- **how.aprs.works** — the current, actively maintained APRS knowledge hub (supersedes aprs.org).
- **Dire Wolf** — `github.com/wb2osz/direwolf` — the reference software TNC/decoder; use it as the source of conformance test vectors (especially MIC-E).
- **Device-ID (tocall) database** — `github.com/aprsorg/aprs-deviceid`, machine-readable at `https://aprs-deviceid.aprsfoundation.org/tocalls.dense.json` (CC BY-SA 2.0, attribution required). Maps the AX.25 destination to the sending device/software. **APRS Command's own registered tocall is `APCMD0`** (`AprsConstants.ToCall`; bump to `APCMD1` at v1.0). Every outbound packet must carry it.
- **APRS symbols** — aprsspec `APRS-Symbols` / `how.aprs.works/aprs-symbols/`.
- Conformance roadmap: `docs/architecture/APRS_SPEC_CONFORMANCE_PLAN.md`.

## External dependencies / pending approvals
- **RepeaterBook API** (`RepeaterBookService`): the Field Repeater Lookup feature needs an app-level approval from RepeaterBook (distributed-app category); operators then use their own `rbuapp_` token. **Approval is not granted as of 2026-08-01** (a reply to RepeaterBook is outstanding). The feature is built to degrade cleanly (no token → "no token configured"); do **not** assume the API works or treat the feature as shippable until approval lands.
  - The User-Agent contact email (`jrospopo@sbcglobal.net`, `RepeaterBookService.cs:33`) is the developer's **RepeaterBook account login** and is intentionally different from the primary gmail address. It is correct — do **not** "fix" it to match other contact addresses.

## Reference
- Full user/dev docs: `docs/` (User Manual, Developer Guide, Safety and Transmit Guide, APRS-IS/RF setup, etc.)
- Decisions log: `APRS-Command_Decisions_Log.md`
- Original agent instructions: `AGENTS.md`

73 de KE4CON

---

## Documentation & Installer Standards

These apply to **every** user-facing document (installation guide, user manual, programming guide, README) and **every** installer / setup script in this repo. Locked house standards.

### Audience & voice
- Assume a **lazy, non-technical reader**: does the least effort, won't read ahead, copy-pastes literally.
- **Plain, simple, layman's language**; short sentences. Detailed but easy to follow.
- **Spell out every step and click**; name the exact button/menu/field label. Say **what the reader will see** after each action.
- **Front-load a short "In a nutshell" / "Quick version" summary** at the top of each document and each chapter, so a skimmer still succeeds.
- **Define every acronym in full on first use** — e.g. "Application Programming Interface (API)" — then use the short form.
- **Never use a placeholder a reader might type literally** (e.g. `SERVER-IP`). Tell them to substitute their real value and give a concrete example.
- **Anticipate common errors** and say exactly what to do about each.
- **Length is not a concern — thoroughness is.** Never trade completeness for brevity.
- **American English spelling** (color, center, meters).

### User manuals — depth standard (the APRS-Command User Manual is the benchmark)
- Roughly **1,500–1,800 words and ~33 content blocks per chapter**.
- **Name every UI element exactly**; **tabulate every field / column / option** with a plain-language "what it means".
- Use sub-sections for variations/edge cases; **end every feature chapter with a Troubleshooting section** (symptom → fix).
- Include dedicated **reference chapters**: Troubleshooting & FAQ, Glossary, Keyboard Shortcuts, Menu/Navigation Reference (and Licenses & Credits where relevant).
- **Ground every step in the real application UI / source** — read the actual screens; never guess a label.

### Installers / setup
- **Automate as much as possible**: a **one-command setup script** with **interactive prompts** whose defaults are accepted by pressing **Enter**. The fewer chances to fail installing, the better.

### House doc pipeline
- Navy + gold python-docx house style (`style.py`). **Markdown is the living source of truth**; the styled `.docx` / PDF is generated from **per-chapter JSON** (`chapters/*.json` + a `build.py`) — the same pipeline as the APRS-Command manual and the OpenTrack guides.
- Each project ships three core documents: **Installation Guide, User Manual, Programming Guide.**
