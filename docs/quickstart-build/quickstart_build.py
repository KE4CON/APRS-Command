"""
APRS Command — Quick Start Guide builder.

A short, fast-path Word document (navy+gold, matching the User Manual and Programming Guide) for people
who won't read the full manual. Covers only what's needed to get running: install, first-run setup,
confirm receive-safe, connect a feed, watch the map. Screenshot placeholders included so images can be
inserted by hand, then exported to PDF.

Run:  python quickstart_build.py     ->  writes ../QUICK_START_GUIDE.docx
Env:  QUICKSTART_OUT overrides the output path (when Word has the file locked).
"""
import os, sys, datetime

HERE = os.path.dirname(__file__)
sys.path.insert(0, os.path.join(HERE, "..", "manual-build"))
import style as S  # noqa: E402
from docx.shared import Inches, Pt  # noqa: E402


def build():
    doc = S.new_document(
        header_title="APRS Command — Quick Start Guide",
        header_sub="The 10-minute path  ·  Receive-first, transmit-safe",
        footer_left="APRS Command Quick Start  ·  Open-source (GPL v3)  ·  73 de KE4CON",
    )

    S.cover(
        doc,
        kicker="APRS COMMAND  ·  QUICK START",
        big_title="APRS COMMAND",
        subtitle="Quick Start Guide",
        doc_kind="ON THE AIR IN 10 MINUTES",
        version="v1.0",
        tagline="Install  ·  Set Up  ·  Watch the Map — the fast path",
        author="James Rospopo  ·  KE4CON",
        date_str=datetime.date.today().strftime("%B %d, %Y"),
    )

    # ── Read this first ───────────────────────────────────────────────────────
    S.h1(doc, "Read This First (Really — It's 60 Seconds)")
    S.body(doc, "This guide gets you from download to a live map of nearby ham radio activity as fast as "
        "possible. It skips the details on purpose — the full User Manual has everything else. Five short "
        "steps and you're running.")
    S.callout(doc, "important", "You will NOT transmit anything.",
        "APRS Command starts in listen-only mode. The title bar shows a TX Disabled badge, and nothing "
        "you do in this guide puts a signal on the air. So relax and click around — you can't key up the "
        "radio by accident.")
    S.callout(doc, "tip", "Use a practice callsign while you learn.",
        "Type N0CALL wherever a callsign is asked for. It's the standard \"not a real station\" callsign "
        "for testing, and it lets you explore everything without pretending to be a licensed station. "
        "Swap in your real callsign only when you're ready to actually operate.")

    # ── Step 1 — Install ──────────────────────────────────────────────────────
    S.h1(doc, "Step 1 — Install It")
    S.steps(doc, [
        ["Go to the official downloads page: ", ("github.com/KE4CON/APRS-Command/releases", 'b'), "."],
        ["Grab the file for your computer and run it:"],
    ])
    S.bullets(doc, [
        [("Windows:", 'b'), " the ", ("…-windows-x64-Setup.exe", 'c'), " installer — double-click it."],
        [("Mac:", 'b'), " the ", ("…-macos-….dmg", 'c'), " — open it and drag ", ("APRS Command", 'b'),
         " into Applications."],
        [("Linux / Raspberry Pi:", 'b'), " the ", (".deb", 'c'), " or ", (".rpm", 'c'), " package."],
    ])
    S.callout(doc, "note", "If you see a security warning, that's normal.",
        "Depending on the build, Windows or macOS may show a one-time \"are you sure?\" caution the first "
        "time. It's safe — just click through it. (The User Manual's Installing chapter explains exactly "
        "what to click for each.)")
    S.screenshot(doc, "The GitHub releases page with the Windows, macOS, and Linux download files")

    # ── Step 2 — First-run setup ──────────────────────────────────────────────
    S.h1(doc, "Step 2 — Fill In the Welcome Screen")
    S.body(doc, "The first time it opens, a small setup window appears. Fill in five things — that's all "
        "it wants:")
    S.steps(doc, [
        ["Callsign: type ", ("N0CALL", 'c'), " (your practice callsign)."],
        ["Latitude and Longitude: your approximate location in decimal degrees (e.g. ", ("42.33", 'c'),
         " and ", ("-88.45", 'c'), "). A quick web search of your town plus \"lat long\" works fine."],
        ["Distance units: pick ", ("Miles", 'b'), " or ", ("Kilometers", 'b'), "."],
        ["Receive filter radius: how far out to pull in activity — ", ("125", 'c'), " is a good start."],
        ["Click ", ("Save and continue", 'b'), "."],
    ])
    S.callout(doc, "tip", "None of this is permanent.",
        "You can change any of it later under Settings → Station. Get it roughly right and move on.")
    S.screenshot(doc, "The First-Run Setup window with Callsign, Latitude, Longitude, units, and radius filled in")

    # ── Step 3 — You're on the map ────────────────────────────────────────────
    S.h1(doc, "Step 3 — Meet the Map (and the Safety Badge)")
    S.body(doc, "You land on the map — the main screen. Glance at the top-right corner and you'll see two "
        "small badges:")
    S.bullets(doc, [
        [("TX Disabled", 'b'), " — your proof that nothing is transmitting. Good. Leave it alone."],
        [("APRS-IS Offline", 'b'), " — you're not connected to a data feed yet. That's the next step, and "
         "it's why the map is empty right now."],
    ])
    S.screenshot(doc, "The map page just after setup: title bar with the APRS-IS Offline and TX Disabled badges, empty map")

    # ── Step 4 — Get stations ─────────────────────────────────────────────────
    S.h1(doc, "Step 4 — Fill the Map With Real Activity")
    S.body(doc, "An empty map is just a map with no data yet. The fastest way to see real, live stations is "
        "to connect to APRS-IS — the internet side of the APRS network. No radio required.")
    S.steps(doc, [
        ["On the menu bar, click ", ("Settings", 'b'), ", then the ", ("Connections", 'b'), " tab."],
        ["From the type dropdown, choose ", ("APRS-IS", 'b'), " and click ", ("Add", 'b'), "."],
        ["Fill in the boxes on the right:"],
    ])
    S.bullets(doc, [
        [("Server:", 'b'), " ", ("rotate.aprs2.net", 'c')],
        [("Port:", 'b'), " ", ("14580", 'c')],
        [("Passcode:", 'b'), " ", ("-1", 'c'), "  (this means receive-only — no transmitting)"],
        [("Filter:", 'b'), " leave blank (it uses your location automatically)"],
    ])
    S.steps(doc, [
        ["Make sure the port's ", ("Enabled", 'b'), " box is checked, then click ", ("Save", 'b'), "."],
    ])
    S.body(doc, "Within a few seconds the badge flips to APRS-IS Online and stations start appearing on the "
        "map. That's it — you're live.")
    S.callout(doc, "important", "The -1 passcode keeps you receive-only.",
        "With the passcode set to -1, you're pulling in a full live feed but sending nothing. A real "
        "passcode is only needed if you ever choose to transmit — not now.")
    S.screenshot(doc, "The Connections tab with an APRS-IS port set to rotate.aprs2.net / 14580 / passcode -1, and stations populating the map")

    # ── Step 5 — Look around ──────────────────────────────────────────────────
    S.h1(doc, "Step 5 — Drive the Map")
    S.body(doc, "You're running. Here's everything you need to get around:")
    S.bullets(doc, [
        [("Move:", 'b'), " click and drag the map."],
        [("Zoom:", 'b'), " roll the mouse wheel — it zooms toward the pointer."],
        [("Who's that? ", 'b'), " click any station marker to see its details in the bottom-left."],
        [("Change the map:", 'b'), " use the ", ("Base map", 'b'), " dropdown in the top-left corner "
         "(streets, topo, or aerial)."],
        [("Home button:", 'b'), " the ", ("⌂", 'c'), " icon on the left edge snaps back to your area if you "
         "get lost."],
    ])

    # ── What next ─────────────────────────────────────────────────────────────
    S.h1(doc, "That's It — Now What?")
    S.body(doc, "You've got a live APRS map in about ten minutes. Everything below is optional and fully "
        "explained in the User Manual whenever you want it:")
    S.bullets(doc, [
        [("Messages, weather radar, and the station list", 'b'), " — under the View, Map, and Messages menus."],
        [("Draw on the map", 'b'), " (search areas, coverage, notes) — Map → Drawing Toolbar."],
        [("Practice safely", 'b'), " with Replay, Simulation, and Training — none of it touches the air."],
        [("When you're ready to transmit", 'b'), " — set up your real callsign and turn on transmit, "
         "deliberately, in Settings. The manual walks you through it."],
    ])
    S.callout(doc, "tip", "Want the whole picture?",
        "The full User Manual covers every feature, step by step, in the same plain language. This guide "
        "was just the fast lane.")
    S.body(doc, ["Welcome aboard, and ", ("73", 'b'), " (that's ham for \"best regards\")."])

    out = os.environ.get("QUICKSTART_OUT") or os.path.join(HERE, "..", "QUICK_START_GUIDE.docx")
    out = os.path.abspath(out)
    doc.save(out)
    print("OK — wrote", out)


if __name__ == "__main__":
    build()
