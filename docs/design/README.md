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

## Decisions locked in

- Navigation: left icon rail; map-first; collapsible/dockable panels.
- Themes: Light (default), Dark, and High contrast — all user-selectable, none forced.
- Device priority: laptop first, then desktop, then Raspberry Pi (touch pass last).
- All "smaller touches" in scope (map layer toggles, search-and-center, follow-me,
  right-click context menus, command palette); the map-dependent ones follow the
  Mapsui map work.
