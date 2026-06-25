# Design & planning docs

Planning and design material for the modernization effort. These describe intended
direction; the guides in the parent `docs/` folder document current features.

- `DESIGN_PROPOSAL.md` — overall design: the bootstrap/composition-root fix, the
  command bar, the real map, persistence, the plugin host, and other improvements.
- `UIVIEW_FEATURE_PARITY.md` — every UI-View feature mapped to current repo status,
  with the modern C# approach for each gap.
- `UI_DESIGN_PROPOSAL.pdf` — proposed cross-platform layout (left rail, map-first,
  collapsible panels, themes) with reasoning. Annotatable.
- `LIVE_BOOTSTRAP_NOTES.md` — what the `feature/live-bootstrap` work changed and how
  to build/verify it.
- `SETTINGS_MENU_SPEC.md` — structure of the Settings panel: every section, the
  owned-vs-delegated (GrayWolf) split, and the four connection types (Managed
  local modem / Network TNC / AGWPE / Serial KISS) plus APRS-IS, with
  plain-language UI helper text.

## Decisions locked in

- Navigation: left icon rail; map-first; collapsible/dockable panels.
- Themes: Light (default), Dark, and High contrast — all user-selectable, none forced.
- Device priority: laptop first, then desktop, then Raspberry Pi (touch pass last).
- All "smaller touches" in scope (map layer toggles, search-and-center, follow-me,
  right-click context menus, command palette); the map-dependent ones follow the
  Mapsui map work.
- Connections: four types — Managed local modem (sound card), Network TNC
  (KISS-TCP), AGWPE, and Serial KISS — plus APRS-IS. DigiRig and SignaLink are
  sound-card/PTT interfaces, not TNCs; their audio reaches APRS Command through a
  Direwolf/GrayWolf modem, either one you run (Network TNC) or one the Managed
  local modem mode runs for you. Managed local modem is a committed feature (lets
  the operator pick the USB audio input/output device + PTT in-app), kept for
  familiarity with YAAC/AGWPE/SoundModem workflows.
- Connection model: a **list of typed ports** (not a single connection), each with a
  name, type, settings, and its own enable/receive/transmit switches — matching real
  use (RF + APRS-IS at once) and the existing multi-port engine. Defaults to one
  receive-only APRS-IS port for a simple first run. Changes only the
  `ConnectionSettings` record; the persistence store is unaffected.
