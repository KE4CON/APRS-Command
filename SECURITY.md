# Security and Data Handling Policy

This document describes how APRS Command handles data, credentials, and
third-party API integrations. It exists so operators, code reviewers, and
third-party API providers (such as RepeaterBook) have one authoritative,
citable place to see exactly what the application does — backed directly
by the source code, not just a description.

APRS Command is fully open source (GPL v3). Every claim below can be
verified directly against the source at
https://github.com/KE4CON/APRS-Command.

---

## 1. General Posture

APRS Command is a **local desktop application with no backend server of
its own**. There is no APRS-Command-operated infrastructure, no account
system, and no telemetry or crash-reporting service of any kind. Every
installation runs independently on the operator's own machine.

- No usage analytics, telemetry, or crash reporting is collected or
  transmitted anywhere.
- No data is sent to the project maintainer or to any server controlled
  by the APRS Command project.
- All settings, including any API credentials an operator enters, are
  stored **only** in a local settings file inside that operator's own OS
  user profile directory (see `JsonAppSettingsStore`), and never leave
  that machine except in the specific, deliberate network requests
  described below.
- Network activity is limited to the services the operator explicitly
  configures and enables (APRS-IS, RF/TNC connections, and optional
  integrations such as RepeaterBook, CalTopo/SARTopo, and NWS alerts).

## 2. Third-Party API Integrations

A small number of optional features call third-party APIs. Each is
opt-in, off by default, and used only for the specific purpose described
below.

### 2.1 RepeaterBook (Repeater Directory panel)

- **Purpose:** lets an operator look up repeaters near their own station
  location from within the app. This is the only use of RepeaterBook
  data anywhere in the application.
- **Trigger:** strictly user-initiated. The app never queries
  RepeaterBook automatically, on a timer, or in the background. A query
  only happens when the operator opens the panel and clicks Search.
- **Rate limiting:** a hard-coded minimum 10-second interval is enforced
  between queries client-side (`RepeaterBookService.MinQueryIntervalSeconds`),
  independent of user action.
- **Data storage:** query results are held in memory only, for the
  current session, to populate the on-screen list. Results are never
  written to disk, cached, or logged, and are cleared on the next search
  or app close.
- **Redistribution:** results are displayed only to the operator who
  requested them, on their own screen. APRS Command never redistributes,
  rebroadcasts, republishes, or shares RepeaterBook data with any other
  user or service.
- **Token storage:** each operator supplies and stores their own personal
  RepeaterBook API token (Settings → Connections → RepeaterBook API
  Token). The token is saved locally in the operator's own settings file
  and transmitted only to RepeaterBook's own API endpoint, over HTTPS,
  via the `X-RB-App-Token` header. It is never logged, never included in
  any error output, and never shared between users or bundled with the
  application.
- **Source:** `src/Aprs.Desktop/Services/RepeaterBookService.cs`,
  `src/Aprs.Desktop/ViewModels/RepeaterDirectoryViewModel.cs`,
  `src/Aprs.Desktop/Configuration/RepeaterBookSettings.cs`.

### 2.2 CalTopo / SARTopo (live position forwarding)

- **Purpose:** optionally forwards received station positions to a
  CalTopo/SARTopo map for SAR coordinators, using CalTopo's public
  position-reporting endpoint.
- **Credentials:** this integration does not use an API key or account
  authentication — only a Map ID the operator copies from their own
  CalTopo map, which is not a secret credential.
- **Trigger and rate limiting:** forwarding only occurs while explicitly
  enabled, with a configurable minimum interval between updates (default
  60 seconds).
- **Source:** `src/Aprs.Desktop/Configuration/CalTopoSettings.cs`.

### 2.3 Future integrations

Any future third-party API integration will follow the same pattern
described in this document: opt-in, user-initiated or clearly rate
limited, no server-side persistence, no redistribution, and credentials
stored only in the operator's own local settings file. This document will
be updated alongside any new integration.

## 3. Known Limitation: Local Credential Storage

API tokens (currently just the RepeaterBook token) are stored in
`settings.json` as plain text, protected only by normal operating-system
file and user-account permissions — they are **not** encrypted at rest
(e.g. no Windows DPAPI, macOS Keychain, or Linux secret-service
integration yet). This is an accurate, deliberately honest limitation
rather than a claim of stronger protection than actually exists.
Encrypting locally stored credentials at rest is a tracked improvement;
see the project's GitHub Issues for current status before relying on this
document being fully up to date on that point.

## 4. Reporting a Vulnerability

APRS Command is maintained by a single volunteer developer. If you find a
security issue, please open a GitHub issue at
https://github.com/KE4CON/APRS-Command/issues, or, for anything sensitive
enough that you'd rather not post publicly first, use GitHub's private
vulnerability reporting feature on the repository (Security tab →
"Report a vulnerability") so it can be addressed before public
disclosure. There is no dedicated security contact email at this time.

---

*Last updated: July 27, 2026 · Applies to v0.5.0-alpha*
