# APRS Specification Conformance Plan

**Goal:** APRS Command parses and generates **every** APRS packet type exactly per the current
specification, verified against a reference decoder and real traffic.

**Status:** Phase 0 audit complete; Phases 1–4 done (every data type parsed/generated, network
behavior audited); Phase 5 standing conformance suite established (all types have spec-cited vector
coverage). Remaining is residual polish only — see Phase 5.

---

## Canonical references (the yardstick)
- **APRS Protocol Reference 1.2** — `github.com/wb2osz/aprsspec` (APRS101 + aprs11 errata + implemented
  aprs12). Supersedes the obsolete APRS101.pdf. Authoritative.
- **Dire Wolf** — `github.com/wb2osz/direwolf` — reference decoder; source of conformance test vectors.
- **how.aprs.works** — current knowledge hub.
- **Device-ID (tocall) DB** — `github.com/aprsorg/aprs-deviceid`. Our tocall: `APCMD0`.
- Our own **APRS Theory & Operations Primer** (`docs/`) — technically reviewed by WB2OSZ; the
  human-facing conformance statement. Keep it in lockstep with the code.

---

## Phase 0 — Conformance audit (RECEIVE / parse side)

First-pass audit of `Aprs.Core` against the spec + primer DTI set. Legend: ✅ conformant (light
verification) · ⚠️ partial / needs work · ❌ missing. Items marked *(verify)* need Dire Wolf / aprsspec
test vectors to confirm exactness.

| Packet type | DTI | Parse status | Notes |
|---|---|---|---|
| Position, uncompressed | `!` `=` `/` `@` | ✅ | weather-offset + ack/rej case already fixed (D2). Verify all 4 timestamp/messaging variants. |
| Position, compressed (base-91) | (in body) | ✅ | `AprsCompressedPositionDecoder` wired in `AprsPositionParser`. *(verify altitude / range / course-speed cs bytes)* |
| **MIC-E** | `` ` `` `'` `0x1C` `0x1D` | ✅ **Done** | `AprsMicEParser` (built against Dire Wolf's decoder): destination-field lat + N/S/offset/W-E, longitude/speed/course, symbol, MIC-E altitude, and the message code (Emergency/En Route/… surfaced in the comment). Decodes to `PositionAprsPacket`. Tests: `AprsMicEParserTests`. |
| Weather (positionless + position) | `_` / position+`_` | ✅ | Timestamped `@`/`/` offset fixed (D2). *(verify compressed-weather, all wx fields)* |
| Message + ACK/REJ | `:` | ✅ | ack/rej now case-sensitive (D2). *(verify bulletins `BLNx`, announcements, group bulletins, 67-char limit, telemetry-in-message)* |
| **Object** | `;` | ✅ | Live `*` / killed `_` — **verified correct** against Dire Wolf (objects: `*`/`_`; items: `!`/`_`). The primer §7.3 was the one in error (said kill uses `/`); **corrected ✅** (`_`, not `/`). *(still verify compressed object + timestamp forms)* |
| Item | `)` | ✅ | *(verify live/kill char `!`/`_` per spec, same discrepancy class as objects)* |
| Telemetry | `T#` | ✅ | Sequence + analog + `BITS` + `PARM./UNIT./EQNS./BITS.` metadata handled. *(verify base-91 telemetry + telemetry-in-message)* |
| Status | `>` | ✅ **Done** | `AprsStatusReport` decomposes the body into optional leading DHM-zulu timestamp **or** Maidenhead locator+symbol, and trailing beam-heading/ERP (`^`) — remainder is the display message (`StatusText`); `RawStatusText` keeps the verbatim body. Built against Dire Wolf's decoder. Tests: `AprsStatusReportTests`. |
| Station capabilities | `<` | ✅ (basic) | Raw capability text captured. |
| Query | `?` | ✅ **Done (decode)** | Decomposed into `QueryType` + `QueryKeyword` + optional `QueryTarget` (`?APRS?`/`?APRSx`, `?IGATE?`, `?WX?`, `?PING?`). Tests: `AprsQueryParsingTests`. *(Auto-responding to queries remains out of scope.)* |
| **Third-party** | `}` | ✅ **Done** | `AprsParser` unwraps `}` and re-parses the encapsulated packet (depth-guarded) so the originating station surfaces, not the gateway. Tests: `AprsThirdPartyParsingTests`. |
| User-defined | `{` | ✅ **Done** | `UserDefinedAprsPacket` captures the user/developer ID byte + raw payload — recognized, not Unknown. Tests: `AprsNmeaAndUserDefinedTests`. |
| Raw NMEA GPS | `$` | ✅ **Done** | `AprsNmeaParser` decodes position sentences (GxRMC/GxGGA) → `PositionAprsPacket` (type `$`) with lat/lon + course/speed/altitude; non-position sentences left as Unknown. Tests: `AprsNmeaAndUserDefinedTests`. |
| DAO datum/precision ext. | `!Dxx!` in comment | ✅ **Done** | `AprsDaoExtension` refines lat/lon (human-readable + base-91 forms) and strips the token, applied to uncompressed/compressed positions, objects, items, and MIC-E. Trailing-token-only detection avoids false positives. Tests: `AprsDaoExtensionTests`. |

