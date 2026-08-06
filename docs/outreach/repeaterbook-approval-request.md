# RepeaterBook — App Approval Request (draft)

**Status:** draft, ready to send from Jim's (KE4CON) RepeaterBook account.
**Prepared:** August 2026.
**Context:** The Field Repeater Lookup feature (`RepeaterBookService` / `RepeaterDirectoryWindow`)
is built but dormant, pending app-level approval under RepeaterBook's *distributed-app* category.
An earlier request drew a vague reply; this follow-up uses a different contact channel and asks
directly for the specific requirements needed for approval. See Decisions Log **P4** and
`SECURITY.md` §2.1 for the full feature scope and safeguards.

> Not sent yet. Fill in the actual send date and any channel-specific greeting before sending.

---

**Subject:** APRS Command — following up on app approval (distributed-app category)

Hello,

I'm Jim (KE4CON), the author of **APRS Command**, a free, open-source, cross-platform APRS client for situational awareness and emergency communications. I reached out previously about approval to use the RepeaterBook API and wanted to follow up through a different channel, as I'd genuinely like to get this right on your terms.

I'll be direct about what I'm asking: **I'd like to understand the specific requirements you need met for approval.** The earlier guidance I received was fairly general, and I want to make sure I'm addressing your actual concerns rather than guessing — so if there are concrete conditions, wording, or limits you need to see, I'm glad to implement them.

To that end, here's exactly how APRS Command already uses RepeaterBook, because I've deliberately built it to be a narrow, well-behaved integration rather than a general repeater-finder:

- **One narrow purpose.** A "Field Repeater Lookup" panel that helps an operator find EmComm-affiliated (ARES/RACES/SKYWARN) and local voice repeaters near **their own configured station location** — for net coordination and backup voice during a deployment. It is not a general search tool: it never accepts an arbitrary location, and every query uses only the operator's own station coordinates.
- **User-initiated only.** No automatic, timed, or background queries — a request happens only when the operator opens the panel and clicks Search, with a hard-coded 10-second minimum between queries.
- **No shared token.** APRS Command applies under your **distributed-app category** (open source, installed by end users). No token is embedded in the app. Once the *application* is approved, **each operator generates their own personal `rbuapp_` token** from their own RepeaterBook account and pastes it into their local settings; it's sent only to your API over HTTPS and never shared or bundled.
- **Display-only — never transmitted, never redistributed.** Results are shown on the operator's own screen only, held in memory for the current session and cleared on the next search or on close — never written to disk, cached, logged, or shared with any other user or service, and **never transmitted over RF or to APRS-IS**. (APRS Command routes every outbound transmission through a central transmit-safety authority; this feature has no path to it.)
- **Attribution.** "Data courtesy of RepeaterBook.com" is shown wherever results appear, and I'm happy to adjust the exact wording or placement to your preference.

The feature is complete and currently disabled in the app, waiting on your approval before it ships — I won't enable it until I have your OK.

If it would help, I'm happy to hop on a call, share the relevant source, or make specific changes. Mainly, I'd appreciate knowing **what you'd need from me to move an approval forward.** Thank you for maintaining RepeaterBook — it's the resource the community relies on, and I'd much rather build on it, properly, than work around it.

73,
Jim — KE4CON
APRS Command
