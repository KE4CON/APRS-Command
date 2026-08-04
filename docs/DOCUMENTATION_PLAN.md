# APRS-Command — Documentation Initiative Plan

> **Authoritative plan-of-record for the two big documentation deliverables.**
> Started 2026-08. Read this first before working on either document so the intent,
> depth, format, and maintenance model are not forgotten between sessions.

---

## Why this exists

Two documents are being produced to a very high standard so the project is usable by
everyday operators *and* maintainable by future contributors long after the original
author (Jim, KE4CON) has moved on. Both must look **as professional and polished as the
program itself** — the explicit bar is: *people should say "wow" to the documentation.*

There are **two deliverables**:

1. **APRS Command — User Manual** (for operators)
2. **APRS-Command Programming Guide Book** (for developers / posterity)

---

## Deliverable 1 — User Manual

**Audience:** operators using the app. Assume a ham who will *not* read source code and
*will not* read a terse reference — it must be self-explanatory.

**Standard (non-negotiable):** *painstakingly detailed, step-by-step, covering every
feature.* A reader must **never** say "what are they talking about." Plain language,
every button/window/field explained, every screen walked through.

**Authoring format:** **Microsoft Word (`.docx`)** — so Jim can drop **screenshot images**
directly into the flow. Screenshot locations are marked with dashed **`SCREENSHOT`
placeholder boxes** carrying a caption describing exactly what to capture.

**Distribution format:** after screenshots are added and the manual is complete, **convert
to PDF** for distribution.

**Look & feel (already prototyped in the Replay sample):**
- Designed title block per chapter (chapter kicker + large title + subtitle + accent rule)
- "In this chapter" mini-contents
- Custom heading styles (deep-blue `#1F4E79` H1, medium-blue `#2E6DA4` H2, Segoe UI Semibold)
- Colored callout boxes: **Important** (amber), **Note** (blue), **Tip** (green) — shaded
  fill + accent left border
- Numbered steps and bulleted lists with hanging indents
- Header (doc title, rule) + footer (chapter + page number)
- Dashed **SCREENSHOT** placeholder frames

**Coverage:** every feature — roughly ~40 chapters/sections. Build one chapter at a time.
**Replay** was written first as the tone/depth/format sample for approval before scaling.

**Sample built:** `USER_MANUAL_Replay_sample.docx` (Replay chapter) — sent for review.

---

## Deliverable 2 — APRS-Command Programming Guide Book

**Title (use exactly):** **APRS-Command Programming Guide Book**

**Audience:** developers, future maintainers, and the curious. The goal is a *complete
picture of how and why the code was written* — a record so the project can **live on long
after the author is gone.**

**Standard:** the **who, what, why, and how of every section of the code** — *why it was
written that way, what it does, why that approach was chosen.* Think **PhD thesis** in
thoroughness, but **kept in terms an everyday person can understand.**

**Formats (produce all three):** **Markdown + PDF + Word.**
- **Markdown** is the **living source of truth**, version-controlled in the repo alongside
  the code.
- **PDF** and **Word** are generated from the Markdown for reading/printing/distribution.

### Amendment / supplement model (applies to BOTH documents)

Purpose: allow future improvements **without reprinting the whole book** ("we won't kill
so many trees"). Design:

- **Stable, fixed section numbering.** Section numbers never get renumbered once assigned;
  new material is *added*, not inserted in a way that shifts existing numbers.
- **Standalone, dated amendments.** Each future change ships as its own short, dated,
  printable amendment document — tagged:
  - **`AMENDS §X.Y`** — revises existing section X.Y
  - **`ADDS §Z`** — adds a new section Z
- **Amendments Register** — a table in the core book listing every amendment (date, tag,
  summary) so a reader knows the book's current state and what to append.
- **"How this book is maintained"** section — explains the numbering + amendment discipline
  so future contributors follow it.

---

## Presentation standard (both documents)

Professional, modern, "wow." Consistent palette and typography (see the Replay sample for
the concrete style). Not academic-drab; polished like the app's UI (soft depth, strong
accent color, clear hierarchy).

---

## Status / progress tracker

| Item | Status |
|---|---|
| User Manual — Replay sample chapter (Word) | **Done — sent for review** |
| User Manual — tone/depth/format approval | Awaiting Jim's feedback |
| User Manual — remaining ~40 feature chapters | Not started (blocked on approval) |
| User Manual — PDF conversion | Later (after screenshots added) |
| Programming Guide — outline & section numbering scheme | Not started |
| Programming Guide — core chapters (Markdown) | Not started |
| Programming Guide — PDF + Word generation pipeline | Not started |
| Amendment model — templates (register + amendment doc) | Not started |

## Build environment notes (this Windows machine)

- **No Node.js on PATH** — docx-js is not usable here. The Word documents are generated
  with **Python + `python-docx`** (installed: `python-docx 1.2.0`, Python 3.14).
  Generator script for the sample: kept in the session scratchpad (`gen_replay.py`).
- **No LibreOffice/Word CLI** for headless PDF render — PDF conversion of the finished
  Word manual will be done by Jim (open in Word → Save As PDF), or via a tool when available.
- Markdown → PDF/Word for the Programming Guide: decide toolchain when starting it
  (pandoc if available; otherwise a python-docx generator mirroring the manual's style).