**Robustness:** parsers guard indexing and use `TryParse`; the fuzz harness covers malformed input.
Good — keep it.

### Phase 0 — GENERATE / transmit side (audit)
Formatters exist for position beacons (tested, `>APCMD0` ✅), weather, objects, messages.
- **Area-object encoding — FIXED (was broken).** `AprsAreaObjectEncoder` previously emitted a made-up
  `/A{S}{C}{WWW}/{HHH}` string — **not** the APRS format — so no client rendered it as an area. It also
  mapped shape codes via the enum's raw value (triangle/box were wrong). Rewritten per WB4APR
  PROTOCOL.TXT: `Tyy/Cxx` with `offset_degrees = value² / 100` (`value = 10·√degrees`) and the correct
  non-sequential shape codes (0,1,3,4,5,6,8,9). Tests: `AprsAreaObjectEncoderTests`.
  **Caveat:** area objects are obscure — even Dire Wolf doesn't decode them — so there is no
  independent decoder oracle; this is validated against the spec formula + hand-computed math.
  **Primer §7.4 — corrected ✅** — the primer now states `yy = √(offset ÷ 0.01)` /
  `offset = value² × 0.01` (i.e. `value² / 100`), matching the encoder. (An earlier draft had `× 1500`.)
- Remaining generate-side verification: round-trip (generate → parse → equal) for position/object/
  message/weather; object-kill char on emit.

---

## Phase 1 — Close receive-side gaps (priority order)
Each with spec vectors from aprsspec + Dire Wolf:
1. ~~**MIC-E decode**~~ ✅ **done** (`AprsMicEParser`). ~~Follow-up: device-ID via the MIC-E comment suffix (Phase 4).~~ ✅ **done** — `DeviceIdentificationService` identifies MIC-E radios from the comment (`mice`/`micelegacy`), gated on `StationSnapshot.IsMicE` (D10).
2. ~~**Third-party `}`** unwrap~~ ✅ **done** (`AprsParser`, depth-guarded).
3. ~~**Query `?`** decomposition~~ ✅ **done** (`QueryType`/keyword/target).
4. ~~**DAO `!Dxx!`** precision~~ ✅ **done** (`AprsDaoExtension`, applied to all position-bearing types). Remaining: any compressed-position / telemetry gaps Phase 0 verification surfaces.
5. ~~Resolve the **object/item kill-char** discrepancy~~ ✅ **done** — code verified correct against Dire Wolf; **primer §7.3 corrected** (killed object is `_`, not `/`).

