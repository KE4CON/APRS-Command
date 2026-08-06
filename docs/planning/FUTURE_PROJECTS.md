# APRS Command — Future / Separate Project Ideas

A parking lot for ideas that are **out of scope for the current desktop application** and, if pursued,
would be built as **their own separate projects** — not folded into this codebase. Recording them here
keeps the analysis from being lost without expanding the desktop app's scope.

> Nothing in this document is scheduled or committed. These are deliberately deferred until the v1.0
> desktop application is locked and proven.

---

## 1. Mobile companion app (phone / tablet)

**Status:** idea only — not scheduled. **Would be a separate project** with its own codebase, repo, and
release cycle, distinct from the APRS Command desktop application.

### Why it must be separate
The desktop app is built around a desktop paradigm — a menu bar, multiple floating windows/panels,
hover tooltips, right-click, and mouse-sized targets. None of that maps to a phone. A mobile version
needs a ground-up, touch-first UI and a different hardware model, so it is a new project that *shares
ideas* with the desktop app rather than sharing its code. (This mirrors how IcomRigControl and
FieldCommand IMS are kept as separate programs.)

### Two possible approaches

**A. Responsive web companion / PWA (lowest effort).**
Grow the existing **Mobile Companion Web View** (`Aprs.Desktop/Services/MobileCompanionServer.cs`) into
a polished, responsive web app or installable PWA.
- Reuses the server already in the desktop app; runs in any phone/tablet browser (iOS and Android alike);
  no app stores.
- The phone is APRS-IS-only *by itself*, but it views the **desktop's** feed over the LAN — so it sees
  everything the desktop hears, **RF included** (the desktop is the radio; the phone is the display).
- Best fit for "let me see the operating picture on my phone while the desktop runs the station."

**B. Native mobile app (Avalonia Android / iOS).**
Avalonia 11 has Android and iOS targets and Mapsui runs on mobile, so a native app is technically
feasible — but it is a multi-phase effort:
- **UI:** a complete mobile-first redesign (single-window navigation, touch targets, gestures).
- **RF hardware — the real constraint:** USB-serial TNCs do **not** travel to mobile (`System.IO.Ports`
  is unavailable on iOS and unusable on Android as-is). The only realistic RF path is a **Bluetooth/BLE
  KISS TNC** (e.g. Mobilinkd); on iOS, BLE is essentially the *only* radio option. Default assumption
  for a native mobile build is therefore **APRS-IS-only, with BLE TNC as an optional RF add-on.**
- **Platform deps:** desktop/Linux-only dependencies (`System.Device.Gpio`, `Tmds.DBus.Protocol`,
  `System.IO.Ports`) would be conditionally excluded or replaced per target.
- **Distribution:** iOS via the App Store / TestFlight (the project now has an Apple Developer account)
  with AOT compilation; Android via Play Store or sideloaded APK.

### Competitive context (why it matters)
Against the established desktop clients, APRS Command is already competitive on features and ahead on
modern UX, EmComm/training tooling, transmit-safety, and extensibility. Its two real gaps are
**maturity** (time-in-service) and **native mobile** — the latter is exactly what this project would
close. The incumbent to study is **APRSdroid** (open-source Android; APRS-IS + Bluetooth/USB TNC). See
the appendix for the full comparison.

### Decision
Revisit after the v1.0 desktop app is locked. If pursued, **start with Approach A** (web companion /
PWA) — it delivers mobile viewing for the least effort and sidesteps the iOS-hardware problem — and
treat a full native app (Approach B) as a later, separate undertaking.

---

## Appendix — APRS client feature comparison (2026)

How the APRS Command **desktop** app compares to the other widely used clients. Factual snapshot for
positioning; not marketing copy.

| Capability | APRS Command | Xastir | YAAC | APRSISCE/32 | PinPoint | APRSdroid |
|---|---|---|---|---|---|---|
| Platforms | Win/Mac/Linux/Pi | Linux/Unix (Win via Cygwin) | Win/Mac/Linux (Java) | Windows | Windows | Android |
| Open source | GPLv3 | GPL | free / source | freeware | freeware | GPL |
| Core APRS (map, APRS-IS, RF/TNC, messaging, objects, telemetry, GPS) | Yes | Yes | Yes | Yes | Yes | Yes (lighter) |
| iGate + Digipeater | Yes | Yes | Yes | iGate | No | No |
| Offline maps | Yes | Yes | Yes | Yes | Yes | Limited |
| Weather-station hardware ingest (Tempest, Davis, Ecowitt, WeeWX…) | Deep | Partial | Partial | No | No | No |
| EmComm / net tooling (Net Control, roster, after-action, geofence alerts) | Yes (rare) | No | No | No | No | No |
| Replay / Simulation / Training (transmit-safe) | Yes (rare) | Minimal | No | No | No | No |
| Extensibility (REST API, WebSocket, plugins, file hooks) | Yes | No | Plugins | No | No | No |
| Planning tools (PHG coverage, elevation, frequency reference) | Yes | Partial | Partial | No | No | No |
| Modern UI + dark mode | Yes | Dated | Dated | Dated | Somewhat | Yes (mobile) |
| Transmit-safety authority (receive-first, global inhibit, exercise mode) | Yes (distinctive) | No | No | No | No | No |
| Native mobile | No (web companion) | No | No | Legacy WinMobile | No | Yes |
| Maturity / field-tested | Alpha | Decades | Years | Years | Yes | Yes |

**Where APRS Command stands out:** the only modern, cross-platform, open-source client with a polished
UI; EmComm/exercise tooling (Net Control, after-action, geofence alerts, Replay/Simulation/Training with
a centralized transmit-safety authority) that virtually no other client ships; deep weather-station
hardware integration; and a genuine extension platform (API/WebSocket/plugins/file hooks — only YAAC has
a comparable plugin story).

**Where the others still lead:** maturity and field-testing (Xastir, YAAC, APRSISCE/32 have years to
decades of use and large user bases; APRS Command is alpha), native mobile (APRSdroid), and established
community/ecosystem.

**Sources:** [DXZone APRS software catalog](https://www.dxzone.com/catalog/Software/APRS/) ·
[KI4HDU — APRS client options](https://ki4hdu.com/amateur-radio/packet/aprs-client-options/) ·
[YAAC (OpenStreetMap wiki)](https://wiki.openstreetmap.org/wiki/YAAC)
