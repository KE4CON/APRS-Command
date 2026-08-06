# Capturing Screenshots for the User Manual

A practical, follow-along guide for taking the manual's screenshots so they are **clean** (a
factory-fresh app, no personal data) and **consistent** (all images look the same). Capture on
**Windows**, which has the screen-capture software.

---

## ⚠️ Read this first — the one rule that protects your work

Once you start inserting images into `docs/published/USER_MANUAL.docx`, **do NOT regenerate the manual again.**
Re-running `docs/manual-build/build.py` overwrites `USER_MANUAL.docx` from scratch and would **erase
every screenshot you placed.** The workflow is:

1. Finish all the *text* (done — 40 chapters).
2. Insert screenshots **by hand** into `USER_MANUAL.docx` — this is the last step.
3. From then on, edit in Word directly and ship future changes as dated amendment supplements.

If you ever DO need a text change after images are in, tell the assistant first — there's a safe way to
do it that doesn't lose your images.

---

## Part 1 — Reset the app to factory-fresh (Windows)

Everything APRS Command remembers lives in one folder:

```
C:\Users\<you>\AppData\Roaming\APRS Command
```

(That's `%APPDATA%\APRS Command`. Inside it: `config\settings.json`, `map-cache\`, `logs\`, `exports\`,
and so on.) There is no in-app "reset" button — you reset by moving that folder aside. **Back it up
rather than delete it**, so you can restore your real setup afterward.

### To reset

1. **Close APRS Command completely.**
2. Open **PowerShell** and run:
   ```powershell
   Rename-Item "$env:APPDATA\APRS Command" "APRS Command.backup"
   ```
3. **Launch APRS Command.** It comes up completely factory-fresh — the **First-Run Setup** window
   appears, the map is empty, and every setting is at its default. This is your clean baseline.

### To restore your real setup when you're done shooting

1. **Close APRS Command.**
2. Run:
   ```powershell
   Remove-Item "$env:APPDATA\APRS Command" -Recurse -Force
   Rename-Item "$env:APPDATA\APRS Command.backup" "APRS Command"
   ```

### Optional — reset settings only, keep downloaded map tiles

If you'd rather not re-download map tiles, delete just the settings file instead of the whole folder
(with the app closed):

```powershell
Remove-Item "$env:APPDATA\APRS Command\config\settings.json"
```

This still triggers First-Run Setup, but keeps your cached map tiles. For the most pristine, uniform
shots, the full-folder reset above is cleanest.

---

## Part 2 — Why Windows is fine for every OS

The app draws its own interface (Avalonia + a bundled font) with **in-window menus** and
**custom-chromed floating panels** — so the menu bar, the title bar with its status badges, the icon
sidebar, the map area, and every floating panel look **essentially identical on Windows, macOS, and
Linux.** Capturing everything on Windows gives you a consistent set that represents the app for users on
any operating system.

**The only things that differ across operating systems:**

1. **The outer window frame** — the minimize / maximize / close buttons and the border (Windows buttons
   vs. macOS "traffic lights" vs. Linux). This is just the outer edge of a full-window shot.
2. **Native file dialogs** — the Save / Open pickers (exporting GPX/KML, saving after-action reports)
   use each operating system's own dialog.

Everything *inside* the app window is the same everywhere, so those two exceptions are the only OS-specific
bits, and the manual already covers the install steps in text.

---

## Part 3 — Settings for consistent images

Do these once before you start, so every screenshot is uniform:

- **Display scaling = 100%.** Windows **Settings → System → Display → Scale** set to **100%**. This keeps
  every image the same crispness and size. (If your screen is very high-resolution and 100% is tiny, pick
  one scale and use it for *all* shots — consistency matters more than the exact value.)
- **Fixed window size.** Resize the APRS Command window once to a comfortable size and **don't change it**
  between shots, so images line up in the manual.
- **Use the practice callsign `N0CALL`** during first-run setup, and a tidy example location, so no real
  personal data appears in the images.
- **Crop the OS window frame** out of full-window shots if you want them to look operating-system-neutral
  (capture just the app's client area). Or keep the Windows frame on every shot — either way, be
  consistent.
- **Same base map** for map shots unless a caption calls for a specific one.

---

## Part 4 — The capture workflow

1. **Reset to factory-fresh** (Part 1) and set your capture settings (Part 3).
2. Open `USER_MANUAL.docx` and find the **screenshot placeholders** — the dashed boxes labeled
   "SCREENSHOT" with an italic caption describing exactly what the image should show.
3. For each placeholder, **set the app up to match the caption**, take the shot, and **replace the
   placeholder box** with your image (delete the placeholder, insert the picture in its place).
4. Because the boxes already sit where images belong, this doubles as a **review pass** — as you match
   each caption, you're also checking that the surrounding text is accurate against the real app.
5. When every placeholder is filled, **export the final PDF** from Word (File → Export → Create
   PDF/XPS) — that's the distributable manual.
6. **Restore your real setup** (Part 1).

---

*Remember the one rule at the top: once images are in, don't regenerate the .docx. Edit in Word and use
dated amendments from there.*
