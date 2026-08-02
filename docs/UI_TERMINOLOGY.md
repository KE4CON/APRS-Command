# UI Terminology — Canonical Reference

**Status:** Locked vocabulary. These are the authoritative names for the regions of the
APRS Command interface. Use these exact terms in the **User Manual**, **Quick Start**, the
**Programming Manual**, tooltips, and any release notes. Do not invent synonyms.

The names below are taken directly from the application layout (`MainWindow.axaml`,
`MapView.axaml`, `MainStatusBarView.axaml`), so the words in the docs match the words in the
code.

See the labeled diagram: [`home_page_regions.svg`](home_page_regions.svg).

---

## The "header" trap (read this first)

There is **no region called a "header"** in this app. The word is banned as a region name to
avoid the exact mix-up we keep hitting:

- The dark strip across the very top of the window is the **title bar** — *not* the "header."
- Each floating panel/window (Station List, Messages, etc.) also has a **title bar** — the
  colored strip carrying the grip dots and the ✕ close control.
- The only thing that earns the word "header" is a **column header** — the sort/label strip
  at the top of a *list* (e.g. the blue Callsign / Last Heard strip in the Station List).

If you're about to write "header," stop and decide: is it a **title bar** (top of a window/panel)
or a **column header** (top of a list)?

---

## Page name

The main map screen is the **home page**. (Informally "the map page" is acceptable in prose,
but **home page** is the preferred term and should be used in headings and step-by-step
instructions.)

---

## Home page regions

| # | Canonical name | What it is | Visibility |
|---|----------------|-----------|------------|
| 1 | **Title bar** | Dark strip at the very top: app name "APRS Command" + the "APRS station map" subtitle. | Always |
| 2 | **Status badges** | Two badges at the right end of the title bar: the **APRS-IS badge** and the **TX badge**. | Always |
| 3 | **Menu bar** | The text menus: Settings · View · Map · Messages · Operate · Tools · Help. | Always |
| 4 | **Weather-alert banner** | Full-width banner announcing an active NWS alert; click to open details. | Only when an NWS alert is active |
| 5 | **Draw-mode banner** | Full-width banner shown while a drawing tool (line/polygon/circle/erase) is active, with an Exit control. | Only while a draw tool is active |
| 6 | **Icon sidebar** | The vertical toolbar of icon buttons down the left edge. | Always |
| 7 | **Map area** | The map itself — the main canvas that fills the rest of the window. | Always |
| 8 | **Base-map selector** | On-map dropdown, top-left of the map: OpenStreetMap / USGS Topo / USGS Imagery / USGS Imagery + Topo. | Always |
| 9 | **Station-details panel** | On-map panel, bottom-left; shows the selected station's details. | Only when a station marker is selected |
| 10 | **Radar scrubber** | On-map control, bottom-center; step/play through radar frames. | Only when weather radar is on and frames are loaded |
| 11 | **Object-placement overlay** | On-map panel, top-right; guides placing/moving an object. | Only while placing an object |
| 12 | **Status bar** | The strip along the very bottom: *Ready* · *APRS-IS Disconnected/Connected* · *RF TX Disabled/Enabled*. | Always |

---

## Icon sidebar buttons (region 6)

The sidebar buttons have **no printed labels in the app — only tooltips.** These are their
canonical names (top to bottom). A divider separates the map tools from the operator tools.

**Map tools**
1. **Home** (⌂) — Home / reset the map view.
2. **Center on my station** (◎) — recenter the map on your own station.
3. **Find station** (🔍) — search for a station and jump to it.
4. **Measure distance** (📏) — measure distance between points on the map.
5. **Map layer** (🗺) — cycle the base-map layer.

**Operator tools**
6. **Beacon Now** (📡) — transmit your position immediately.
7. **Alerts** (🔔) — alert status.
8. **Range rings** (⊙) — toggle distance range rings.
9. **Trails** (🛤) — toggle station movement trails.
10. **Radar** (🌧) — toggle the weather-radar overlay.

> When naming these in prose, use the name then the tooltip if helpful, e.g.
> *"the **Beacon Now** button (📡) in the icon sidebar."* Do not refer to them by emoji alone.

---

## Floating panels / windows

Every feature that opens in its own window (Station List, Messages, Objects, Settings, etc.) is
a **floating panel**. Its parts:

- **Panel title bar** — the colored strip at the top with the **grip** (the row of dots you drag
  to move the panel) and the **✕ close control**.
- **Panel body** — everything below the panel title bar.
- Inside a list, the top label/sort strip is a **column header** (see the "header" trap above).

---

## Quick do/don't

| Use this | Not this |
|----------|----------|
| title bar | header, top bar, banner (for #1) |
| status badges | status lights, indicators |
| menu bar | menu strip, ribbon |
| icon sidebar | toolbar, side panel, dock |
| map area | map pane, viewport, canvas (in prose) |
| status bar | footer, bottom bar |
| floating panel | window, dialog, box |
| panel title bar | header, caption bar |
| column header | header row, sort bar |
| home page | map page (ok informally), main screen |
