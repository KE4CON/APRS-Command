# Privacy Policy

**APRS Command** is a free, open-source (GPL v3) desktop application for amateur radio. This is its
complete privacy policy. Every statement here can be verified directly against the source code at
[github.com/KE4CON/APRS-Command](https://github.com/KE4CON/APRS-Command).

*Last updated: August 5, 2026.*

## The short version

APRS Command **does not collect any data about you.** It has no backend server, no account system, and
no telemetry, analytics, or crash reporting of any kind. Nothing is sent to the developer or to any
server operated by the project. The program runs entirely on your own computer.

## What is stored, and where

- **All settings stay on your machine.** Your callsign, station location, preferences, and any API
  credentials you choose to enter are saved only in a local settings file inside your own operating-system
  user profile directory (see `JsonAppSettingsStore` in the source). They are never uploaded anywhere.
- **No usage data is gathered.** There is no tracking of how you use the app, no identifiers, no
  crash-reporting service, and no "phone home" of any kind.

## When the program uses the network

APRS Command only makes network connections that **you explicitly configure and turn on.** With none of
these enabled, the app makes no outbound connections. The possible connections are:

- **APRS-IS** — the amateur-radio APRS internet servers, if you connect to one.
- **Radio / TNC links** — local hardware or software TNCs (no internet involved).
- **Map tiles** — background map imagery is fetched from the map provider you select, for the area you
  view.
- **Optional third-party integrations** — each is opt-in and off by default (for example RepeaterBook,
  CalTopo/SARTopo, or weather alerts). When you enable one, the app contacts only that provider's own
  service, over HTTPS, for the feature you asked for. It never shares your data with any other party.

## Credentials

Any API token you enter (for example a RepeaterBook token) is stored locally in your settings file and is
sent **only** to that provider's own API. It is never logged, never included in error output, never shared
between users, and never bundled with the application. Note: credentials are currently stored as plain
text protected by your operating-system file permissions (not yet encrypted at rest) — this limitation is
documented honestly in [SECURITY.md](SECURITY.md).

## No transmission of your data by the app

Information the app fetches for display (such as repeater or gateway lookups) is shown only on your own
screen and is **never** transmitted over the air (RF) or to APRS-IS, and never redistributed to anyone
else.

## Changes to this policy

This policy is versioned with the source code. Any change to how the application handles data will be
reflected here and in [SECURITY.md](SECURITY.md), which contains the full, source-backed security and
data-handling detail.

## Contact

Questions or concerns: open an issue at
[github.com/KE4CON/APRS-Command/issues](https://github.com/KE4CON/APRS-Command/issues).
