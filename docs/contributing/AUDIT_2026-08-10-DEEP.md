# APRS-Command — Deep Multi-Lens Audit (2026-08-10, second pass)

A second, exhaustive audit after the first (`AUDIT_2026-08-10.md`). Six specialized reviewers (concurrency,
transmit-safety, services/feature-logic, security, parser spec-conformance, UI/persistence/tests) plus a
Dire-Wolf **oracle diff** of the parser over a spec corpus. Every finding was verified against real code;
spec findings were cross-checked against `decode_aprs.exe` 1.8.1. Legend: ☑ fixed · ◐ partial · ☐ open.

Status counts are updated as fixes land. Every fix carries a regression test (playbook rule).

---

## CRITICAL / HIGH

- ☑ **Transmit-safety — N0CALL can key up on RF (FCC §97.119).** The identity gate lived only in
  `TransmitSafetyAuthority.Evaluate`, which the RF beacon/message/weather paths never call; the transport
  inhibit gate checks *global inhibit only*, not identity. APRS-IS is incidentally safe (blocked by the
  missing passcode) but RF has no passcode backstop, so a default profile (valid position + path, callsign
  still `N0CALL`) with RF transmit enabled would beacon unidentified. **Fix:** identity gate at the single RF
  chokepoint `KissRfBeaconTransmitClient.SendBeaconAsync` — blocks empty/`N0CALL`/`NOCALL`/`MYCALL` before
  any byte, covering beacon+message+weather. Test: `KissRfBeaconTransmitClientTests`.
- ☑ **Concurrency — `AprsMessageStoreService` unsynchronized `List` across 3 threads.** UI (compose/refresh),
  the 10 s retry tick, and transport receive threads (incoming ACK via `MessageAckCoordinator.ProcessIncomingPacket`)
  all touch `messages` with no lock → "Collection was modified" thrown on the receive thread → caught by
  `Fault()` → the KISS/AGWPE receive loop exits permanently (RF port goes dead until restart). **Fix:** `sync`
  lock on every access, event raised outside the lock, getters return copies.
- ☑ **Concurrency — `GeofenceService` unsynchronized `List`+`Dictionary` on receive threads.**
  `EvaluateStationPosition` runs on every KISS/AGWPE receive thread and writes `stationInsideState`;
  concurrent `Dictionary` mutation can corrupt buckets → 100 %-CPU spin / `IndexOutOfRange`. **Fix:** `sync`
  lock around all geofence + state access.
- ☑ **Services — positionless weather emits `HHMMSS`, not the spec `MMDDHHMM` (MDHM).** `AprsWeatherFormatter.BuildTimestamp`
  returned 6 digits; the `_` positionless form needs an 8-digit MDHM. Our own parser (MDHM-first) then ate two
  wind-direction digits, losing every weather field; other decoders (aprs.fi/Dire Wolf) equally can't decode
  it. Oracle-confirmed. **Fix:** `ToString("MMddHHmm")`. Test: `WeatherObservation_Positionless_RoundTrips`
  + corrected `WeatherReportWithoutPositionCanBeFormatted`. *(Found independently by the services and
  parser-spec reviewers.)*
- ☑ **Services — `BeaconScheduler` reset BOTH beacon timers on every beacon → RF never fires.**
  `CalculateNextBeaconTimes` recomputed `NextAprsIs` **and** `NextRf` from `now` on each beacon; with the
  default 30 min APRS-IS / 60 min RF the RF countdown was perpetually pushed past the next APRS-IS fire and
  never elapsed — RF beaconing silently disabled. **Fix:** per-transport `recomputeAprsIs`/`recomputeRf`
  flags; firing one transport advances only its own timer. Test: `BeaconNow_FiringAprsIs_DoesNotPostponeRfBeaconTimer`.
