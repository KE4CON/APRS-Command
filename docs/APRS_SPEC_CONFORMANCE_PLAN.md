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
| Query | `?` | ⚠️ Partial | Stored raw; query type (`?APRS?`, `?WX?`, directed queries) not decomposed or answerable. |
| **Third-party** | `}` | ✅ **Done** | `AprsParser` unwraps `}` and re-parses the encapsulated packet (depth-guarded) so the originating station surfaces, not the gateway. Tests: `AprsThirdPartyParsingTests`. |
| User-defined | `{` | ❌ Missing | Minor / experimental. |
| Raw NMEA GPS | `$` | ❌ Missing | Legacy; rarely needed. |
| DAO datum/precision ext. | `!w..!` in comment | ❌ Missing | aprs12 precision enhancement; refines position accuracy. |

**Robustness:** parsers guard indexing and use `TryParse`; the fuzz harness covers malformed input.
Good — keep it.

### Phase 0 — GENERATE / transmit side (audit pending, key item flagged now)
Formatters exist for position beacons (tested, `>APCMD0` ✅), weather, objects, messages. The one to
verify first: **area-object encoding** — the primer §7.4 specifies the aprs11-corrected
`offset_degrees = value² × 1500` √-scaling for the `Tyy/Cxx` extension. Confirm `ObjectEditorViewModel` /
the area-object encoder implement exactly this (this correction is a frequent implementation error).

---

## Phase 1 — Close receive-side gaps (priority order)
Each with spec vectors from aprsspec + Dire Wolf:
1. ~~**MIC-E decode**~~ ✅ **done** (`AprsMicEParser`). Follow-up: device-ID via the MIC-E comment suffix (Phase 4).
2. ~~**Third-party `}`** unwrap~~ ✅ **done** (`AprsParser`, depth-guarded).
3. **Query `?`** decomposition.
4. **DAO `!w..!`** precision; plus any compressed-position / telemetry gaps Phase 0 verification surfaces.
5. ~~Resolve the **object/item kill-char** discrepancy~~ ✅ **done** — code verified correct against Dire Wolf; the **primer §7.3 needs correcting** (killed object is `_`, not `/`).

## Phase 2 — Generate-side exactness
- Verify every emitted packet is spec-exact (tocall ✅ done; position, area-object √/1500, object kill,
  message ack/format, weather, status).
- **Round-trip tests**: generate → parse → assert equality; diff our output vs. Dire Wolf's decode.

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
