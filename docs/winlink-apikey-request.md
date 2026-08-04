# Winlink — Web Services API Key Request (draft)

**Status:** draft, ready to send from Jim's (KE4CON) account.
**Prepared:** August 2026.
**Context:** The Winlink RMS Gateways feature (`WinlinkRmsGatewayService`) queries the CMS Web
Services `/gateway/proximity` endpoint, which requires a per-application access key issued by a
Winlink administrator (free, but not self-service). Per Decisions Log **P5**, a request was already
made and is awaiting a response; this is a clean re-send that includes a full project description.
Unlike RepeaterBook, the issued key *is* the authorization — no separate terms carve-out is needed.

> Not sent yet. Fill in the actual send date / recipient before sending.

---

**Subject:** Web services API key request — APRS Command (open-source APRS client)

Hello,

I'm Jim (KE4CON), author of **APRS Command**, a free, open-source, cross-platform APRS client for situational awareness and emergency communications. I'd like to request a **per-application web services access key** so the app can display nearby Winlink RMS gateways to operators. (I believe I have a request already in — apologies if this is a duplicate; I wanted to make sure it reached you with a full description of the project.)

**What it's for:** a "Winlink RMS Gateways" panel that helps an operator see the **RMS gateways near their own station location** — for planning voice/Winlink backup paths during a field deployment or net. It uses the **`/gateway/proximity`** endpoint with the operator's own grid square.

**How it uses the API, responsibly:**

- **User-initiated only** — no automatic, timed, or background polling. A query happens only when the operator opens the panel and requests it, and requests are rate-limited on our side to stay within actual need.
- **Display-only.** Results are shown on the operator's own screen (and as a toggleable map marker) — never transmitted over RF or to APRS-IS, never redistributed, rebroadcast, cached to disk, or shared with any other user or service.
- **Per-operator key handling.** APRS Command is open source and distributed to end users, so no shared key is embedded. The key you issue is stored locally in the operator's own settings and sent only to `api.winlink.org` over HTTPS.
- **Attribution.** I'm glad to credit Winlink wherever gateway data appears, in whatever form you prefer.

The feature is built but currently **disabled**, waiting on the key before it ships.

Could you let me know what you need from me to issue a key — and whether there are any usage conditions or attribution requirements I should build in? I want to use the service the way you intend. Project details and source are available on request. Thank you for maintaining Winlink — it's an invaluable resource for the EmComm community.

73,
Jim — KE4CON
APRS Command
