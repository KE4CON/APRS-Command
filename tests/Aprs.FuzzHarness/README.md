# APRS Command — APRS-IS Fuzz Harness

Connects to the live APRS-IS network, streams real packets through the
parser, and reports crashes, misdecodes, and performance outliers.

This is a **manual tool** — it is not part of the automated CI suite. Run
it periodically or before a release to verify the parser handles real-world
traffic without errors.

---

## What it checks

| Check | Description |
|---|---|
| **Crashes** | Any unhandled exception from `AprsParser.Parse()` |
| **Null returns** | Parser returns null instead of a packet |
| **Misdecodes** | `IsValid=true` but coordinates/speed/course out of physical range |
| **Slow packets** | Any packet taking longer than the configured threshold to parse |
| **Packet type distribution** | Counts by type — useful for spotting unknown/raw packet spikes |

---

## Running it

```bash
cd tests/Aprs.FuzzHarness
dotnet run -c Release -- --minutes 10
```

**With your own callsign (recommended for better server routing):**

```bash
dotnet run -c Release -- \
  --callsign KE4CON \
  --passcode 12345 \
  --minutes 30
```

**World feed (high volume — use briefly):**

```bash
dotnet run -c Release -- --radius 20000 --minutes 5
```

---

## Options

| Flag | Env var | Default | Description |
|---|---|---|---|
| `--callsign` | `APRS_CALLSIGN` | `N0CALL` | Your callsign (passcode -1 = receive-only) |
| `--passcode` | `APRS_PASSCODE` | `-1` | APRS-IS passcode (-1 for receive-only) |
| `--radius` | `APRS_RADIUS` | `500` | Filter radius in km from (0,0). Use ≥20000 for world feed. |
| `--minutes` | `APRS_MINUTES` | `10` | How long to run |
| `--slow-ms` | `APRS_SLOW_MS` | `50` | Threshold in milliseconds to flag a packet as slow |

---

## Exit codes

| Code | Meaning |
|---|---|
| `0` | No crashes or misdecodes found |
| `2` | Failed to connect to APRS-IS |
| `3` | Crashes or misdecodes were found — check output for details |

Exit code 3 makes this usable in shell scripts or manual CI steps:
```bash
dotnet run -c Release -- --minutes 10 || echo "Parser issues found!"
```

---

## What to do with findings

**Crashes**: copy the raw packet line and write a failing unit test in
`tests/Aprs.Tests/AprsParserTests.cs`, then fix the parser. The raw line
is all you need to reproduce the issue deterministically.

**Misdecodes**: same approach — the raw line is the minimal reproduction.

**Slow packets**: if consistently > 100ms, investigate the parser code path
for that packet type. Most packets should parse in under 1ms.

---

## Notes

- The harness uses passcode `-1` by default — this is read-only. No packets
  are transmitted regardless of passcode.
- Progress is reported every 30 seconds to stdout.
- A world feed (`--radius 20000`) will receive thousands of packets per minute.
  5 minutes is usually enough to get representative coverage.
- APRS-IS server: `rotate.aprs2.net:14580` (automatic failover server pool).