- ☑ **Parser/spec — compressed positions with an OVERLAY symbol misclassified as uncompressed → position
  lost.** `AprsCompressedPositionDecoder.IsCompressed` treated only `/` and `\` as compressed, but a
  compressed leading byte is the Symbol Table Identifier, which may also be an overlay letter `A`–`Z` or
  `a`–`j` (numeric overlays are encoded as `a`–`j` so it's never a digit). Any overlaid compressed
  position/object/item/weather was routed to the uncompressed parser and dropped (null lat/lon). Oracle-
  confirmed against Dire Wolf. **Fix:** recognize `/`, `\`, `A`–`Z`, `a`–`j` as compressed.
- ☐ **Concurrency — `BeaconService.ApplySettings` on the GPS thread rebuilds the APRS-IS client every fix.**
  `WireGpsWriteback` calls `ApplySettings` on the background GPS loop (unmarshaled) on every position update;
  `ApplySettings` unconditionally disposes + rebuilds + reconnects the APRS-IS client → ~1 Hz connection
  thrash for a mobile station, a torn `aprsIsClient` field vs. UI-thread saves, and a stale captured client
  in `MessageAckCoordinator` (messaging silently dies after the first rebuild). **Fix (planned):** only
  rebuild when connection-relevant config changed; guard the field; give the message coordinator a live
  handle; marshal.
- ◐ **Tests/coverage — critical gaps.** ☐ `SqliteStationDatabase` has **zero** tests (round-trip, prune
  trigger, corrupt-row tolerance, concurrent write+dispose — the C4-fixed class). ☐ The fuzz harness is a
  live-APRS-IS console Exe that fuzzes only `AprsParser.Parse` and **never runs in CI**; no in-suite
  deterministic fuzz over the weather drivers, `KissFrameCodec`, `AgwpeFrameCodec`, NMEA, MIC-E. ☐ Full
  menu-reachability test still absent. Plan: add all three.

---

## MEDIUM

- ☐ **Parser/spec — unknown/dotted wind (`.../...`) aborts weather field parsing.** The `DDD/SSS` branch
  requires all-digit fields; dotted wind matches neither branch, so `index` stays 0 and gust/temp/rain/
  humidity/baro all fall into the comment. Oracle-confirmed (Dire Wolf skips dotted wind, parses the rest).
  Fix: when the 7-char wind slot is non-numeric, leave wind null but advance the index past it.
- ☐ **Parser/spec — item live/kill state dropped.** `ItemAprsPacket` has no `IsKilled`/`IsAlive`; a killed
  item (`_` separator) is indistinguishable from live (`!`), so a station removing its item isn't honored.
  Fix: capture the separator, add the fields (mirror the object parser).
- ☐ **Security — Mobile Companion has no CSP (incomplete H1 fix).** The first audit recorded H1 as "escape
  + add CSP"; only the escaping landed. No `Content-Security-Policy`/`Referrer-Policy`/`X-Content-Type-Options`
  is emitted, and the client-side `esc()` is the sole XSS control on a page that carries the session token.
  Fix: send a strict CSP + the two hardening headers on the HTML response.
- ☐ **Concurrency — `LiveDataCoordinator.ConnectAprsIsReceiveOnly` leaks the old client on failover.** Each
  server switch overwrites `aprsIsClient` without disconnecting/disposing/unsubscribing the prior one → one
  leaked socket + task + CTS per failover, plus duplicate ingestion if the old server recovers. Fix: tear
  down the old client before assigning the new one.
- ☐ **Services — `WeatherBeaconScheduler` re-fires a failing transmit every tick (no backoff).** On failure
  `NextScheduledTransmitTimeUtc` isn't advanced, so a persistently failing transport is hammered every tick.
  Fix: advance the next time on failure too (optionally shorter retry).

---

## LOW

- ☐ **Parser/spec — luminosity ≥1000 W/m² (lowercase `l`) off by 1000 both ways.** Parse treats `l`/`L`
  identically (should add 1000 for `l`); format clamps to `L999` (should emit `l{v-1000}` for 1000–1999).
  Oracle-confirmed. *(Also flagged by the services reviewer.)*
- ☐ **Parser/spec — MIC-E mixed standard/custom message bits pick the wrong message.** The custom index uses
  `custMsg` alone; the true value is `stdMsg | custMsg`. Only malformed mixed-encoding packets are affected.
- ☐ **Parser/spec — telemetry: trailing comment on the 8-bit digital field rejected; non-numeric (`MIC`)
  sequence rejected.** Take the first 8 bits + treat the rest as comment; accept the `MIC` sequence form.
- ☐ **Parser/spec — AX.25 decoder discards the digipeater path** (`Ax25AprsPayloadDecoder` rebuilds only
  `source>dest:info`). Encoder side is correct.
- ☐ **Services — weather humidity 0 % encodes as `h00` which parses back as 100 %.** Formatter should guard
  a 0 input (`h00` = 100 % is the spec sentinel).
- ☐ **Services — ACK/REJ matched by message id alone; 2-digit id space (`% 100`).** A late ACK from station
  B can acknowledge a message actually sent to A. Match on (id **and** ack source == recipient).
- ☐ **Persistence — SQLite never stores Object/Item snapshots** (keyed by source callsign, but objects key
  by name); **`DeleteTacticalLabel` doesn't normalize the key** like the write paths do (case/whitespace
  callsign leaves a DB row that resurrects on restart); **out-of-order persistence** (per-packet `Task.Run`,
  no ordering guard) can persist a stale snapshot under load.
- ☐ **Security — LAN feed is tokenless + `Access-Control-Allow-Origin: *` + all-interface bind** (documented
  trade-off; consider a one-time warning + origin-restricted CORS). **Update-URL `Process.Start(UseShellExecute=true)**`
  without asserting `https` scheme. **Winlink API key in URL query string**, surfaceable in error text.
- ☐ **Concurrency (minor)** — `PacketStatisticsService.currentHour` torn read at hour boundary;
  `lastInboxCount` RMW race (missed/double toast); `CalTopoForwardingService` never disposed/unsubscribed
  (app-lifetime `HttpClient` leak); a fire-and-forget `ConnectAsync` swallows faults.
- ☐ **Persistence (minor)** — `JsonAppSettingsStore.Save/Update` unsynchronized on the process-wide
  singleton (single `.tmp` path collision under concurrent saves); `WindowStateService` off-screen math has
  no tests.
- ☐ **Reachability (intent check)** — RepeaterBook + Winlink have full command→window handlers but no menu
  item (deliberately held; add a `// held` comment so they don't read as wired). Dead `enum MainFeaturePanel`.

---

## Verified SOUND (deep pass, oracle where noted)
Transport primitives (all four clients — snapshot-under-lock, no lock-across-await, bounded channels,
reconnect cleanup); `StationDatabase`/`RawPacketLogService`/`StationTrailService` locks; replay/sim/training
transmit-inert; REST/WS/FileHook cannot transmit (zero transport refs); message ACK/retry per-message lock;
digipeater New-N + iGate mandatory NOGATE/RFONLY/TCPIP/q-construct blocks; SSRF (all hosts hardcoded); path
traversal (fixed folders + `Path.GetFileName`); XXE (DtdProcessing.Prohibit); deserialization (no
TypeNameHandling, depth-capped, size-capped); CSV formula-injection guard; Leaflet SRI; secret-logging clean;
VoiceAlert/Direwolf command-injection neutralized. Parser: uncompressed offsets, all timestamp variants,
compressed base-91 math, MIC-E long/speed/course/altitude, object/item offsets + kill char, weather negative
temp / snow / humidity, message addressee/reply-ack/bulletins, status, telemetry, DAO — all field-verified,
oracle-matched where a decoder exists.
