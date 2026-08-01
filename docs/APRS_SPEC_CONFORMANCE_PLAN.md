# APRS Specification Conformance Plan

**Goal:** APRS Command parses and generates **every** APRS packet type exactly per the current
specification, verified against a reference decoder and real traffic.

**Status:** Phase 0 (audit) — first pass complete, below. Phases 1–5 not started.

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
| **Object** | `;` | ✅ | Live `*` / killed `_` — **verified correct** against Dire Wolf (objects: `*`/`_`; items: `!`/`_`). The primer §7.3 is the one in error (says kill uses `/`); the code is right. **Action: correct the primer.** *(still verify compressed object + timestamp forms)* |
| Item | `)` | ✅ | *(verify live/kill char `!`/`_` per spec, same discrepancy class as objects)* |
| Telemetry | `T#` | ✅ | Sequence + analog + `BITS` + `PARM./UNIT./EQNS./BITS.` metadata handled. *(verify base-91 telemetry + telemetry-in-message)* |
| Status | `>` | ⚠️ Partial | Stored as raw text; not decomposed (timestamp, Maidenhead locator, beam heading). Fine for display, incomplete for structured use. |
| Station capabilities | `<` | ✅ (basic) | Raw capability text captured. |
| Query | `?` | ✅ **Done (decode)** | Decomposed into `QueryType` + `QueryKeyword` + optional `QueryTarget` (`?APRS?`/`?APRSx`, `?IGATE?`, `?WX?`, `?PING?`). Tests: `AprsQueryParsingTests`. *(Auto-responding to queries remains out of scope.)* |
| **Third-party** | `}` | ✅ **Done** | `AprsParser` unwraps `}` and re-parses the encapsulated packet (depth-guarded) so the originating station surfaces, not the gateway. Tests: `AprsThirdPartyParsingTests`. |
| User-defined | `{` | ❌ Missing | Minor / experimental. |
| Raw NMEA GPS | `$` | ❌ Missing | Legacy; rarely needed. |
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
  **Primer §7.4 is wrong too** (a second doc error, like §7.3): it states `offset = value² × 1500`; the
  correct scaling is `value² / 100`. **Action: correct the primer §7.4 formula.**
- Remaining generate-side verification: round-trip (generate → parse → equal) for position/object/
  message/weather; object-kill char on emit.

---

## Phase 1 — Close receive-side gaps (priority order)
Each with spec vectors from aprsspec + Dire Wolf:
1. ~~**MIC-E decode**~~ ✅ **done** (`AprsMicEParser`). Follow-up: device-ID via the MIC-E comment suffix (Phase 4).
2. ~~**Third-party `}`** unwrap~~ ✅ **done** (`AprsParser`, depth-guarded).
3. ~~**Query `?`** decomposition~~ ✅ **done** (`QueryType`/keyword/target).
4. ~~**DAO `!Dxx!`** precision~~ ✅ **done** (`AprsDaoExtension`, applied to all position-bearing types). Remaining: any compressed-position / telemetry gaps Phase 0 verification surfaces.
5. ~~Resolve the **object/item kill-char** discrepancy~~ ✅ **done** — code verified correct against Dire Wolf; the **primer §7.3 needs correcting** (killed object is `_`, not `/`).

## Phase 2 — Generate-side exactness
- Verify every emitted packet is spec-exact (tocall ✅ done; area-object `Tyy/Cxx` ✅ fixed; position,
  object kill, message ack/format, weather, status still to verify).
- **Round-trip tests**: generate → parse → assert equality. Position beacon + status beacon
  (`AprsRoundTripTests`) and **weather** (`AprsWeatherRoundTripTests` — wind/gust/temp/humidity/
  pressure all survive) round-trip cleanly. Remaining (lower priority — simpler formats, and their
  formatters sit behind the object-editor service / message-retry engine): object + message
  round-trips; and diffing our output vs. Dire Wolf.

## Phase 3 — Network-behavior conformance
- Audit `DigipeaterService` against **APRS-Digipeater-Algorithm.pdf** (New-N WIDEn-N, fill-in vs
  wide-area, dupe-suppression window, used-flag / callsign insertion).
- iGate gating rules incl. RF↔IS loop prevention (primer §5.4); `GATE`/`NOGATE`/`RFONLY`.

## Phase 4 — Symbols & device-ID
- Reconcile the symbol table against aprsspec `APRS-Symbols`.
- Integrate the device-ID (tocall) database — bundled snapshot + weekly refresh — to show each
  station's radio/software. CC BY-SA 2.0 → handle attribution via the third-party-notices flow.

## Phase 5 — Standing conformance suite
- A dedicated spec-conformance test section fed by aprsspec + Dire Wolf vectors, guarding conformance
  continuously. Keep the primer updated in lockstep as gaps close.

---

## Recommended sequencing
Phase 0 verification (test vectors) → **MIC-E** → **third-party `}`** → area-object generate check.
These deliver the largest conformance gains first.
