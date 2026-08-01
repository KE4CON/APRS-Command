# Device Identification — Design Note

**Status:** **Slice 1 (offline core) built** ✅. Decisions below were made ("your call"): **hybrid
delivery, full dataset, shown in both list + detail, auto weekly refresh + manual button.**

**Done (slice 1):** `IDeviceIdentificationService` + `DeviceIdentificationService` +
`DeviceIdentity` (`src/Aprs.Services/DeviceId/`), a bundled full snapshot of `tocalls.dense.json`
(embedded resource, CC BY-SA 2.0 — see `docs/THIRD_PARTY_NOTICES.md`), tocall pattern matching with
`?` wildcards (most-specific wins), and `DeviceIdentificationServiceTests` (incl. our own `APCMD0` →
"APRS Command"). Fully offline, no UI yet.

**Done (slice 2 — surfacing):** the tocall now flows `StationSnapshot.Destination` → `StationMarker`
→ `StationMarkerViewModel` (which resolves `DeviceIdentity`/`Device` via the bundled service), into
`StationListRowViewModel` + `StationDetailsViewModel`. The station list shows a "Device: …" line,
hidden when unidentified (so MIC-E radios don't show a noisy "Unknown" until the MIC-E follow-up).
Tests: `StationDeviceIdentificationTests`. The marker VM uses a shared lazy `DeviceIdentificationService`
by default (overridable) to avoid threading the lookup through the whole VM spine.

**Remaining:** slice 3 (weekly refresh + manual "update now" — at which point consolidate the marker
VM's shared default into a single DI singleton the refresher updates); the MIC-E radio-model follow-up
(the `mice`/`micelegacy` sections, needing suffix extraction tied to the MIC-E decoder); and optionally
surfacing device on the map marker popup.

## What & why
APRS packets carry a **destination "tocall"** (e.g. `APDW17`, `APK003`, `APWW11`) that identifies the
sending **device or software** — a legacy of the protocol. The APRS Foundation maintains a
machine-readable database mapping tocalls (and MIC-E suffixes) to `{vendor, model, class, os}`.

We already parse and store each station's `Destination` (`StationDatabase`), and we already decode
MIC-E. With this database we could show, next to each station, **what it's running** — "Kenwood
TM-D710 (mobile)", "Dire Wolf 1.7 (software)", "APRSdroid (phone)", a weather-station's software, a
balloon tracker, etc. For a situational-awareness / EmComm tool that's a real upgrade — at a glance you
can tell a handheld from a software client from an unattended digipeater. It also closes the loop on
our own `APCMD0` allocation: the same DB that identifies everyone else will identify us.

## Data source
- Repo: `github.com/aprsorg/aprs-deviceid` (maintained by Hessu OH7LZB / APRS Foundation).
- Machine-readable: `https://aprs-deviceid.aprsfoundation.org/tocalls.dense.json` (+ `.pretty.json`,
  `.yaml`, `.xml`). Parse with framework `System.Text.Json` — **no new NuGet package**.
- Two match types:
  1. **Tocall prefix** — longest-prefix match (tocalls are hierarchical: `APK` → `APK003`).
  2. **MIC-E** — identified by a suffix in the comment (backtick/apostrophe + a symbol), which pairs
     with our existing MIC-E decoder.
- **License: CC BY-SA 2.0** (attribution required). Handle via the existing third-party-notices flow,
  same discipline as the other bundled assets.

## Proposed architecture (layer-clean)
- **`IDeviceIdentificationService`** in `Aprs.Services` (business logic; consumes the dataset, and — if
  we do refresh — a framework `HttpClient`). UI consumes it; nothing new leaks into Core/Transport.

```csharp
public interface IDeviceIdentificationService
{
    // destinationTocall = packet.Destination; micEInfo optional, from a decoded MIC-E packet.
    DeviceIdentity? Identify(string destinationTocall, string? micEInfo = null);
}

public sealed record DeviceIdentity(string Vendor, string Model, string DeviceClass, string? Os);
```

- Loads the dataset once at startup; builds a longest-prefix lookup for tocalls + a dictionary for
  MIC-E suffixes. Thread-safe, immutable after load.
- Surfaced at the station level: resolve on ingest (or lazily on display) and show in the station
  list / detail.

## Data delivery — the main decision
| Option | Pros | Cons |
|---|---|---|
| **A. Bundle a snapshot** (embedded resource) | Works offline out of the box (key for field/EmComm); no network dependency; simplest | Goes stale between app releases (new devices unrecognized until we ship an updated snapshot) |
| **B. Download + cache** on first run + refresh | Always current | Needs internet on first use (bad for offline field deploy); network failure handling |
| **C. Hybrid** (bundle a snapshot **and** refresh weekly, jittered, caching the latest) | Offline out of the box **and** stays current; matches our offline-map-tiles + refresh-on-demand-GPS pattern | A bit more code (bundled + cache + refresh) |

**My recommendation: C (hybrid)** — bundle a snapshot so it works with no network, and optionally
refresh weekly to stay fresh. It fits the offline-first EmComm ethos.

## Suggested phasing (so each slice is a clean PR)
1. **Slice 1 — core, no UI, no network.** `IDeviceIdentificationService` + a **bundled snapshot** of
   `tocalls.dense.json` + longest-prefix + MIC-E lookup + tests. Attribution in the notices. This is
   the meat and is fully offline.
2. **Slice 2 — surface it.** Show device/model in the station list and/or detail panel.
3. **Slice 3 — refresh (only if we chose C).** Weekly jittered refresh + cache + a manual "update
   device DB" action.

## Testing
- Lookup unit tests: longest-prefix (`APK` vs `APK003`), MIC-E suffix, unknown tocall → null, our own
  `APCMD0` resolves to APRS Command.
- Snapshot-load test (parse the bundled JSON, spot-check a few well-known tocalls).

## Open Decisions (need your call)
1. **Delivery:** A (bundle-only), B (download-only), or **C (hybrid — my rec)**? For slice 1 the
   answer only affects whether we add refresh later; slice 1 bundles either way.
2. **Dataset scope:** ship the **full** `tocalls.dense.json` (~a few hundred KB — best accuracy) or a
   **curated common subset** (smaller repo, but misses less-common devices)? I lean full.
3. **Where it shows:** station **list** (a column), station **detail** panel, or **both**?
4. **Refresh (if C):** auto weekly, or bundle-only + a manual "update now" button? (EmComm operators
   may prefer no surprise network calls.)

Tell me your answers (or just "your call") and I'll build slice 1.