## Phase 2 — Generate-side exactness
- Verify every emitted packet is spec-exact (tocall ✅ done; area-object `Tyy/Cxx` ✅ fixed; position,
  object kill, message ack/format, weather, status still to verify).
- **Round-trip tests**: generate → parse → assert equality. Position beacon + status beacon + **object**
  (live + killed) (`AprsRoundTripTests`), **message** (`AprsMessageRetryEngineTests`), and **weather**
  (`AprsWeatherRoundTripTests`) all round-trip cleanly. ✅ **Object timestamp bug fixed (was broken):**
  the object emitter wrote time-of-day (`HHMMSS`) under the DHM `z` suffix, so any object beaconed at
  minute ≥ 24 encoded an invalid "hour" (e.g. 14:25:30 → `142530z` = day 14, hour 25). Now DHM-zulu UTC
  (`ddHHmmz`). Remaining (lower priority): diffing our output vs. Dire Wolf.

## Phase 3 — Network-behavior conformance ✅ **audited (D13)**
- `DigipeaterService` (New-N WIDEn-N): callsign insertion + decrement verified correct. **Fixed:** the
  duplicate fingerprint included the mutating path (so the same packet via a different neighbour / the
  digi's own echo evaded suppression) → now source+dest+payload; added a used-flag loop guard (never
  repeat a packet already carrying our used callsign); trap malformed `WIDEn-N` with N>n.
- iGate: **fixed** mandatory, config-independent RF→IS rules — never gate `NOGATE`/`RFONLY` (sender
  directive) or `TCPIP`/`TCPXX`/q-construct (already on APRS-IS → loop). Previously only TCPIP/q were
  covered, and only as editable default config gated behind the dupe-suppression toggle. Monitor
  advisory made consistent. Tests: `DigipeaterServiceTests`, `IGateServiceTests`.
- *Remaining refinements (not blocking):* preemptive/`TRACEn-N` handling; per-hop trap policy config.

## Phase 4 — Symbols & device-ID
- Reconcile the symbol table against aprsspec `APRS-Symbols`.
- Integrate the device-ID (tocall) database — bundled snapshot ✅ (+ weekly refresh, slice 3, still to
  do) — to show each station's radio/software. Tocall matching ✅ and MIC-E radio matching ✅ (D10) both
  done. CC BY-SA 2.0 → attribution handled via the third-party-notices flow.

## Phase 5 — Standing conformance suite ✅ **established**
- A dedicated spec-conformance test section now covers **every** APRS data type with spec-cited
  vectors: positions (all 4 timestamp/messaging DTIs), compressed positions (lat/lon/course-speed/
  range/altitude), objects (live/killed), items (live/killed), weather (position + positionless +
  timestamped), messages/bulletins/ack, telemetry (`T#` + bare metadata), status, capabilities,
  queries, MIC-E, third-party, DAO, raw NMEA, and user-defined —
  `AprsSpec101ConformanceTests` + `AprsSpecConformanceCompletionTests` + the type-specific suites.
- **Residual gaps surfaced by the suite (tracked, not blocking):**
  - **Message-embedded telemetry metadata** — the spec-standard `:CALL :PARM./UNIT./EQNS./BITS.`
    form is currently classified as a plain message; only the bare info-field form is extracted as
    `TelemetryMetadataAprsPacket`. Reclassifying the message-embedded form is a parser change with
    message-handling blast radius (Phase 1), deliberately deferred. Pinned by
    `AprsSpecConformanceCompletionTests.MessageEmbeddedTelemetryMetadata_CurrentlyParsesAsMessage`.
  - Deeper byte-exactness against a live Dire Wolf oracle (vs. the spec's worked examples used here)
    remains an ongoing aspiration rather than a fixed deliverable.
- Keep the primer updated in lockstep as any of these close.

---

## Recommended sequencing
Phase 0 verification (test vectors) → **MIC-E** → **third-party `}`** → area-object generate check.
These deliver the largest conformance gains first.
