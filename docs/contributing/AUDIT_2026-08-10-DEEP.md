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
- ☑ **Concurrency — `BeaconService.ApplySettings` on the GPS thread rebuilt the APRS-IS client every fix.**
  Fixed: `ApplySettings` computes a connection signature (server/port/passcode/filter/callsign/transmit
  flags) and rebuilds the client only when it changes; the swap is `clientLock`-guarded; `CreateFromSettings`
  seeds the signature so the first write-back doesn't rebuild; the GPS write-back marshals the call to the UI
  thread. Test: `BeaconServiceApplySettingsTests`.
- ◐ **Tests/coverage.** ☑ `SqliteStationDatabase` tests added (round-trip, corrupt-row tolerance, concurrent
  write+dispose; added a path-override ctor for isolation). ☑ In-suite deterministic fuzzer added
  (`DeterministicFuzzTests` — 300k iterations over `AprsParser` + KISS/AGWPE codecs, no throw/hang, runs in
  CI). ☐ Remaining: a full menu-reachability test (every `*Requested` event has a subscriber) — a cheap
  reflection test, tracked residual.

---

## MEDIUM — all fixed

- ☑ **Parser/spec — dotted wind (`.../...`) aborted weather parsing.** Now skips the 7-char wind slot when
  non-numeric so gust/temp/rain/humidity/baro still parse (wind stays null). Oracle-confirmed. Test added.
- ☑ **Parser/spec — item live/kill state dropped.** `ItemAprsPacket` gains `IsKilled`/`IsAlive` from the
  `!`/`_` separator. Test added.
- ☑ **Security — Mobile Companion CSP.** The HTML response now sends a strict `Content-Security-Policy` +
  `X-Content-Type-Options: nosniff` + `Referrer-Policy: no-referrer` (defense-in-depth behind the escaping).
- ☑ **Concurrency — `LiveDataCoordinator` failover leak.** The previous receive-only client is now
  disconnected + disposed before the new one is assigned.
- ☑ **Services — `WeatherBeaconScheduler` no backoff on failure.** `NextScheduledTransmitTimeUtc` now
  advances on failure too, so a failing transport isn't re-fired every tick.

---

## LOW — fixed

- ☑ **Parser/spec — luminosity lowercase `l` = value+1000.** Fixed on both parse (add 1000) and emit
  (`l{v-1000}` for 1000–1999). Oracle-confirmed. Test added.
- ☑ **Parser/spec — MIC-E mixed standard/custom bits.** Custom table now indexed with `stdMsg | custMsg`.
- ☑ **Services — weather humidity 0 %.** Formatter clamps an implausible 0 % up to 1 % so it isn't emitted
  as the `h00` (=100 %) sentinel.
- ☑ **Persistence — `DeleteTacticalLabel` key normalization.** Now trims+upper-cases the callsign like the
  write path, so a varied-case delete no longer leaves an orphan row.
- ☑ **Security — update-URL scheme validation.** The release URL is parsed and asserted `http`/`https`
  before `Process.Start(UseShellExecute=true)`.

## LOW — tracked residuals (genuinely minor; not yet fixed)

- ☐ **Parser/spec — telemetry:** a trailing comment on the 8-bit digital field is rejected; a non-numeric
  (`MIC`) sequence is rejected. Take the first 8 bits + treat the rest as comment; accept the `MIC` form.
- ☐ **Parser/spec — AX.25 decoder discards the digipeater path** (`Ax25AprsPayloadDecoder` rebuilds only
  `source>dest:info`). Heard-via digis are lost on the *decode* side; the encoder side is correct.
- ☐ **Services — ACK/REJ matched by message id alone**, 2-digit id space (`% 100`). A late ACK from a
  different station could match a recycled id. Match on (id **and** ack source == recipient).
- ☐ **Persistence — SQLite never stores Object/Item snapshots** (they key by name, not source callsign);
  **out-of-order persistence** (per-packet `Task.Run`, no ordering guard) can persist a stale snapshot under
  a busy feed. Both self-heal from live packets; restart-persistence only.
- ☐ **Security — LAN FieldCommand feed** is tokenless + `Access-Control-Allow-Origin: *` + all-interface
  bind (a documented EMCOMM-NET trade-off; consider a one-time in-app warning + origin-restricted CORS).
  **Winlink API key in URL query string**, surfaceable in exception text.
- ☐ **Concurrency (minor)** — `PacketStatisticsService.currentHour` torn read at the hour boundary;
  `lastInboxCount` RMW race (missed/double toast); `CalTopoForwardingService` never disposed (app-lifetime
  `HttpClient` leak); a fire-and-forget `ConnectAsync` swallows faults.
- ☐ **Persistence (minor)** — `JsonAppSettingsStore.Save/Update` unsynchronized on the process-wide
  singleton; `WindowStateService` off-screen math has no tests.
- ☐ **Tests — full menu-reachability test** (assert every `*Requested` event has a `MainWindow` subscriber /
  every `Open*Command` resolves) — cheap reflection test, would catch a menu item wired to a removed handler.
- ☐ **Reachability (intent)** — RepeaterBook + Winlink have full handlers but no menu item (deliberately
  held); dead `enum MainFeaturePanel`. Add a `// held` comment / delete the dead enum in a cleanup pass.

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
