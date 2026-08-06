# APRS-Command Programming Guide Book

*How and why the code works — a maintainer's field guide, in plain language.*

*Generated August 06, 2026 · Markdown is the living source of truth.*


---


# 1. What APRS Command Is, and How to Read This Book

*A plain-language orientation to the program, the parts it is built from, and the stable-numbering, dated-amendment discipline that keeps this book alive as the code grows.*


## What This Is / What It Is For

*APRS Command* is a desktop program for amateur ("ham") radio operators. It shows a live map of nearby radio stations, lets you send and receive short digital messages and position reports, and turns a scatter of radio traffic into a single shared picture of what is happening in an area — who is where, what the weather is doing, where the resources and hazards are. It runs from one codebase on Windows, macOS, Linux, and the Raspberry Pi, and it is written in *C#* on *.NET 10* with the *Avalonia* user-interface framework.

The name for that shared picture is *APRS* — the Automatic Packet Reporting System. This book does not assume you know what any of that means yet; every term gets explained in plain words the first time it appears, and the chapters that follow build the whole picture from the ground up. What matters here, at the very start, is the spirit of the thing. APRS was created in the 1980s by *Bob Bruninga, WB4APR*, not as a way to track vehicles but as a *situational-awareness* tool: a way for radio operators to share, in real time, what is going on around them. That vision — a common operating picture for emergencies, public-service events, and any operation where hams need to see the whole board — guides every design decision in this program.

There is a second, sadder reason this program exists in the exact form it does. A beloved earlier APRS client, *UI-View32* by *Roger Barker, G4IDE*, was closed-source. When Roger passed away in 2004, the source code was destroyed at his request. The program could never again be fixed, updated, or moved to a new operating system, and a tool that many operators relied on slowly died as the world moved on around it. APRS Command is released under the *GPL v3* license specifically so that can never happen to it: no one can take it closed, no one can destroy its source, and if the original author is gone tomorrow, any ham in the world can pick it up and carry it forward. This very book is part of that same promise — it exists so the reasoning behind the code survives, not just the code.

> **The one-sentence version** — APRS Command is a cross-platform, open-source APRS client built to give ham operators a shared, real-time operating picture; this book explains not just what the code does but why it was written the way it was, so the project can outlive its author.


### Who this book is for, and how to read it

This is the *Programming Guide Book* — the developer-and-maintainer companion to the separate operator's *User Manual*. It is written for three readers at once: a future maintainer who has to change the code safely, a curious newcomer who has never written a line of C#, and the project's own posterity — whoever inherits this program years from now. The standard it holds itself to is deliberately high: the thoroughness of a doctoral thesis, expressed in the language of an everyday person. Nothing is dumbed down and nothing is left mysterious.

Every section answers the same three questions in the same order: *what* it does, in one plain sentence; *why* it was built this way, including the alternative that was rejected and the problem the chosen shape prevents; and *how* it actually works, in real code from the real source, explained line by meaningful line. Read this book beside the code, not instead of it — the file and method names are real, and you are meant to open them. When a piece of jargon appears for the first time, it is defined right there, usually with a plain-world comparison, so you never have to already know the vocabulary to follow the argument.


### The theme that runs through everything: receive-first

If you remember one idea before reading anything else, remember this one, because it shapes the whole design: *receive-first*. Listening to the radio costs nothing and breaks no rules, so the app listens freely, by default, the moment it starts. *Transmitting* — actually putting a signal on the air or posting to the shared internet backbone — is the opposite. It is a serious, regulated act: you must identify yourself with a real, licensed callsign, you must not jam a shared frequency, and during a training drill you must be able to guarantee that nothing leaks onto the real air.

So in APRS Command, transmitting is never assumed; it is earned. Every condition that would make a transmission legal or safe starts out as "no," and only a deliberate, correct configuration turns it to "yes." You will see this asymmetry echoed everywhere — in how the network connection defaults to receive-only, in how radio ports must be explicitly enabled before they can key up, and above all in the single locked door that every outgoing packet must pass through (the subject of §11, the chapter that set this book's voice). Receive-first is not a slogan; it is a structural commitment you can point at in the source.

> **A few terms, in plain words** — A codebase is simply all the source files that make up the program. A project (here, a .csproj) is one buildable box of related code. A layer is a project with a defined job that is only allowed to depend on the layers beneath it. GPL v3 is an open-source license that guarantees the source must always stay open and available. A callsign is the unique, government-issued identifier a licensed ham transmits under. None of this requires prior programming knowledge to follow.


### How the program is organized — the parts

APRS Command is not one giant blob of code. It is deliberately split into separate *projects*, each a small box with one clear job, arranged in *layers* so that dependencies only ever point downward. Think of it as a building: the foundation knows nothing about the roof, but the roof rests on everything below it. The lowest layer understands the radio protocol and nothing else; the top layer draws the windows and buttons and rests on all the layers below. This separation is enforced by the compiler itself — a project simply cannot reach a project it was not given permission to reference — so the boundaries cannot quietly rot over time.

```csharp
// Dependency direction is downward-only, enforced at compile time by ProjectReference.
// A layer may see the layers below it, never the layers above.
//
//   Aprs.Core         (pure APRS protocol: packet types + parser, no I/O, no UI)
//        ^
//   Aprs.Transport    (moving bytes: APRS-IS, serial/TCP KISS, AGWPE)
//        ^
//   Aprs.Services     (the brains: stations, beacons, iGate, digipeater, alerts, safety)
//        ^
//   Aprs.Mapping      (map symbols, tile providers, Mapsui glue)
//        ^
//   Aprs.Desktop      (Avalonia UI + the composition root that wires it all together)
//
//   AprsCommand.Api / .Contracts  (optional local REST + WebSocket surface + shared DTOs)
```

The reason for this discipline is exactly the lesson of UI-View32. Protocol parsing, network I/O, business rules, and the user interface all change at different speeds and for different reasons; keeping them physically apart is what prevents the tangled coupling that made older clients impossible to port to new platforms. Here is what each box does and, just as importantly, what it is forbidden to do:

| Project | Its one job | What it must never touch |
| --- | --- | --- |
| Aprs.Core | APRS packet types and the parser — pure protocol logic | No serial, no network, no files, no UI |
| Aprs.Transport | Carrying bytes in and out: APRS-IS, KISS, AGWPE | No business rules, no UI |
| Aprs.Services | The business logic: stations, beacons, iGate, digipeat, alerts, transmit safety | No UI; reaches Core/Transport only through interfaces |
| Aprs.Mapping | Map symbols, tile sources and cache, Mapsui integration | No transmit logic |
| Aprs.Desktop | The Avalonia UI — views, viewmodels, and the composition root | No raw sockets or parser calls; talks to services |
| AprsCommand.Api / .Contracts | Optional local REST API, WebSocket stream, and shared DTOs | Ships disabled, read-only, transmit blocked by default |

All seven projects, plus the test projects, are gathered in one Visual Studio *solution* file, `CrossPlatformAprs.sln` — the single entry point a developer opens to build the whole program. Correctness is guarded by two test projects: `tests/Aprs.Tests`, a suite of roughly twelve hundred automated checks, and `tests/Aprs.FuzzHarness`, which throws torrents of malformed and hostile input at the parser to prove it never crashes. The whole thing targets .NET 10, with the exact toolchain version pinned in `global.json` so every contributor builds against the same compiler.


### How this book is organized

The book follows the same downward path as the code: it starts at the surface with orientation, descends into the pure protocol core, works outward through transport and services, and finishes at the user interface and the concerns of long-term health. There are six parts and, at the time of writing, twenty-two numbered chapters. You are reading §1 now. The chapter numbers are fixed and meaningful — more on why that matters in a moment.

| Part | Covers | Chapters |
| --- | --- | --- |
| I — Orientation | What the program is, the architecture at a glance, the project layout | §1–§3 |
| II — The Core Domain | Packets as data, the parser family, position and specialized decoders | §4–§7 |
| III — Moving Data | Transports, async streams and back-pressure, the event bus | §8–§10 |
| IV — The Services Layer | Transmit safety, station state, replay/training, feature services, persistence | §11–§15 |
| V — The Desktop App | Composition & startup, MVVM, the map, extension surfaces | §16–§19 |
| VI — Quality & Longevity | The test suite, how the codebase is meant to grow, how this book is maintained | §20–§22 |

Chapter §11, *The Transmit-Safety Authority*, was written first, before all the others, as the calibration piece — the sample that locked this book's voice and depth before the rest were generated. If you want to feel the standard the whole book is held to, read §11 early. It is also the fullest expression of the receive-first theme introduced above.


### How this book is maintained — stable numbers, dated amendments

A program under active development changes constantly, and a book that has to be reprinted from scratch every time a paragraph goes stale is a book nobody keeps current. So this guide is maintained by a specific discipline, borrowed from the way technical standards and legal codes stay current without being reissued cover to cover. The rule is simple and absolute: *section numbers are permanent*. Once §11 means the Transmit-Safety Authority, it means that forever. New material is *added* with a fresh number; it is never *inserted* in a way that pushes existing numbers up. A cross-reference written today still points at the same section a decade from now.

When something does change, the change ships as its own short, dated, standalone *amendment* document rather than as a silent edit buried in a reprint. Each amendment carries one of two tags so its nature is obvious at a glance:

```csharp
AMENDS §11.3   // revises existing material within section 11.3
ADDS   §23     // adds an entirely new section, numbered after the last one

// An amendment is dated, printable on its own, and appended to the book.
// Existing section numbers are never renumbered to make room — only extended.
```

> **The one discipline that makes this work** — Never renumber. To revise, issue an AMENDS for the existing number. To add, issue an ADDS with the next unused number. This is what lets improvements accumulate over years without invalidating a single reference anyone has ever written — "we won't kill so many trees," and no reader is ever left unsure which edition they hold.

To keep all of this legible, the core book carries an *Amendments Register* — a running table, maintained in §22, that lists every amendment ever issued with its date, its tag, and a one-line summary. A reader can glance at the register and know exactly what state the book is in and which loose amendment pages, if any, belong appended to it. The register is empty at first publication; its first rows will look like this:

| Date | Tag | Summary |
| --- | --- | --- |
| — | (none yet) | First publication — no amendments issued |
| (future) | AMENDS §X.Y | Revises the named subsection; supersedes the prior text |
| (future) | ADDS §Z | Adds a new section; existing numbers unchanged |

The book is produced in three formats from a single source. The *Markdown* files in the repository are the living source of truth — they live beside the code, travel with it through version control, and are the thing you edit. The polished *PDF* and *Word* editions are generated from that Markdown for reading, printing, and sharing; they are never hand-edited. Because the source lives next to the code, a *freshness manifest* maps each chapter to the source files it describes, so a single change to the code can flag exactly which chapters need a look — the guide is designed to be kept honest, not to drift.


## Why It Matters / Design Takeaways

APRS Command exists to make sure a tool the community depends on can never again be lost the way UI-View32 was — and this book exists for the same reason, one level up: to preserve the *reasoning*, not just the code, so a stranger years from now can understand every decision well enough to carry the project forward. Read it beside the source, expect every term to be explained, and expect every design choice to come with the alternative it beat and the problem it prevents.

Two ideas carry through everything that follows. First, *receive-first*: listening is free and default, transmitting is earned and deliberate — you will see that asymmetry structurally, not just rhetorically. Second, *layered separation*: seven small projects with compiler-enforced boundaries, so the parts can evolve independently and the whole stays portable. Hold those two, and the rest of the book is elaboration.

> **For the maintainer of this book** — The numbering discipline is not optional decoration — it is the mechanism that lets the guide grow for years without ever being invalidated. When you improve a section, issue an AMENDS; when you document something new, issue an ADDS with the next free number and log it in the Amendments Register. Never renumber an existing section. That single rule is what keeps this book, like the program it documents, alive after its author.


# 2. The Big Picture: Architecture at 10,000 Feet

*How a single radio packet travels from the airwaves or the internet, through parsing and services, and finally becomes a dot on the map.*


## What This Is / What It Is For

APRS-Command is, at its heart, a listening station. Somewhere out there a ham radio operator's tracker transmits a tiny burst of text saying "here I am, at this latitude and longitude, and here's a short note." That burst can reach this program two completely different ways: over the air as a radio signal, or over the internet through a worldwide relay network. This chapter follows one of those bursts on its entire journey — from the moment it arrives as a meaningless line of characters to the moment it appears as a labeled icon on your map — and explains why the road it travels is shaped the way it is.

If you understand this one journey, you understand the spine of the whole application. Almost every feature in APRS-Command — the message center, weather display, net-control roster, geofence alerts, station trails, the developer API — is a branch hanging off this single trunk. Learn the trunk first and every branch makes sense.

The key idea to hold onto: *no matter where a packet comes from* — radio, internet, a file being replayed, a training simulation — it is funneled into *one* shared pipeline and treated identically from there on. That single funnel is the most important design decision in the program, and most of this chapter is about why it exists and how it works.


### The journey in one breath

Before the details, here is the whole trip as a numbered walk. Each step is a real class in the source; later sections open each one up.

1. A *transport client* (for example `AprsIsClient` for the internet, or `KissTcpCoordinator` for radio-over-network) receives a raw text line and stamps it with where it came from.
2. The `LiveDataCoordinator` catches that raw line and hands it to the pipeline, making sure it lands on the correct thread first.
3. The `AprsIngestionService` — the single funnel — records the raw line, asks the parser to make sense of it, and files the result.
4. The `AprsParser` inspects the line's structure and turns it into a strongly-typed *domain object* like a `PositionAprsPacket` or `WeatherAprsPacket`.
5. The `StationDatabase` folds that object into what it already knows about that station — updating position, counting packets, spotting duplicates, recording a trail point.
6. The ingestion service raises two events — `PacketParsed` and `PacketIngested` — announcing that a packet just went through.
7. On a gentle timer, the `LiveDataCoordinator` copies the current station list into the `MapViewModel`, which turns each station into a map marker you can see.

> **Two outputs, not one** — A packet produces two results at the end: it updates the station database (which drives the map and station list), AND it fires an event that dozens of independent features listen to. The map is just the most visible consumer. Hold this dual nature in mind — it is why one packet can simultaneously move a dot, pop a message toast, speak an alert aloud, and forward data to an external service.


### Why the app is built in layers

APRS-Command is split into separate projects that are stacked like floors of a building, and the building code forbids reaching down through the floor. In plain terms: the code that talks to the internet is not allowed to know anything about buttons and maps, and the code that draws the map is not allowed to open a network socket. Each floor may only call the floor directly below it.

The reason is not tidiness for its own sake — it is testability and longevity. The parser and station database live in projects (`Aprs.Core` and `Aprs.Services`) that have *no idea a user interface exists*. That means you can feed them ten thousand test packets in a fraction of a second with no window ever opening. It also means that if the desktop UI framework is someday replaced, the entire brain of the application survives untouched. An *interface* here works like a wall socket: `AprsIngestionService` accepts anything shaped like an `IAprsParser`, and does not care whether that's the real parser or a fake one wired up for a test.

| Layer (project) | Plain-language job | What it is allowed to touch |
| --- | --- | --- |
| Aprs.Transport | Talk to the outside world — internet servers, radio modems — and hand back raw text lines | Sockets and streams only; no domain meaning |
| Aprs.Core | Understand APRS grammar: turn a raw line into a typed packet object | Nothing else — pure logic, zero dependencies |
| Aprs.Services | Hold the state and rules: station database, ingestion, event bus, alerts | Aprs.Core only; never the UI or a socket |
| Aprs.Desktop | The visible app: windows, view models, and the wiring that connects everything | Everything below it, through interfaces |

> **The boundaries are compiler-enforced** — These are not polite suggestions in a style guide. Each layer is a separate .csproj that only references the layers it is permitted to see. If someone tries to call a map view model from inside the parser, the code simply will not compile. The architecture defends itself.


### Receive-first: why listening is the default and talking is fenced off

APRS-Command boots up already listening, and it will happily run forever without ever transmitting a single packet. This is deliberate, and it is baked into how the app connects. Look at how the startup opens its internet connection in `DesktopRuntime.Start()` — it calls `ConnectAprsIsReceiveOnly`.

```csharp
// LiveDataCoordinator.ConnectAprsIsReceiveOnly(...)
var config = defaults with
{
    Callsign = string.IsNullOrWhiteSpace(callsign) ? "N0CALL" : callsign.Trim(),
    // ... server, port, filter ...
    ReceiveOnly = true,
    TransmitEnabled = false,
};
```

A receive-only connection to the APRS internet network logs in with the special passcode `-1`, which the network recognizes as "this station may listen but may never send." The connection is physically incapable of transmitting. Why go to this trouble? Because on a shared radio band and a global network, an accidental or buggy transmission is rude at best and disruptive at worst. Making reception the safe, always-on default means the app is useful and harmless the instant it launches — you can watch the world without ever keying up.

Transmitting, by contrast, is treated as a privileged act that must pass through a guard. Every code path that could send — beacons, messages, objects, digipeating — consults a single `ITransmitSafetyAuthority` before keying up. That authority also doubles as a global "inhibit" switch used by training and exercise modes, so an instructor can freeze all transmission with one flag while the receive side keeps running normally. Listening is free; talking is gated.


### The single funnel: AprsIngestionService

This is the pinch point the whole architecture is built around. Every transport — internet, radio-over-TCP, serial radio, the AGWPE/BPQ32 packet engine, the bundled Direwolf software modem, replayed history, and the training simulator — ends up calling the same one method: `IngestReceivedLine`. Here is the entire core of it.

```csharp
public void IngestReceivedLine(string rawLine, AprsPacketSource source, DateTimeOffset receivedAtUtc)
{
    if (string.IsNullOrWhiteSpace(rawLine)) return;

    rawPacketLog.AddReceivedRawPacket(rawLine, source, timestampUtc: receivedAtUtc);

    AprsPacket? packet = null;
    if (parser.TryParse(rawLine, receivedAtUtc, out packet, out _) && packet is not null)
    {
        var target = source == AprsPacketSource.Replay && replayStationDatabase is not null
            ? replayStationDatabase
            : stationDatabase;
        target.ProcessPacket(packet, source);
    }

    PacketParsed?.Invoke(this, new ParsedPacketEventArgs(packet, source));
    PacketIngested?.Invoke(this, EventArgs.Empty);
}
```

Read it top to bottom and you have the pipeline in miniature. First it drops empty lines. Then it logs the raw text verbatim (so the exact bytes are always recoverable, even if parsing later fails). Then it asks the parser to decode the line; `TryParse` follows the .NET convention of returning `true`/`false` instead of throwing, because a malformed packet off the air is normal, not an error. If a packet came out, it is filed into the station database — with one clever twist: replayed packets go to a *separate* database so reviewing history never disturbs your live picture. Finally it announces the packet to any listeners.

Notice the second argument to every call: an `AprsPacketSource`. This is a small tag — `AprsIs`, `TcpKiss`, `SerialKiss`, `Direwolf`, `Agwpe`, `Replay`, `Simulation`, and a few more — that rides along with the packet so downstream features can tell where it came from. The RF diagnostics screen, for instance, only pays attention to packets tagged as arriving over radio.

> **Why one funnel matters** — Because every source converges here, a feature written once — say, geofence alerting — automatically works for internet packets, radio packets, and replayed history alike, with zero extra code. Add a brand-new transport tomorrow and every existing feature lights up for it for free. That is the entire payoff of the single-funnel design.

Every transport looks the same at the point it feeds in. Here is the radio-over-TCP coordinator doing exactly what the internet client does, just with a different source tag:

```csharp
// KissTcpCoordinator.OnRawPacketReceived
if (!string.IsNullOrWhiteSpace(e.RawPacketLine))
{
    ingestion.IngestReceivedLine(e.RawPacketLine, AprsPacketSource.TcpKiss, e.ReceivedAtUtc);
}
```


### The parser: from a line of characters to a real object

The `AprsParser` is the translator. APRS packets are a terse, decades-old text format; a raw line looks like `W1AW-9>APRS,WIDE1-1:=4123.45N/07234.56W>Mobile test`. The parser's job is to break that apart and hand back a tidy C# object whose meaning is obvious to the rest of the program. It works in two stages: first it splits the universal envelope, then it identifies the specific message type.

The envelope split keys off two characters. The `>` separates the sender from the routing path; the `:` separates that header from the actual *information* payload. From the header it pulls the source callsign and its *SSID* (a number 0–15 that lets one operator run several stations, like `W1AW-9` for a vehicle), the destination, and the digipeater path the packet hopped through.

Then comes the type dispatch — a waterfall of checks, in a deliberate order, because some APRS type-markers are ambiguous and order resolves the ambiguity.

```csharp
if (weatherParser.CanParse(rawPacket.Information))
    return weatherParser.Parse(rawPacket);

if (IsPositionInformation(rawPacket.Information))
    return positionParser.Parse(rawPacket);

// MIC-E encodes position in the destination field; its markers don't
// collide with anything else, so it is safe to dispatch here.
if (micEParser.CanParse(rawPacket.Information))
    return micEParser.Parse(rawPacket);

// ... status, telemetry, message, object/item, query ...
```

Each specialized sub-parser (`positionParser`, `weatherParser`, `micEParser`, and the rest) owns the fiddly rules of one format, so no single method becomes a thousand-line monster. One case is worth calling out because it shows real defensive thinking: *third-party traffic*. On the internet, a gateway station often wraps a packet it heard on the radio inside its own packet, marked with a `}`. The parser unwraps it and re-parses the inner packet so the *original* station surfaces, not the gateway — but it carries a depth counter and refuses to unwrap more than three times, so a maliciously or accidentally looping chain can never spin the program into an infinite recursion.

```csharp
if (rawPacket.Information.StartsWith('}') && thirdPartyDepth < 3)
{
    var inner = rawPacket.Information.Length > 1 ? rawPacket.Information[1..] : string.Empty;
    return Parse(inner, receivedAtUtc, thirdPartyDepth + 1);
}
```

The output of all this is one of a family of *record* types — `PositionAprsPacket`, `WeatherAprsPacket`, `MessageAprsPacket`, and so on, all sharing the base `AprsPacket`. A record is an immutable data object: once created it never changes, which makes it safe to hand around the app without fear that some distant piece of code will quietly alter it. If a line is gibberish the parser still returns an object — an `UnknownAprsPacket` or a raw packet flagged with validation errors — so the pipeline never crashes on bad input; it simply marks the packet invalid and moves on.


### The station database: turning packets into a living picture

A single position packet is a snapshot; the `StationDatabase` is the movie. Its job is to remember every station it has heard and fold each new packet into what it already knows. Stations are kept in a dictionary keyed by callsign-and-SSID, compared case-insensitively, so `w1aw-9` and `W1AW-9` are the same station. When a packet arrives for a known station, the database creates an updated `StationSnapshot` that carries forward the old details and overlays whatever the new packet provides.

```csharp
// ApplyPacketSpecificFields — a position packet overlays only what it carries,
// keeping prior values where the new packet is silent.
PositionAprsPacket position => station with
{
    Latitude  = position.Latitude  ?? station.Latitude,
    Longitude = position.Longitude ?? station.Longitude,
    Comment   = string.IsNullOrEmpty(position.Comment) ? station.Comment : position.Comment,
    CourseDegrees = position.CourseDegrees ?? station.CourseDegrees,
    // ... speed, altitude, symbol ...
},
```

The `?? station.Latitude` reads as "use the new latitude, but if the packet didn't include one, keep the latitude we already had." This is why a station whose latest packet is a plain status message doesn't suddenly lose its position on the map — the database is additive, layering new facts over old ones rather than wiping the slate each time.

The database also quietly does three housekeeping jobs that matter enormously in the field. It *detects duplicates*: APRS packets frequently arrive two or three times via different paths, so the database hashes each packet's payload and, if it saw the same content within thirty seconds, counts it as a duplicate instead of pretending it's fresh news. It *records trails*: successive positions for a moving station are stitched into a breadcrumb path, with rules to skip near-identical points and cap how many are kept. And it *ages stations*: every station has a lifecycle — Active, Stale, Expired, Hidden — computed from how long since it was last heard, so a station that went quiet an hour ago fades rather than lingering forever as if still present.

> **Objects and items get names, not callsigns** — Most packets are keyed by the sender's callsign. But APRS "objects" (a flagged hazard, an aid station) and "items" are keyed by the object's own name instead, because the point is the thing on the map, not who reported it. The database's GetStationKey handles this switch, which is why a placed object shows under its label rather than the callsign of whoever announced it.


### Keeping the map smooth: the coalescing refresh

Here is a problem the naive design would trip over. During a busy net or a big event, packets can pour in dozens per second. If the map redrew itself on every single packet, the interface would stutter and thrash. The `LiveDataCoordinator` solves this with a technique worth understanding: it *coalesces* updates. Instead of refreshing on every packet, each packet just flips a flag.

```csharp
// In the constructor:
this.ingestion.PacketIngested += (_, _) => dirty = true;

// Started once, runs forever:
refreshTimer = new DispatcherTimer(
    TimeSpan.FromMilliseconds(500),
    DispatcherPriority.Background,
    (_, _) => RefreshIfDirty());

private void RefreshIfDirty()
{
    if (!dirty) return;
    dirty = false;
    var source = ActiveStationDatabase;
    source.UpdateAgeStates(DateTimeOffset.UtcNow);
    map.UpdateStations(source.GetVisibleStations());
    rawPacketLog.Refresh();
}
```

A thousand packets arriving in half a second set the same `dirty` flag a thousand times, but the map redraws just *once*, on the next timer tick. Whether one packet or a hundred arrived, the cost of redrawing is paid at most twice a second. This is a classic trade: the map may lag reality by up to half a second, which no human notices, in exchange for staying perfectly smooth under any load.

There's a threading subtlety here too. Packets arrive on background threads (network and radio don't wait politely for the UI). But user-interface objects in this framework may only be touched from one special thread. So the internet client marshals each raw line onto that UI thread before ingesting it — `Dispatcher.UIThread.Post(...)` — which keeps the entire station database and log single-threaded and therefore free of the subtle bugs that plague shared-across-threads data. The whole receive spine runs on one thread by design.

```csharp
aprsIsClient.RawPacketReceived += (_, e) =>
    Dispatcher.UIThread.Post(() =>
        ingestion.IngestReceivedLine(e.RawPacketLine, AprsPacketSource.AprsIs, e.ReceivedAtUtc));
```


### The last mile: from station snapshot to map marker

The final hop is short. When the refresh timer fires, it asks the database for the currently-visible stations and hands them to `MapViewModel.UpdateStations`, which turns each `StationSnapshot` into a `StationMarkerViewModel` the map can draw.

```csharp
public void UpdateStations(IEnumerable<StationSnapshot> stations)
{
    var markers = stations
        .Select(station => StationMarker.TryCreate(station, out var marker) ? marker : null)
        .OfType<StationMarker>()
        .ToList();

    Markers.Clear();
    foreach (var marker in markers)
        Markers.Add(new StationMarkerViewModel(marker));
}
```

`StationMarker.TryCreate` quietly filters out any station that has no usable position — you cannot pin a dot with no coordinates — so a station heard only via a status message stays in the list but doesn't clutter the map. `Markers` is an *observable collection*: a list that announces its own changes, so the map view is subscribed and repaints automatically the instant markers are swapped in. The view model never reaches out to touch the map; it just changes its data, and the UI follows. The packet's journey is complete.


### The parallel channel: the event bus

There is a second nervous system running alongside the station-database path, and it's easy to miss. The `AprsEventBus` is a publish-and-subscribe hub: components announce typed events ("a station updated," "weather came in," "a transmit was blocked") without knowing or caring who is listening. Think of it as a radio station broadcasting on labeled channels — the `DecodedEventLogService`, the on-screen event monitor, the file-hook system, and the developer-facing REST and WebSocket APIs all tune in to the channels they care about.

Why have this in addition to the direct ingestion events? Because it *decouples* the parts of the app that don't belong together. The parser and station database should not know that a WebSocket server exists. The event bus lets those distant, optional features subscribe to the packet flow without the core ever holding a reference to them. It also keeps a rolling history of recent events (so a screen opened late can still show what just happened) and safely isolates subscribers — if one handler throws an exception, the bus catches it and the others still run.

> **Two paths, one purpose** — The direct path (ingestion → station database → map) is the tight, ordered, single-threaded spine that must be fast and correct. The event bus is the loose, optional, extensible fabric for everything else. Simulation and replay publish onto the same bus, which is exactly why a replayed session lights up the event log and developer API just like live traffic.


### Where it all gets wired: App.axaml.cs and DesktopRuntime

Two files hold the assembly instructions for the whole machine. `DesktopRuntime.Create()` is the *composition root* — the one place where every real service is constructed and handed its dependencies. It builds the dependency-injection container, creates the ingestion service, the station databases, the coordinator, the map view model, and the transports, and threads them together. Concentrating this in one method means there is exactly one place to look to understand what the live app is made of, and exactly one place to change a wiring decision.

Then `App.axaml.cs` performs the *fan-out*: a series of small `Wire...` methods that each subscribe one feature to the packet stream. This is where the trunk sprouts its branches, and reading them is the fastest way to grasp the app's breadth. Each does one job:

```csharp
// Every parsed position packet updates the net-control roster:
rt.Coordinator.PacketParsed += (_, e) =>
{
    if (e.Packet is not PositionAprsPacket pos) return;
    Dispatcher.UIThread.Post(() =>
        rt.MainViewModel.NetControl.ProcessHeardStation(
            pos.SourceCallsign, pos.Latitude, pos.Longitude, pos.Comment, pos.ReceivedAtUtc));
};
```

That same `PacketParsed` event is the tap that feeds weather to the weather window, telemetry to the telemetry monitor, positions to station trails and geofence checks, statistics to the counters, and packets to the CalTopo forwarder — each in its own tidy `Wire...` method. Because they all filter by packet type ("is this a `WeatherAprsPacket`?") and by source tag, each feature quietly ignores everything that isn't its business. This is the whole architecture in action: one stream in, many independent listeners, each doing one thing well.


## Why It Matters / Design Takeaways

If you preserve nothing else about this design, preserve the *single funnel*. Every transport converging on `AprsIngestionService.IngestReceivedLine` is what lets the app treat radio, internet, replay, and simulation as interchangeable, and it is why every feature works across every source without special cases. Resist any temptation to let a new transport shortcut into the map or a feature directly — route it through the funnel and it inherits the entire app for free.

Preserve the *layer boundaries*. The parser and station database knowing nothing of the UI is not academic purity; it is what makes the brain of the app fast to test and cheap to keep alive across UI changes. The compiler enforces this today. Keep it that way — the moment a service reaches up into a view model, the wall has a hole in it.

Preserve *receive-first safety*. The app listens by default and treats transmission as a privileged act behind a single safety authority. On shared airwaves that is not a convenience, it is a responsibility. New transmit paths must go through the same gate, no exceptions.

Preserve the *coalesced refresh* and the *single-threaded spine*. The half-second, dirty-flag redraw is what keeps the map calm during a packet storm, and marshaling every packet onto the UI thread before ingestion is what keeps the shared station data free of concurrency bugs. These are quiet decisions that only reveal their value under load — don't optimize them away without understanding what they buy.

Finally, understand the *two channels*: the tight ingestion spine that must be correct and fast, and the loose event bus that makes the app endlessly extensible. New features almost always belong on the bus or on a `PacketParsed` subscription in `App.axaml.cs` — a new branch on the trunk — not surgery on the trunk itself. Keep the trunk boring and let the branches be where the app grows.


# 3. The Solution Layout: Projects and Boundaries

*How APRS-Command is split into nine projects, what each owns, and the inward-pointing dependency rule that keeps the core clean.*


## What This Is / What It Is For

APRS-Command is not one big program. It is nine smaller programs (in .NET these are called *projects* — a project is one unit of code that compiles into one file, like one chapter that gets bound into a book) that are stitched together at the very end. This chapter is the map of those nine pieces: what each one owns, which ones are allowed to know about which others, and the single rule that keeps the whole thing from turning into spaghetti.

Why should a future maintainer care about the layout before touching any real code? Because the boundaries between projects are the load-bearing walls of the building. If you understand where a wall is and why it is there, you will not accidentally knock it down — and knocking one down is how a clean codebase slowly rots into one where every change breaks three unrelated things. The project split is the author's way of writing the architecture rules into a form the compiler itself will enforce, so the rules cannot quietly be forgotten.


### The nine projects at a glance

Everything lives in one *solution* (a solution is just the container that holds all the projects together — the file is `CrossPlatformAprs.sln`). Source projects sit under `src\`, test projects under `tests\`. Here is the whole roster, with who owns what and who each one is allowed to reference.

| Project | Plain-language job | References (may use) |
| --- | --- | --- |
| Aprs.Core | The pure rules of the APRS radio language — packet models and the parsers that turn raw text into objects. | Nothing. Zero dependencies. |
| Aprs.Transport | The wires to the outside world — talking to radios (KISS/AGWPE/Direwolf over serial) and to the internet APRS-IS servers. | Aprs.Core |
| AprsCommand.Contracts | A frozen, versioned vocabulary of plain data shapes (DTOs) used at the app's outer edges — API and extensions. | Nothing. Zero dependencies. |
| Aprs.Services | Where the app actually thinks — alerts, beacons, weather, the event bus, folder setup. ~300 files. | Aprs.Core, Aprs.Transport, AprsCommand.Contracts |
| AprsCommand.Api | The outward-facing doors — a local REST API, a WebSocket event stream, and file import/export hooks for extensions. | AprsCommand.Contracts, Aprs.Services |
| Aprs.Mapping | Everything map-related — tiles, symbols, markers, Maidenhead grid math. | Aprs.Core, Aprs.Services |
| Aprs.Desktop | The screen you actually see — the Avalonia UI, and the place where all the pieces are wired together and the program starts. | Core, Transport, Services, Mapping, Api (+ UI packages) |
| Aprs.Tests | Automated tests that check the real behavior of every layer. | All source projects |
| Aprs.FuzzHarness | A stress-tester that fires live internet traffic at the parser to hunt for crashes. Run by hand. | Aprs.Core, Aprs.Transport |

> **A note on the CLAUDE.md mismatch** — The project guidance file describes a different program (an 'Activation Planner' on Avalonia 12 / a ProcessEngine–PropagationModel–Services–UI stack). The real APRS-Command source does not match that. This chapter describes what is actually in the source: nine projects targeting `net10.0`, a UI built on Avalonia 11.3.7, and the layer names shown above. When the guidance file and the source disagree, the source wins.


### Aprs.Core — the pure heart

*What it does:* Core holds the knowledge of the APRS language itself — what a position report looks like, how a Mic-E compressed packet is decoded, what fields a weather or telemetry packet carries — and nothing else. Its 15 files are all parsers and data models (`AprsParser.cs`, `AprsMicEParser.cs`, `AprsPacket.cs`, `AprsConstants.cs`, and so on).

*Why it is built this way:* Core is deliberately kept *pure* — meaning it depends on absolutely nothing. Look at its entire project file:

```csharp
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

Notice what is missing: there is not a single `<ProjectReference>` and not a single `<PackageReference>`. Core knows nothing about radios, nothing about the network, nothing about screens, nothing about databases. That is the point. Parsing radio text is the trickiest, most bug-prone code in the whole app (which is exactly why the fuzz harness exists to attack it). Keeping it dependency-free means it can be tested in complete isolation — you can throw ten thousand malformed packets at it in a plain test loop without ever starting a UI, opening a serial port, or touching the internet. The `IAprsParser` interface even promises it will never throw on bad input; it returns a `bool` and hands back an error string instead. A component with no dependencies is the one you can reason about, test, and trust the most, so the most dangerous logic lives exactly there.


### Aprs.Transport — the wires to the outside

*What it does:* Transport is everything that carries raw APRS text between the app and the physical/network world: the AGWPE/Direwolf client, the APRS-IS internet client, KISS-over-serial, and AX.25 frame encoding/decoding. It is the one place that is allowed to do messy real-world I/O.

*Why it is built this way:* Transport references Core (it produces the raw text that Core parses) and pulls in exactly one outside package — `System.IO.Ports` — because talking to a serial radio needs it:

```csharp
<ItemGroup>
  <PackageReference Include="System.IO.Ports" Version="9.0.0" />
  <ProjectReference Include="..\Aprs.Core\Aprs.Core.csproj" />
</ItemGroup>
```

The key design move is the `IAprsTransport` interface — a tiny contract that every kind of connection (radio or internet) satisfies. An *interface* is like a wall socket: any device with the right plug works, and the socket does not care what the device is. Here every transport promises the same three things:

```csharp
public interface IAprsTransport : IAsyncDisposable
{
    string Name { get; }
    IAsyncEnumerable<string> ReadPacketsAsync(CancellationToken cancellationToken);
    Task SendPacketAsync(string packet, CancellationToken cancellationToken);
}
```

Because a serial radio and an internet server both present themselves as an `IAprsTransport`, the layers above (Services, UI) can read and send packets without knowing or caring which one is plugged in. Swapping a USB radio for an internet feed changes nothing upstream — that is the payoff of putting all the I/O behind one small, stable seam.


### AprsCommand.Contracts — the frozen vocabulary

*What it does:* Contracts is a set of plain data shapes — `MessageDto`, `StationUpdateDto`, `WeatherObservationDto`, `RawPacketDto`, and friends. A *DTO* (Data Transfer Object) is a simple container whose only job is to carry values across a boundary, like a shipping box that holds goods but has no machinery of its own. Contracts also owns the versioning and JSON-serialization rules for those boxes (`ContractSchemaVersion.cs`, `ContractJsonSerializerOptions.cs`).

*Why it is built this way:* Like Core, Contracts depends on nothing — its project file has no references at all. That is intentional: these shapes are the language spoken at the app's outer doors (the local API and any third-party extensions). If they had dependencies, changing an internal detail could ripple out and break an extension somebody else wrote. By being tiny, self-contained, and versioned, Contracts can stay stable for the outside world even as the guts of the app churn. It is a separate project precisely so the compiler forbids the internal code from smuggling private types into a public promise.


### Aprs.Services — where the app thinks

*What it does:* Services is the biggest project by far (~300 files) and holds the application's brain: alert rules and triggers, beacon formatting, the weather ingestion pipeline, the internal event bus, application-folder setup, bulletins, announcements, and much more. It coordinates Core (parsing) and Transport (I/O) into features.

*Why it is built this way:* Services is allowed to reference the three lower building blocks and nothing above it:

```csharp
<ItemGroup>
  <ProjectReference Include="..\Aprs.Core\Aprs.Core.csproj" />
  <ProjectReference Include="..\Aprs.Transport\Aprs.Transport.csproj" />
  <ProjectReference Include="..\AprsCommand.Contracts\AprsCommand.Contracts.csproj" />
</ItemGroup>

<ItemGroup>
  <!-- APRS device-ID (tocall) database snapshot, bundled so device
       identification works offline. -->
  <EmbeddedResource Include="DeviceId\tocalls.dense.json" />
</ItemGroup>
```

Two things stand out. First, Services depends only downward — on Core, Transport, and Contracts. It never references the UI, so all this logic can run and be tested with no screen attached. Second, the tocall device-ID database is *embedded* (baked straight into the compiled file) so that identifying which radio model sent a packet works offline, with no separate data file to lose. The deliberate choice to keep the thinking here — and out of the UI — is what lets the same logic be reused by the API and covered by tests.


### AprsCommand.Api and Aprs.Mapping — the feature satellites

*Aprs.Mapping* owns everything map-shaped: tile downloading and caching, APRS symbol lookup, map markers (station/object/weather), and Maidenhead grid-square math. It references Core and Services — it is a feature layer sitting on top of the brain, kept in its own project so the heavy map machinery does not bloat the rest.

*AprsCommand.Api* owns the app's outward doors: a local REST API, a WebSocket event stream, and file import/export hooks that let extensions plug in. It references Contracts (the frozen vocabulary it speaks) and Services (the brain it exposes). Keeping the API in its own project means the risky, outside-facing surface is walled off from the core logic; the app can function completely even if nobody ever opens those doors.


### Aprs.Desktop — the composition root

*What it does:* Desktop is the program you see and run. It is the only project marked `OutputType=Exe` among the app proper, it carries the version and copyright, and it holds the Avalonia views, the viewmodels, the converters, the persistence and audio and runtime folders — and, crucially, the wiring that connects everything else together and starts it up.

*Why it is built this way:* Desktop sits at the top and is allowed to reference every other source project, because it is the *composition root* — the single place where all the parts are assembled into a running whole (like the final assembly line where the engine, wheels, and body finally meet):

```csharp
<ItemGroup>
  <ProjectReference Include="..\Aprs.Core\Aprs.Core.csproj" />
  <ProjectReference Include="..\Aprs.Transport\Aprs.Transport.csproj" />
  <ProjectReference Include="..\Aprs.Services\Aprs.Services.csproj" />
  <ProjectReference Include="..\Aprs.Mapping\Aprs.Mapping.csproj" />
  <ProjectReference Include="..\AprsCommand.Api\AprsCommand.Api.csproj" />
</ItemGroup>
```

This is also the only project that carries the heavy UI packages — Avalonia 11.3.7, Mapsui (the map control), Microsoft.Data.Sqlite, the Microsoft dependency-injection container, Velopack for updates. That concentration is deliberate: because those packages live only in Desktop, none of the lower layers get contaminated by UI concerns. A quick check of the source confirms it — there is not a single `using Avalonia` anywhere in Core, Transport, or Services. The screen framework stops at the top floor.

> **Why the composition root can reference everything** — It looks like Desktop 'breaks the rules' by depending on all five projects. It does not — being the assembly point is its whole job. The rule being protected is that the lower layers must NOT depend on Desktop. Wiring flows down from one top; knowledge never flows up. One project references many is fine; many projects referencing one at the top would be the disaster.


### The test projects

*Aprs.Tests* is the automated safety net (xUnit). Its project file references every source project — Core, Transport, Services, Mapping, Desktop, Contracts, and Api — because tests need to reach into every layer to check real behavior. Tests are the one place where referencing everything is expected and healthy: a test's job is to observe, not to be observed.

*Aprs.FuzzHarness* is a separate, hand-run stress tool (its own `Exe`). It references only Core and Transport, because its single mission is to connect to live APRS-IS and fire a firehose of real-world packets through the parser to surface crashes and misdecodes. Its own project description says it is deliberately kept out of the automated CI suite — it is a hunting tool, not a pass/fail gate. Giving it the minimum two references keeps it lean and focused on the parser it is trying to break.


### The one rule: dependencies point inward

Strip away the details and every reference edge in the whole solution obeys one direction. Draw it and the arrows only ever point toward the pure, dependency-free heart:

```csharp
Aprs.Desktop  (the exe — references everything below)
   │  ├── AprsCommand.Api ── Contracts, Services
   │  ├── Aprs.Mapping ───── Core, Services
   │  ├── Aprs.Services ──── Core, Transport, Contracts
   │  ├── Aprs.Transport ─── Core
   │  └── Aprs.Core ──────── (nothing)
   │
   └── Contracts ─────────── (nothing)

Arrows only point DOWN/INWARD. Nothing points up at Desktop
except the test projects, whose job is to watch everything.
```

Because these references are declared in the `.csproj` files, the compiler enforces the rule for you. If someone tries to make Core reach up into Services, or sneak an Avalonia UI type into Transport, the build simply fails — there is no reference edge that would allow it. The architecture is not a diagram in a wiki that drifts out of date; it is compiled reality. The nesting in the solution file (`src` and `tests` folders) is cosmetic grouping for the IDE, but the reference edges are the real, enforced law.


## Why It Matters / Design Takeaways

The whole layout expresses one idea: the more dangerous or valuable a piece of code is, the fewer things it should depend on. So the trickiest logic — parsing the APRS language — lives in Core with zero dependencies, and the outward-facing promises live in Contracts with zero dependencies. Everything else is arranged in rings around those two hearts, each ring allowed to know only about the rings closer to the center.

For a future maintainer, three things must be preserved. First, never add an upward reference: a lower layer must never reference a higher one, and nothing but the tests may reference Desktop. Second, keep Core and Contracts dependency-free — the moment either grows a reference, its whole reason for existing (isolation and stability) is gone. Third, keep UI, database, and networking packages confined to the projects that own them (Desktop for UI, Transport for radio/network) so the brain in Services stays testable with no screen and no wires attached.

If you honor those three rules, the project split keeps doing its quiet job: making the illegal shortcuts literally impossible to compile, so the design survives long after anyone remembers why it was drawn this way.


# 4. Packets as Data: the AprsPacket Record Hierarchy

*How one abstract record and its thirteen sealed subtypes turn a line of radio text into a safe, immutable, self-describing object.*


## What This Is / What It Is For

APRS is a system where ham radios broadcast tiny bursts of text over the air — a position report, a weather reading, a short message, a status line. Each burst, once decoded, is a single line of text. The file `src/Aprs.Core/AprsPacket.cs` is where APRS-Command decides how to *hold* one of those decoded bursts in memory so the rest of the program can work with it safely.

The whole file is one idea expressed thirteen times: there is a single family of data types called **AprsPacket**, and every kind of packet the app understands is a member of that family. A position report becomes a **PositionAprsPacket**. A weather report becomes a **WeatherAprsPacket**. A line nobody could make sense of becomes an **UnknownAprsPacket**. They are all `AprsPacket`s, so any code that just wants "a packet" can accept the whole family, while code that cares about weather specifically can ask for exactly the weather kind.

This chapter explains three things: what a *record* is and why the author chose it, why the packets are *immutable* (unchangeable once created), and what each of the real subtypes represents. Getting this right matters because this file is the *vocabulary* of the entire application. Every map marker, every log entry, every message thread eventually traces back to one of these records. If the shape of the data is wrong or fragile here, every layer above inherits the problem.

> **Where this sits** — AprsPacket lives in Aprs.Core, the lowest layer — no UI, no networking, just the plain data shapes and the parsers that fill them. Everything else in the app depends on Core; Core depends on nothing.


### First, what is a "record"?

In C#, a *record* is a special kind of type whose job is to *hold data* rather than *do things*. You can think of the difference like this: a regular class is like an employee — it has information but it also performs actions. A record is more like a filled-out paper form — it is purely the information, sitting still, and its whole identity is the values written on it.

The very first lines of the file declare the head of the family as an *abstract record*:

```csharp
public abstract record AprsPacket(
    string RawLine,
    string SourceCallsign,
    int? SourceSsid,
    string Destination,
    IReadOnlyList<string> Path,
    string Information,
    DateTimeOffset ReceivedAtUtc,
    bool IsValid,
    IReadOnlyList<string> ValidationErrors);
```

That short block does a surprising amount. The word `abstract` means "this is a category, not a thing you can create directly" — you never make a bare `AprsPacket`, only one of its concrete subtypes, just as you never buy "a vehicle" at a dealership, you buy a specific car or truck. The list in parentheses is called a *positional record*: it declares, in one breath, nine pieces of data the packet carries *and* the constructor that accepts them *and* read-only properties to read them back. Written as an old-style class, the same thing would be roughly fifty lines of boilerplate.

Each field is named in plain domain terms. `RawLine` is the original text exactly as received. `SourceCallsign` is the operator's call sign (like `KE4CON`), and `SourceSsid` is the optional number after a dash that distinguishes that operator's several stations (a home base versus a truck). `Path` is the list of relay stations the packet hopped through. `Information` is the payload — the part whose meaning depends on the packet type. `ReceivedAtUtc` stamps when the app saw it. `IsValid` and `ValidationErrors` record whether the line parsed cleanly and, if not, what went wrong.

> **Why int? and not int** — The question mark in `int? SourceSsid` means "nullable" — the value is allowed to be absent. Not every station has an SSID, so the type itself says "this may be missing," forcing later code to handle the empty case instead of guessing a fake default like 0.


### Why records instead of ordinary mutable classes?

A packet describes a moment that already happened: a specific radio said a specific thing at a specific time. That fact never changes afterward. Modeling it as an *immutable* value — one that cannot be altered after it is created — matches reality and closes off a large family of bugs.

The properties a positional record generates are *init-only*: they can be set when the object is built and never again. Consider what that prevents. Suppose the map, the log window, and a message service all hold a reference to the same received packet. With a normal mutable class, if the log window "cleaned up" the callsign in place, the map would silently see the changed value too — a spooky action-at-a-distance bug that is miserable to track down. Because the packet is frozen, sharing it is completely safe: nobody can change what everybody else is looking at.

The second gift records bring is *value-equality*. A normal class compares by *identity* — two objects are "equal" only if they are literally the same object in memory, like two people being the same person only if they are one body. A record instead compares by *content* — two records are equal when all their fields are equal, the way two filled-out forms are "the same form" if every box matches. That is exactly what you want for de-duplicating packets: the same beacon heard twice should be recognized as the same information without any hand-written comparison code.

```csharp
// Illustrative — value-equality is generated for you:
var a = new UnknownAprsPacket(raw, "KE4CON", 9, "APRS", path, info, when, true, errors, qc);
var b = new UnknownAprsPacket(raw, "KE4CON", 9, "APRS", path, info, when, true, errors, qc);
bool same = a == b;   // true — because every field matches, not because they are one object
```

> **The design decision in one line** — Packets are frozen facts. Records give you free construction, free read-only properties, free content-based equality, and a compiler guarantee that a received packet can never be quietly mutated out from under the code holding it. That is why the author reached for records over classes here.


### Why a hierarchy, and why the base fields repeat

Every packet, regardless of kind, has a source call sign, a path, a receive time, and a validity flag. Those nine fields belong to *all* of them, so they live on the abstract base `AprsPacket`. What differs is the meaning packed into the `Information` payload — and that is what each subtype adds on top.

Look at how a concrete subtype is declared. It restates the nine base fields, adds its own, and then hands the nine base values back up to the base with the `: AprsPacket(...)` call at the bottom:

```csharp
public sealed record PositionAprsPacket(
    string RawLine, string SourceCallsign, int? SourceSsid, string Destination,
    IReadOnlyList<string> Path, string Information, DateTimeOffset ReceivedAtUtc,
    bool IsValid, IReadOnlyList<string> ValidationErrors,
    string? QConstruct,           // added: how an internet gateway tagged the packet
    char PositionType,            // added: which position format was used
    string? Timestamp,
    double? Latitude, double? Longitude,
    char? SymbolTableIdentifier, char? SymbolCode,   // which map icon to draw
    string Comment,
    int? CourseDegrees, int? SpeedKnots, int? AltitudeFeet,
    int PositionAmbiguity)
    : AprsPacket(
        RawLine, SourceCallsign, SourceSsid, Destination,
        Path, Information, ReceivedAtUtc, IsValid, ValidationErrors);
```

The keyword `sealed` means "nothing may inherit from this" — `PositionAprsPacket` is a leaf, a final concrete kind, not a category to be extended further. That keeps the family flat and knowable: one abstract root, and a fixed set of sealed leaves. Anyone reading the file can see the *entire* set of packet types the program recognizes in one place, which is a real gift for a project meant to outlive its author.

You may notice `QConstruct` repeats on nearly every subtype rather than sitting on the base. That is a deliberate line-drawing: `QConstruct` is an APRS-Internet-System detail (a marker showing how a gateway injected the packet), meaningful for real decoded packets but not conceptually part of the universal "every packet has a source and a time" core. It rides along on the concrete types that can carry it.

> **A little repetition is the price of clarity** — Positional records cannot inherit a constructor's parameter list, so each subtype restates the base fields. It looks repetitive, but the payoff is that each record reads as a complete, self-contained description of one packet kind — no hunting up a chain of parent classes to learn what fields exist.


### The thirteen real subtypes

Here is the full family as it actually appears in the file, in the order it is written. Each is a `sealed record` deriving from `AprsPacket`.

| Subtype | What it represents | A few of its own fields |
| --- | --- | --- |
| RawAprsPacket | A packet decoded down to the common fields but not yet interpreted by type — the starting point every richer type is built from | QConstruct |
| PositionAprsPacket | A station reporting where it is (and possibly heading/speed/altitude) | Latitude, Longitude, CourseDegrees, SpeedKnots, AltitudeFeet, PositionAmbiguity |
| StatusAprsPacket | A short human status line, optionally with a grid locator and beam info | StatusText, MaidenheadLocator, BeamHeadingDegrees, EffectiveRadiatedPowerWatts |
| TelemetryAprsPacket | Numeric sensor readings — five analog channels plus eight on/off bits | SequenceNumber, AnalogValues, DigitalValues |
| TelemetryMetadataAprsPacket | The labels/units/equations that give a telemetry stream meaning | MetadataKind, Values, BitValues, ProjectTitle |
| CapabilityAprsPacket | A station announcing what services/features it offers | CapabilityText |
| UserDefinedAprsPacket | A developer-private format the spec reserves but does not define | UserId, Content |
| UnknownAprsPacket | A line that parsed structurally but matched no known type | (base fields + QConstruct only) |
| MessageAprsPacket | A directed text message, bulletin, announcement, ack, or query | Addressee, MessageBody, MessageId, IsBulletin, BulletinGroup, IsAnnouncement, IsQuery |
| QueryAprsPacket | A request asking stations to respond (position, weather, ping, etc.) | QueryType, QueryKeyword, QueryTarget |
| ObjectAprsPacket | A point one station places on the map on behalf of something else, with a live/killed state | ObjectName, IsAlive, IsKilled, Latitude, Longitude |
| ItemAprsPacket | Like an object but simpler, with no timestamp/kill lifecycle | ItemName, Latitude, Longitude |
| WeatherAprsPacket | A full weather observation | WindSpeedMph, TemperatureFahrenheit, HumidityPercent, BarometricPressureMillibars, RainLastHourHundredthsInch |

A few of these deserve a note. **UnknownAprsPacket** is not a failure — it is a deliberate, first-class outcome. When a line does not match any known type, the app still returns a real, valid `AprsPacket` object carrying the raw text, rather than throwing an error or returning nothing. That is a quiet but important robustness decision: the radio world is full of odd and future formats, and the app is designed to shrug them off gracefully rather than crash on the unexpected.

**UserDefinedAprsPacket** makes the same philosophy explicit in its own doc comment: the APRS spec reserves a `{` format for developer-private data with no standard meaning, so the app recognizes it as its own type purely so it is *not* misclassified as Unknown — captured and labeled, but honestly left uninterpreted.

> **Objects vs. Items** — ObjectAprsPacket carries a timestamp and an IsAlive/IsKilled lifecycle — it can be placed and later 'killed' off the map. ItemAprsPacket is the lighter cousin with no timestamp and no kill state. Same idea (put a point on the map for something that isn't a station), two spec formats, two honest record types.


### A small helper enum: AprsQueryType

Tucked between the message and query records is a plain enumeration that classifies what a query is asking for:

```csharp
public enum AprsQueryType
{
    General,   // ?APRS? and the ?APRSx station-data variants
    IGate,     // ?IGATE?
    Weather,   // ?WX?
    Ping,      // ?PING?
    Unknown    // an unrecognized keyword (still captured in QueryKeyword)
}
```

An *enum* (enumeration) is simply a fixed, named set of choices — like the settings on a light switch that can only be Off, Low, or High. Storing the query kind as `AprsQueryType.Weather` instead of the raw string `"?WX?"` means later code branches on a clean, spell-checked value the compiler understands, and the raw keyword is still preserved in `QueryKeyword` for anything that needs the original text. Note the same forgiving pattern again: even an unrecognized query gets a home (`Unknown`) rather than being dropped.


### How a rich packet gets built

The records are just the shapes; the parsers fill them. The flow is always: decode the line into a bare **RawAprsPacket** first, then, once the type is known, build the specific subtype by copying the raw packet's common fields forward and adding the interpreted ones. You can see this exact move in `AprsPositionParser.cs`:

```csharp
return new PositionAprsPacket(
    rawPacket.RawLine,
    rawPacket.SourceCallsign,
    rawPacket.SourceSsid,
    rawPacket.Destination,
    rawPacket.Path,
    rawPacket.Information,
    rawPacket.ReceivedAtUtc,
    rawPacket.IsValid && validationErrors.Count == 0,
    validationErrors,
    rawPacket.QConstruct,
    positionType, timestamp,
    parsedPosition.Latitude, parsedPosition.Longitude,
    parsedPosition.SymbolTableIdentifier, parsedPosition.SymbolCode,
    parsedPosition.Comment,
    courseDegrees, speedKnots, altitudeFeet,
    parsedPosition.PositionAmbiguity);
```

The nine base fields are handed straight through from `rawPacket`, and the position-specific values (latitude, longitude, course, speed, symbol) are the newly interpreted additions. One detail worth catching: `IsValid` is recomputed as `rawPacket.IsValid && validationErrors.Count == 0` — the position stage can *demote* an otherwise-valid raw packet if it finds its own problems. Validity is cumulative, tightened as more is understood, never loosened.

> **The same shape from many sources** — PositionAprsPacket is constructed in at least three parsers — the standard position parser, the Mic-E parser, and the NMEA parser. Because they all target one record type, everything downstream sees a single uniform 'position' shape no matter which on-air format it arrived in.


### How the rest of the app tells the types apart

Once packets are a sealed family, code that needs to react differently by type uses *pattern matching* — asking "which kind is this?" in a way the compiler can check. `AprsBulletinService` does exactly this with a `switch` expression:

```csharp
return packet switch
{
    MessageAprsPacket messagePacket when messagePacket.IsAnnouncement => StoreAnnouncement(messagePacket, source),
    MessageAprsPacket messagePacket when messagePacket.IsBulletin     => StoreBulletin(messagePacket, source),
    MessageAprsPacket messagePacket when messagePacket.IsQuery        => StoreMessageQuery(messagePacket, source),
    QueryAprsPacket queryPacket                                       => StoreDirectQuery(queryPacket, source),
    _ => AprsBulletinAcceptResult.NotHandled
};
```

Read it top to bottom: if the packet is a `MessageAprsPacket` *and* its `IsAnnouncement` flag is set, store it as an announcement; a bulletin flag routes it as a bulletin; and so on. The final `_` is the catch-all — anything this service does not care about is politely declined with `NotHandled` rather than mishandled. Because the packet family is sealed and known, this kind of branching is safe and exhaustive: there are no surprise subtypes lurking outside the file to break the assumptions.

This is the payoff of the whole design. The parser's job is to *classify once*, turning a fuzzy line of text into a precise typed record. From then on, every other layer trusts the type. A weather panel asks for `WeatherAprsPacket` and gets fields like `TemperatureFahrenheit` already parsed; it never re-parses raw text or guesses. The type system carries the knowledge forward for free.


## Why It Matters / Design Takeaways

**Records because packets are frozen facts.** A received packet describes something that already happened and must never change. Immutable records enforce that at the compiler level, make packets safe to share across the map, the log, and services simultaneously, and hand you free value-equality for de-duplication. This is the single most important reason the file is written the way it is — preserve it.

**One sealed family, all visible in one file.** A future maintainer can open `AprsPacket.cs` and see the complete, closed set of packet kinds the app understands — thirteen leaves under one abstract root. The mild repetition of base fields is the deliberate cost of that clarity and of positional records; do not 'clean it up' into a deep inheritance chain that hides what each type contains.

**Unknown and UserDefined are features, not gaps.** The design chooses to always produce a valid packet object, even for lines it cannot interpret, so the radio firehose of odd and future formats degrades gracefully instead of crashing. Keep that forgiving posture: classify what you can, capture what you cannot, never drop or throw on the merely unfamiliar.

**Classify once, trust the type forever after.** Parsers do the hard interpretive work up front and encode the result as a specific subtype. Every layer above then uses pattern matching to branch safely and exhaustively, never re-parsing raw text. If you add a new packet kind, add a sealed record here first — the type system will then guide you to every place that must learn to handle it.


# 5. Parsing: IAprsParser and the Parser Family

*How a raw line of APRS text is recognized, split into its parts, and routed to the one specialized parser that understands its shape.*


## What This Is / What It Is For

Every APRS station on the air speaks in short bursts of text. A weather station, a car with a GPS tracker, a person sending a text message, a repeater announcing itself — they all send lines that look superficially similar but mean completely different things. Something has to look at each incoming line, figure out *what kind* of packet it is, pull it apart into meaningful pieces, and hand back a tidy object the rest of the app can use. That something is the parser.

This chapter is about two files: **IAprsParser.cs**, which is the *promise* the parser makes to everyone who calls it, and **AprsParser.cs**, the *main* parser that keeps that promise. The main parser does not try to understand every packet type by itself. Instead it does the front-desk work — split the line into its standard parts — and then acts as a switchboard, routing each packet to one of several specialized parsers (weather, position, MIC-E, telemetry, message, and so on), each an expert in one format.

Understanding this file is understanding the spine of the whole application. Every piece of live data that ever appears on the map, in a message window, or on a weather panel enters through here first. If you grasp how a line becomes a typed packet, the rest of the codebase reads much more easily.

> **Where the code lives** — src/Aprs.Core/IAprsParser.cs (the contract) and src/Aprs.Core/AprsParser.cs (the dispatcher). The specialized parsers — AprsWeatherParser, AprsPositionParser, AprsMicEParser, AprsTelemetryParser, AprsMessageParser, AprsObjectItemParser, AprsNmeaParser — sit beside them in the same folder.


### First, what is an interface?

*An interface* is a list of things a class promises to be able to do, written down separately from *how* it does them. The classic analogy is a wall socket: any appliance with the right plug works, and the socket does not care whether you plug in a lamp or a laptop charger. The socket defines the *shape* of the connection; the appliance provides the behavior.

In C#, an interface is a named set of method signatures with no bodies. Here is the entire parsing contract, **IAprsParser**:

```csharp
public interface IAprsParser
{
    bool TryParse(string rawLine, DateTimeOffset receivedAtUtc,
                  out AprsPacket? packet, out string? error);
}
```

That is the whole promise: give me a raw line and the time it arrived, and I will give you back a parsed **packet** and, if something was wrong, an **error** describing it. The `bool` return says at a glance whether the line looked valid.

Two words there deserve unpacking. *out* marks a parameter the method fills in *for* you rather than reads *from* you — it is a second and third way to hand results back, alongside the normal return value. And the `?` in `AprsPacket?` and `string?` means "this may be null" (null = "nothing here") — the compiler will remind any caller to check before using it. So the signature itself tells the honest story: you get a true/false answer, a packet that might be null, and an error that might be null.


### Why parse behind an interface at all?

The main parser could simply be a class everyone calls directly. Putting an interface in front of it buys three concrete things.

- **Swappability.** Anything that needs to parse depends on the *shape* (IAprsParser), not the specific class (AprsParser). A future parser — a faster one, a stricter one, a fake one — can drop into the same socket with no changes to callers.
- **Testability.** Tests for code that *uses* a parser can plug in a tiny stand-in that returns canned packets, without dragging in the full parsing machinery. This is the same reason ProcessEngine hides VOACAP behind IProcessTransport in the sibling project — depend on the plug, not the appliance.
- **A clear boundary.** The interface is a short, readable summary of exactly what the parser offers. A newcomer reads seventeen lines and knows the contract, without wading through three hundred lines of parsing logic.

> **Read the interface first** — When you meet an unfamiliar subsystem in this codebase, find its interface. It is the honest one-paragraph summary — the menu before the kitchen.


### The contract's real rule: parse, don't throw

The XML comment on the interface states the single most important design decision in one phrase: it parses lines "without throwing on malformed input." This matters enormously for APRS.

APRS is radio. Packets arrive corrupted, truncated, half-decoded, or simply weird — a burst of static clips the end of a line, two stations transmit at once, an old radio uses a nonstandard format. If the parser *threw an exception* (crashed out of its normal flow with an error) every time it met a bad line, the receive loop would have to wrap every single call in error-handling, and one malformed packet could stall the stream of good ones.

Instead the design says: a bad line is *normal*, not exceptional. The parser always returns a packet. If the line was malformed, the packet simply carries a list of validation errors and reports itself invalid. Nothing crashes. Here is how the main class delivers on that promise:

```csharp
public bool TryParse(string rawLine, DateTimeOffset receivedAtUtc,
                     out AprsPacket? packet, out string? error)
{
    var parsed = Parse(rawLine, receivedAtUtc);
    packet = parsed;
    error = parsed.ValidationErrors.FirstOrDefault();
    return parsed.IsValid;
}
```

`Parse` does the real work and *always* returns something. `TryParse` then repackages that into the interface's shape: the packet goes out through `packet`, the first validation error (if any) through `error`, and the true/false answer is just `parsed.IsValid`. Even a completely garbled line comes back as a packet you can safely inspect — never a crash.

> **The Try pattern** — The name TryParse follows a long-standing .NET convention (int.TryParse, DateTime.TryParse): return a bool for success, hand the real result back through an out parameter, and never throw for ordinary bad input. Following the convention means the method behaves exactly the way an experienced C# developer expects on sight.


### Step one: split the line into its standard parts

Before anything can decide *what kind* of packet a line is, the line has to be cut into its universal pieces. Every APRS line shares the same skeleton:

```csharp
SOURCE>DESTINATION,PATH1,PATH2:information text

// Example:
// KE4CON-9>APRS,WIDE1-1,WIDE2-1:!3745.00N/12225.00W>on the trail
```

The *source* is who sent it (a callsign, optionally with a `-9` style *SSID* — a number 0-15 that distinguishes a ham's several stations, like a home rig versus a car). The *destination* and *path* describe how it was addressed and which digipeaters relayed it. Everything after the colon is the *information field* — the actual payload, and the part whose first character reveals the packet type.

The private `Parse` method carves this up with two landmark characters. First the colon splits header from information:

```csharp
var separatorIndex = workingLine.IndexOf(':');
if (separatorIndex < 0)
    validationErrors.Add("Packet is missing ':' information separator.");

var header = separatorIndex >= 0 ? workingLine[..separatorIndex] : workingLine;
var information = separatorIndex >= 0 ? workingLine[(separatorIndex + 1)..] : string.Empty;
```

Note the pattern that repeats throughout: a missing landmark adds a validation error but does *not* stop the work. If there is no colon, the code records the complaint and carries on treating the whole line as a header. Then the `>` splits the header into source versus destination-and-path, and helper methods `ParseSource` and `ParseDestinationAndPath` finish the job — validating the callsign, checking the SSID is in range 0-15, and rejecting empty path components. All of it accumulates errors rather than throwing.

The result of this first stage is a **RawAprsPacket** — every packet type in the app inherits from the shared **AprsPacket** record, and RawAprsPacket is the plain, not-yet-classified version carrying the split-up fields. (A *record* is C#'s shorthand for a simple, immutable data-holder — see the fields listed in AprsPacket.cs.) This raw packet is the common raw material every specialized parser receives.


### Step two: the dispatch chain

With the line split, the parser now plays switchboard operator. It looks at the information field and routes the raw packet to whichever specialized parser recognizes its shape. The mechanism is a straightforward top-to-bottom sequence of checks — the first one that matches wins and returns immediately:

```csharp
if (weatherParser.CanParse(rawPacket.Information))
    return weatherParser.Parse(rawPacket);

if (IsPositionInformation(rawPacket.Information))
    return positionParser.Parse(rawPacket);

if (micEParser.CanParse(rawPacket.Information))
    return micEParser.Parse(rawPacket);

// ... status '>', capability '<', telemetry, message, object/item,
//     query '?', third-party '}', NMEA '$', user-defined '{' ...

return new UnknownAprsPacket(/* ... */);
```

Most of these decisions come down to the *first character* of the information field, because the APRS specification assigns each packet type a *data type indicator* — a single leading character that announces the format. A `:` means a message, a `;` an object, a `>` a status report, a `_` or certain position characters a weather report, a `?` a query, and so on. The parser reads that first character and knows which door to open.

| Leading character | Routed to | Meaning |
| --- | --- | --- |
| !  =  /  @ | AprsPositionParser (or weather) | Position report (with/without timestamp) |
| _ | AprsWeatherParser | Positionless weather report |
| 0x60  0x27  0x1C  0x1D | AprsMicEParser | Compact MIC-E position (Kenwood/Yaesu) |
| > | StatusAprsPacket | Status report |
| < | CapabilityAprsPacket | Station capabilities |
| T# | AprsTelemetryParser | Telemetry data |
| : | AprsMessageParser | Text message |
| ;  ) | AprsObjectItemParser | Object / item |
| ? | QueryAprsPacket | Query (?WX?, ?PING?, ...) |
| } | (unwrap and re-parse) | Third-party traffic |
| $ | AprsNmeaParser | Raw NMEA GPS sentence |
| { | UserDefinedAprsPacket | Developer-defined format |


### The CanParse / Parse pattern

Notice the shape of most branches: `if (someParser.CanParse(...)) return someParser.Parse(...)`. Every specialized parser exposes two methods. **CanParse** answers a yes/no question — *is this line mine to handle?* — cheaply and without side effects. **Parse** does the actual, heavier decoding, and is only ever called after CanParse has already said yes.

This split keeps each format's recognition logic living *with* that format's decoding logic, instead of piling every recognition rule into the dispatcher. The weather parser knows what a weather packet looks like; the MIC-E parser knows what MIC-E looks like. The dispatcher just asks each expert in turn. Some CanParse checks are trivial one-liners:

```csharp
// AprsMessageParser
public bool CanParse(string information) => information.StartsWith(':');

// AprsObjectItemParser
public bool CanParse(string information)
    => information.StartsWith(';') || information.StartsWith(')');
```

Others encode real domain knowledge. The MIC-E check recognizes four specific data type indicators, kept as numeric code points so no non-printable characters have to sit in the source file:

```csharp
// AprsMicEParser
public bool CanParse(string information)
    => information.Length > 0
       && information[0] is CurrentGps or OldGps or LegacyCurrentGps or LegacyOldGps;
```

And the weather parser's recognition is genuinely subtle: a weather report can be *positionless* (starts with `_`) or ride *inside* a position report, where a `_` symbol code appears at a computed offset after the latitude and longitude — a different offset depending on whether the position is compressed. That entire judgement lives in the weather parser's `IsPositionWeather`, exactly where it belongs, not in the dispatcher.


### Why the order of the checks is not arbitrary

Because the first matching branch wins, the *sequence* of the checks is itself a design decision — and a couple of orderings prevent real bugs.

Weather is checked **before** plain position. Many weather packets *are* position packets with a weather payload attached; a weather report often begins with the very same `!`, `=`, `/`, or `@` characters a position report uses. If the position check ran first, every weather station would be misfiled as an ordinary position and its temperature, wind, and rain would be silently dropped. Putting weather first, with its more specific recognition, ensures the richer interpretation wins.

MIC-E is safe to place mid-chain, and a code comment says exactly why:

```csharp
// MIC-E encodes position in the destination field; its DTIs (0x60 / 0x27 / 0x1C / 0x1D) do not
// collide with any other packet type, so it is safe to dispatch here.
if (micEParser.CanParse(rawPacket.Information))
    return micEParser.Parse(rawPacket);
```

MIC-E is unusual — it stashes latitude and the N/S sign inside the *destination* field, an idea most formats never use — but its four leading indicators are unique to it, so no earlier or later check could accidentally swallow a MIC-E packet. The comment records that reasoning so a future maintainer does not "tidy" the order and break it.

> **Ordering is load-bearing** — Reordering these checks is not a cosmetic change. Move the position check above the weather check and weather data vanishes. When touching the dispatch chain, treat the sequence as part of the correctness, and lean on the parser tests in tests/Aprs.Tests/AprsParserTests.cs to catch a regression.


### Third-party traffic and the recursion guard

One branch is cleverer than the rest. On the internet side of APRS (APRS-IS), a gateway station often forwards a packet it heard on the radio, wrapping the *entire original packet* inside its own, marked by a leading `}`. If the app parsed that at face value, the *gateway* would appear on the map instead of the station that actually sent the report.

So the parser unwraps it and parses the inner packet instead — by calling *itself*:

```csharp
if (rawPacket.Information.StartsWith('}') && thirdPartyDepth < 3)
{
    var inner = rawPacket.Information.Length > 1 ? rawPacket.Information[1..] : string.Empty;
    return Parse(inner, receivedAtUtc, thirdPartyDepth + 1);
}
```

This is *recursion* — a method calling itself to handle a smaller version of the same problem, here "parse the packet hidden inside this packet." But recursion left unchecked is dangerous: a malformed or malicious chain of nested `}` wrappers could make the parser call itself forever and exhaust memory. The guard is the **thirdPartyDepth** counter. It starts at 0, rises by one with each unwrap, and once it reaches 3 the branch simply stops matching — the packet falls through to a later check instead. A real packet is never wrapped that deeply; three is plenty of headroom while still capping the danger.

> **A recursion guard in the wild** — Any time a method can call itself on attacker-influenced data, it needs a bound. The public Parse overload starts the depth at 0 and hides the counter from callers; only the private overload threads it through. That is the standard, safe way to expose recursion without exposing its bookkeeping.


### The fallbacks: nothing is ever silently lost

After all the specific formats have had their turn, two final safety nets catch whatever remains. A `$` line is treated as a raw NMEA GPS sentence — a legacy format — and decoded into a position so the station still plots on the map. A `{` line is a developer-defined "user-defined" format, which the parser at least recognizes as such (pulling off its ID byte and payload) rather than lumping it in with the truly unknown.

And if a line matches nothing at all, it does not disappear — it becomes an **UnknownAprsPacket** (or, if it was already flagged invalid or empty, the raw packet is returned as-is):

```csharp
if (!rawPacket.IsValid || string.IsNullOrEmpty(rawPacket.Information))
    return rawPacket;

return new UnknownAprsPacket(/* all the common fields */);
```

This is the parse-don't-throw philosophy carried all the way to the end. There is no code path where a line comes in and nothing comes out. Even genuine gibberish becomes an object the rest of the app can display, count, or log — which is exactly what you want when the input is a noisy radio channel and you would rather see an odd packet than lose one.


## Why It Matters / Design Takeaways

The parsing layer is small in lines but large in consequence — it is the single gate every scrap of live data passes through. A future maintainer should preserve four ideas above all.

- **The interface is the contract, and the contract is parse-don't-throw.** IAprsParser promises a result for every line and a crash for none. Any replacement parser must keep that promise, because the entire receive path depends on it and none of the callers are written to handle exceptions.
- **The dispatcher classifies; the specialists decode.** AprsParser splits the line and routes it; each CanParse/Parse pair owns one format's rules. Keep new format knowledge inside a new specialized parser and add one branch to the chain — do not grow the dispatcher into a monolith.
- **Check order encodes correctness.** Weather-before-position and the placement of MIC-E are deliberate. The inline comments explain why; respect them, and let the tests guard them.
- **Recursion is bounded and nothing is discarded.** The third-party unwrap is capped at depth 3, and every unmatched line still becomes a packet (Unknown or user-defined). Both choices come from the reality of the medium: radio data is untrusted and lossy, so the parser is defensive and total by design.

Read this way, AprsParser is less a pile of if-statements and more a small, well-reasoned protocol: recognize the universal skeleton, ask the right expert, prefer the richer interpretation, never trust the input further than a counter allows, and never drop a line on the floor.


# 6. Decoding Positions — Compressed and Uncompressed

*How APRS-Command pulls a latitude and longitude out of a packet, in both the human-readable form and the packed base-91 compressed form, without letting a single miscounted character corrupt the map.*


## What This Is / What It Is For

Every dot APRS-Command draws on its map begins life as a short line of text that arrived over the radio. Somewhere inside that text are the two numbers that matter most: a *latitude* (how far north or south) and a *longitude* (how far east or west). This chapter is about the two files that dig those numbers back out of the packet: **AprsPositionParser.cs**, which handles the layout of a position report, and **AprsCompressedPositionDecoder.cs**, which unpacks the dense, space-saving encoding that many stations use.

The reason this deserves its own chapter is that APRS gives you the same information in two very different disguises. One is meant for a human to read at a glance. The other is squeezed down to save precious airtime, at the cost of being unreadable to anyone without a decoder ring. APRS-Command has to accept both, tell them apart instantly, and never confuse one for the other — because a position that is off by even one character puts a station in the wrong ocean.

A *packet* here just means one received message. The part we care about is its *information field* — the payload after the addressing header — which the code holds in a plain string called `information`. Parsing is the act of walking through that string and turning raw characters into real numbers and labels.


### The two ways a position is written

The *uncompressed* (human-readable) form spells the coordinate out in degrees and minutes, the same way a paper chart does. A latitude looks like `4903.50N` — that is 49 degrees, 03.50 minutes, North. A longitude looks like `07201.75W`. It is legible, but it eats characters: 8 for latitude, 9 for longitude, 19 in total once you add the symbol characters between them.

The *compressed* form encodes the exact same coordinate in just 13 characters total, including the map symbol and a bonus field for speed or altitude. It does this by turning each coordinate into one big whole number and then writing that number in an unusual counting system called *base-91*. It is not human-readable at all — `/5L!!<*e7>{?!` is a real, valid compressed position (it decodes to 49.5 North, 72.75 West) — but it is far shorter on the air.

|  | Uncompressed | Compressed |
| --- | --- | --- |
| Looks like | 4903.50N/07201.75W- | /5L!!<*e7>{?! |
| Readable by a human? | Yes | No |
| Latitude size | 8 characters | 4 characters (base-91) |
| Longitude size | 9 characters | 4 characters (base-91) |
| Extra data (speed/altitude) | Optional, in the comment text | Built into 2 dedicated bytes |
| Position ambiguity allowed? | Yes (blanks blur precision) | No — always full precision |


### Telling the two apart from a single character

Before it can decode anything, the parser must decide which of the two forms it is looking at. The trick APRS relies on is elegant: the two formats begin with characters that can never collide. A normal latitude always starts with a *digit* (the tens-of-degrees digit, `0`–`9`). A compressed position always starts with a *Symbol Table Identifier* — the single character `/` or `\` that selects which set of map icons to use. Those are different characters, so one look at the first position settles it.

```csharp
public static bool IsCompressed(string information, int offset)
{
    if (offset >= information.Length) return false;
    var c = information[offset];
    // Compressed format leads with the Symbol Table Identifier (/ or \).
    // Normal lat/long leads with digit characters (degree digits).
    return c is '/' or '\\';
}
```

Line by line: `offset` is the index in the string where the coordinate is supposed to begin. The first `if` is a guard — if the packet is too short to even reach that spot, we cannot be looking at compressed data, so we answer `false` rather than crash. Then we read the one character at that offset and ask a single question: is it `/` or `\`? The odd-looking `'\\'` is how C# writes a single backslash (the first backslash is an escape character). If yes, it is compressed; if it is a digit or anything else, it is not.


### Finding where the coordinate starts

The parser cannot assume the coordinate is at the very front of the information field, because some position reports carry a *timestamp* first. **AprsPositionParser** works this out before anything else.

```csharp
var positionType = information.Length > 0 ? information[0] : '\0';
var hasTimestamp = positionType is '/' or '@';
var latitudeStart = hasTimestamp ? 8 : 1;
var timestamp = hasTimestamp && information.Length >= 8
    ? information.Substring(1, 7)
    : null;
```

The very first character of the information field is a *Data Type Indicator* — a one-character tag that announces what kind of packet this is. The characters `!` and `=` mean a plain position with no time; `/` and `@` mean a position that carries a timestamp. When there is a timestamp, it occupies 7 characters right after the type indicator, so the coordinate does not begin until index `8`. When there is no timestamp, the coordinate begins at index `1`, right after the type indicator. That computed `latitudeStart` value is then handed to every downstream step.


### Reading the human-readable form

For the uncompressed form the work lives in **AprsPositionComponents.Parse**, which slices the fixed-width fields out by position and converts degrees-and-minutes into a single decimal number. Here is the heart of the latitude conversion:

```csharp
var hemisphere = latitudeText[7];
if (hemisphere is not ('N' or 'S'))
{
    validationErrors.Add($"{errorPrefix} latitude hemisphere is invalid.");
    return null;
}

var normalized = latitudeText[..7].Replace(' ', '0');
if (!int.TryParse(normalized[..2], ... out var degrees)
    || !double.TryParse(normalized[2..], ... out var minutes)
    || degrees > 90
    || minutes >= 60)
{
    validationErrors.Add($"{errorPrefix} latitude is invalid.");
    return null;
}

var decimalDegrees = degrees + minutes / 60;
return hemisphere == 'S' ? -decimalDegrees : decimalDegrees;
```

The 8-character latitude field is `DDMM.mmH`: two degree digits, then minutes with a decimal point, then a hemisphere letter. The code reads the hemisphere from the last slot, splits the first two characters as whole degrees and the rest as minutes, then combines them with the standard rule that 60 minutes make one degree (`degrees + minutes / 60`). South and West coordinates are stored as negative numbers so the rest of the program never has to think about hemispheres again — a decimal latitude of `-49.5` simply means 49.5 degrees south.

Two guards earn their keep here. The bounds checks `degrees > 90` and `minutes >= 60` reject impossible coordinates that would otherwise sail through as valid-looking garbage. And `.Replace(' ', '0')` implements APRS *position ambiguity*: a sender can blank out trailing digits with spaces to say "I am being deliberately vague about my exact spot." Turning those spaces into zeros keeps the math working, while a separate routine, `CountPositionAmbiguity`, counts how many were blanked so the map can show the reduced precision honestly rather than pretending to a fake exactness.


### Base-91, explained from zero

To understand the compressed form you need base-91, and the friendliest way in is to remember how ordinary numbers work. Our everyday numbers are *base-10*: ten digits, `0` through `9`, and each position to the left is worth ten times more than the one to its right (ones, tens, hundreds, thousands). Base-91 is the same idea with 91 "digits" instead of ten, so each position to the left is worth 91 times more (ones, 91s, 8281s, and 753571s).

Where do you find 91 different digit symbols? APRS uses 91 consecutive printable ASCII characters, starting at `!` (ASCII code 33) and running up. To turn one of those characters into its numeric value, you simply subtract 33. So `!` is 0, `"` is 1, and so on up to 90. That subtraction is the entire secret, and it is exactly one small function:

```csharp
private static int Base91Value(char c)
{
    var v = (int)c - 33;
    return v is >= 0 and <= 90 ? v : -1;
}
```

`(int)c` gets the character's numeric ASCII code; subtracting 33 shifts `!`-and-up down to `0`-and-up. The range check is a quiet but crucial safety net: any character outside the legal 33-to-124 window returns `-1`, a clearly-impossible "digit" value. Callers test for that negative sentinel and reject the whole coordinate rather than compute a nonsense position from a corrupted byte. A *sentinel* is just an agreed-upon impossible value used to signal "this failed" — like a thermometer reading of -999 that everyone understands to mean "broken," not "very cold."


### Turning four characters back into a latitude

The compressed latitude is four base-91 characters. The decoder reads all four, checks none of them came back as the -1 sentinel, and then assembles them the way you would read a four-digit number — leftmost character is the most significant:

```csharp
var y1 = Base91Value(information[offset + 1]);
var y2 = Base91Value(information[offset + 2]);
var y3 = Base91Value(information[offset + 3]);
var y4 = Base91Value(information[offset + 4]);

if (y1 < 0 || y2 < 0 || y3 < 0 || y4 < 0)
{
    validationErrors.Add($"{errorPrefix} compressed latitude contains invalid base-91 character.");
    return AprsCompressedPosition.Invalid;
}

// Spec §9 p.38: Lat = 90 - ((y1×91³ + y2×91² + y3×91 + y4) / 380926)
var latValue = (y1 * 91.0 * 91.0 * 91.0)
             + (y2 * 91.0 * 91.0)
             + (y3 * 91.0)
             +  y4;
var latitude = 90.0 - (latValue / LatMultiplier);
```

The four `91.0` multiplications are the base-91 place values (91-cubed, 91-squared, 91, and 1) — the direct analog of thousands, hundreds, tens, and ones. That produces one large whole number, `latValue`. The APRS specification then defines a fixed rule for converting that number to degrees: divide by the magic constant `380926` and subtract the result from 90. The constant and the "subtract from 90" both come straight from the protocol's §9 (page 38); they are the exact inverse of how a transmitting station compressed the coordinate in the first place. Longitude works identically, but with its own constant `190463` and a `-180 +` framing instead of `90 -`.


### The two bonus bytes: speed, altitude, or range

After the eight coordinate characters and the symbol code, the compressed format spends two more characters — nicknamed the *cs bytes* — on whatever movement or elevation data the station chose to send, with a final *T byte* (compression type) that says how to interpret them. One field cleverly means three different things depending on context, and the decoder untangles which.

```csharp
if (cByte != ' ' && cByte != 'V')
{
    var cVal = Base91Value(cByte);
    var sVal = Base91Value(sByte);

    if (cByte == '{')
    {
        // Spec §9 p.39: c = { means pre-calculated radio range
        if (sVal >= 0)
            radioRangeMiles = (int)Math.Round(2.0 * Math.Pow(1.08, sVal));
    }
    else if (IsGgaSentence(tByte))
    {
        // T byte says the fix came from a GGA sentence → cs holds altitude
        var csVal = cVal * 91 + sVal;
        if (csVal >= 0)
            altitudeFeet = (int)Math.Round(Math.Pow(1.002, csVal));
    }
    else if (cVal >= 0 && cVal <= 89)
    {
        // course = c × 4 degrees, speed = 1.08^s − 1 knots
        courseDegrees = cVal * 4;
        if (sVal >= 0)
            speedKnots = (int)Math.Round(Math.Pow(1.08, sVal) - 1.0);
    }
}
```

The first guard skips the whole block when the byte is a space (the spec's way of saying "no extra data") — and, pragmatically, also when it is the literal letter `V`, because the specification wrote the space as `V` in its notation and some real transmitters copied the letter by mistake. Accepting both is a small act of defensive compatibility with the messy real world. After that, the value of the first byte selects the meaning: `{` signals a pre-computed radio range; a GGA-sourced fix (detected from bits inside the T byte) means the pair encodes altitude; otherwise it is course and speed. Each branch uses the exact formula from §9 — for example course is the byte's value times 4, giving the compass heading in degrees.

**IsGgaSentence** is a nice miniature of bit-level care: it converts the T byte to its base-91 value, shifts the bits right by three, masks off the low two bits, and checks whether they equal binary `10`. That is the specification's flag for "this position came from a GPS GGA sentence, so the cs bytes are altitude." Reading individual bits out of a byte is exactly the kind of operation that is easy to get subtly wrong, which is why it lives in one named, commented helper rather than being smeared inline.


### Why fixed-column parsing is a bug magnet

Both formats share a hidden danger: they are *fixed-column*. The meaning of a character depends entirely on its exact position — index 7 is the hemisphere, index 9 is the symbol code, the byte at `offset + 12` is the compression type. There are no commas or labels to keep you oriented. Miscount by one, or reach past the end of a truncated packet, and you either read the wrong field or crash on an index that does not exist. Real over-the-air packets are frequently short, garbled, or truncated, so this is not a rare edge case; it is Tuesday.

The code's defense is uniform and deliberate. Every decode begins by proving there is enough length to read before it reads:

```csharp
const int MinLength = 13; // 1 STI + 4 lat + 4 lon + 1 symbol + 2 cs + 1 T

if (information.Length < offset + MinLength)
{
    validationErrors.Add(
        $"{errorPrefix} compressed position field is too short " +
        $"(expected ≥ {MinLength} chars from offset {offset}, " +
        $"got {Math.Max(0, information.Length - offset)}).");
    return AprsCompressedPosition.Invalid;
}
```

The `MinLength` is spelled out with its arithmetic in the comment so the number `13` is auditable, not magical. If the packet is too short, the decoder does not throw and does not guess — it records a specific, human-readable validation error (including how many characters it actually got) and returns the shared `AprsCompressedPosition.Invalid` sentinel, an all-nulls result that downstream code recognizes as "nothing usable here." The uncompressed path mirrors this with `SliceOrEmpty` and `TryGetChar` helpers that clamp every read to the available length, so an incomplete packet yields empty fields and logged errors instead of an exception.


### The final precision touch-up: !DAO!

Both paths finish by running the coordinate through **AprsDaoExtension.Apply**. The `!DAO!` extension is an optional token some stations append to the comment to add a little more precision than the base format carries — roughly one to six extra feet. **AprsPositionComponents** applies it to the uncompressed result and **AprsPositionParser.ParseCompressed** applies it to the compressed one, so both forms end up equally precise.

The interesting design choice is caution: the extension is only honored when a well-formed `!Dxx!` token sits at the very end of the comment, after trimming trailing spaces. That guard exists so an incidental `!...!` in the middle of someone's free-text comment is never mistaken for a precision directive. When a valid token is found its extra fractions of a minute are added toward the reported hemisphere (so magnitude grows in the correct direction) and the token is stripped from the comment the user sees. It is a small feature, but it shows the same instinct as the rest of the code: accept the optional richness the protocol allows, but only when you are certain that is what you are looking at.


### How a real packet flows through

Putting it together with the canonical compressed example `/5L!!<*e7>{?!` (used verbatim as a regression test, where it must decode to 49.5N, 72.75W): the parser sees `!` as the type indicator, computes `latitudeStart = 1`, and finds `/` there — a Symbol Table Identifier, so `IsCompressed` returns true. The decoder confirms 13 characters are available, converts `5L!!` and `<*e7` from base-91 into two big integers, applies the §9 divide-and-offset formulas to get 49.5 and -72.75, reads the symbol and the `>{` cs bytes for course/speed, checks for a `!DAO!` token, and returns a fully-populated position. An uncompressed `4903.50N/07201.75W-` would instead have flowed through the slice-and-convert path in **AprsPositionComponents** to reach the same kind of result.


## Why It Matters / Design Takeaways

The reasoning a future maintainer must preserve is that position decoding is where a stream of noisy radio characters becomes a trustworthy dot on a map, and the cost of a quiet mistake here is a station placed in the wrong place with no error to warn anyone. Everything in these two files is built to make that failure mode impossible.

- **Self-identifying formats.** The compressed/uncompressed choice hinges on one non-overlapping character (`/` or `\` versus a digit). Never replace this with heuristics or guessing — the protocol guarantees the distinction, so the code should too.
- **Base-91 is just subtract-33 place-value arithmetic.** One tiny `Base91Value` helper is the whole decoder ring; keep it, and keep its out-of-range `-1` sentinel, because that sentinel is what stops corrupted characters from producing plausible-looking wrong coordinates.
- **Spec constants are named and cited.** `380926`, `190463`, the `MinLength` of 13, the course-times-4 rule — each is anchored to a page of APRS Protocol Reference §9. That traceability is the difference between auditable and unmaintainable.
- **Fixed-column parsing is guarded, not trusted.** Length is verified before every positional read, on both paths, so truncated real-world packets fail cleanly into a descriptive validation error and an `Invalid` sentinel instead of crashing or lying.
- **Both paths converge on the same refinements.** Course/speed/altitude extraction and the `!DAO!` precision touch-up are applied consistently, so a compressed and an uncompressed report of the same spot yield equally complete, equally precise results.
- **Every fix is pinned by a test.** Regression tests such as `Spec_CompressedObject_PositionDecoded` and the Dire Wolf-verified compressed-weather case lock in behavior against a real reference decoder — the safety rail that lets this delicate code be changed with confidence.


# 7. The Specialized Parsers

*Five format-specific decoders — MIC-E, object/item, weather, telemetry, and message — that turn the trickiest APRS packet types into clean, mappable data.*


## What This Is / What It Is For

APRS (the Automatic Packet Reporting System) is a ham-radio data network where every transmission is a short text line called a *packet*. Chapter 6 covered the ordinary position report, where a station simply says "here I am, at this latitude and longitude." But APRS carries far more than plain positions, and several of its packet types pack their information in ways that look, at first glance, like line noise. This chapter is a tour of the five parsers that handle those harder formats.

A *parser* is just a piece of code that reads raw text and pulls out its meaning — the same job your brain does when it reads "3:45pm" and understands "quarter to four in the afternoon." Each of the five parsers here specializes in one wire format: **AprsMicEParser** (compressed position from trackers), **AprsObjectItemParser** (named markers), **AprsWeatherParser** (weather stations), **AprsTelemetryParser** (sensor numbers), and **AprsMessageParser** (person-to-person text, bulletins, and acknowledgements).

Why five separate classes instead of one giant one? Because each format has genuinely different rules, and a bug in the weather decoder should never be able to break message handling. Splitting them keeps each file small enough to hold in your head, and lets the test suite hammer each format in isolation. This is the same instinct as having separate specialists in a hospital rather than one doctor who does everything adequately and nothing expertly.


### MIC-E: Position Hidden Inside the Callsign

**What it does:** **AprsMicEParser** decodes the most cunning format in all of APRS — a compact position report used by many Kenwood and Yaesu radios and by small GPS trackers, where the location is split across two places in the packet and scrambled with bit tricks to save airtime.

**Why it exists this way:** MIC-E ("Microphone Encoder") was designed when every byte on the air mattered. Rather than spend characters spelling out a latitude, its designers noticed that the AX.25 protocol underneath APRS already reserves a *destination field* — normally the intended recipient — and for position beacons that field is essentially wasted. So MIC-E hides the six latitude digits, the North/South sign, a longitude offset flag, and a status code inside the destination callsign, and puts longitude, speed, course, and symbol in the information field. It is ugly, but it is the reason a handheld radio can beacon its position in a fraction of a second.

The class begins by naming the four bytes that mark a packet as MIC-E, using their numeric codes so the source file stays free of unprintable characters:

```csharp
private const char CurrentGps = (char)0x60;      // '`' current GPS data
private const char OldGps = (char)0x27;          // apostrophe, old GPS data
private const char LegacyCurrentGps = (char)0x1C;
private const char LegacyOldGps = (char)0x1D;

public bool CanParse(string information)
    => information.Length > 0
       && information[0] is CurrentGps or OldGps or LegacyCurrentGps or LegacyOldGps;
```

**How it works:** The first information character (the *data type indicator*, or DTI) must be one of those four. As the dispatch code notes, none of those four bytes collide with any other packet type, so recognizing MIC-E is unambiguous — that is what makes it safe to check by the first byte alone.

The clever part is decoding the destination. Each of its six characters is really carrying two things at once: a latitude digit AND, for the first three characters, one bit of a three-bit status code (the message A/B/C bits). The **DecodeDigit** helper untangles both in a single pass:

```csharp
private static int DecodeDigit(char c, int mask, ref int stdMsg, ref int custMsg, ref int ambiguity)
{
    if (c is >= '0' and <= '9') return c - '0';
    if (c is >= 'A' and <= 'J') { custMsg |= mask; return c - 'A'; }
    if (c is >= 'P' and <= 'Y') { stdMsg |= mask; return c - 'P'; }
    if (c == 'K') { custMsg |= mask; ambiguity++; return 0; }
    if (c == 'L') { ambiguity++; return 0; }
    if (c == 'Z') { stdMsg |= mask; ambiguity++; return 0; }
    return -1;
}
```

Read it as three parallel alphabets that all encode the digits 0-9. Characters `0`-`9` are the plain digits. Characters `A`-`J` also mean 0-9 but they light up a *custom* status bit. Characters `P`-`Y` again mean 0-9 but light up a *standard* status bit. The `|= mask` line is the bit trick: the caller passes a mask of 4, 2, or 1 for the first three characters (and 0 for the rest), so each character contributes one bit to the eventual A/B/C code. The special letters `K`, `L`, `Z` mean "this digit was deliberately blanked for privacy" — the *ambiguity* counter records how many digits were fuzzed so the map can show reduced precision instead of a false pinpoint.

Latitude is assembled from the six decoded digits as degrees, minutes, and hundredths of a minute, with the sign taken from whether the fourth destination character is `P` or higher:

```csharp
var latMinutes = (digits[2] * 10 + digits[3]) + (digits[4] * 10 + digits[5]) / 100.0;
var latitude = (digits[0] * 10 + digits[1]) + latMinutes / 60.0;
var north = dest[3] >= 'P';
if (!north) latitude = -latitude;
```

Longitude, speed, and course come from the information field, and each of those bytes is offset by a fixed amount (the longitude bytes by 28, the speed/course bytes by 28 as well) because the encoding shifts everything into the range of printable characters. The three longitude helpers (**DecodeLongitudeDegrees**, **DecodeLongitudeMinutes**, **DecodeLongitudeHundredths**) each map a printable byte back to a number, with the degrees decode further split into four ranges depending on the 100-degree offset flag read from the destination. The end result is fed into an ordinary **PositionAprsPacket**, so — as the class summary puts it — a MIC-E station "appears on the map exactly like any other position report."


### Objects and Items: Markers a Station Places for You

**What it does:** **AprsObjectItemParser** decodes two closely related formats that let one station put a *named marker* on everyone's map — a storm cell, an aid station, a repeater, a lost hiker's last known spot. An *object* (information starts with `;`) has a timestamp and a live/killed flag; an *item* (starts with `)`) is the lighter-weight version without a timestamp.

**Why one parser for both:** Objects and items are nearly the same idea — a name plus a position plus a symbol — differing mainly in their header layout. Handling them together keeps the shared position-decoding logic in one place, while a single top-level switch routes each to its own method:

```csharp
public bool CanParse(string information)
{
    return information.StartsWith(';') || information.StartsWith(')');
}

public AprsPacket Parse(RawAprsPacket rawPacket)
{
    return rawPacket.Information.StartsWith(';')
        ? ParseObject(rawPacket)
        : ParseItem(rawPacket);
}
```

**How objects work:** An object name is a *fixed-width* field — exactly nine characters, space-padded — so the parser can slice it by position rather than hunting for a delimiter. That is why the constants **ObjectNameLength** (9) and **ObjectTimestampLength** (7) exist: fixed-column formats are fast to parse but unforgiving, so the magic numbers are named and centralized rather than sprinkled through the code.

```csharp
var objectName = rawObjectBody.Length >= ObjectNameLength
    ? rawObjectBody[..ObjectNameLength].TrimEnd()
    : rawObjectBody.TrimEnd();
var liveKilledIndicatorIndex = 1 + ObjectNameLength;
var liveKilledIndicator = TryGetChar(information, liveKilledIndicatorIndex);
var isAlive = liveKilledIndicator == '*';
var isKilled = liveKilledIndicator == '_'; // Real-world convention (WB4APR PROTOCOL.TXT)
```

Right after the nine-character name sits a single *live/killed indicator*: `*` means the object is active, `_` means it has been killed (removed from the map). This matters operationally — a station can place a storm-warning object and later kill it once the danger passes. The code comment credits WB4APR's PROTOCOL.TXT (the late Bob Bruninga, APRS's inventor) because the killed convention is a real-world usage detail, not something you would guess from the formal spec.

**How items differ:** An item has no fixed-width name and no timestamp. Instead its name runs up to a separator character — `!` for a live item, `_` for a killed one — and the parser must find whichever comes first:

```csharp
private static int FindItemPositionSeparator(string rawItemBody)
{
    var liveIndex = rawItemBody.IndexOf('!', StringComparison.Ordinal);
    var killedIndex = rawItemBody.IndexOf('_', StringComparison.Ordinal);

    return liveIndex switch
    {
        >= 0 when killedIndex >= 0 => Math.Min(liveIndex, killedIndex),
        >= 0 => liveIndex,
        _ => killedIndex
    };
}
```

The `switch` reads cleanly once you know the cases: if both separators appear, take whichever comes first (`Math.Min`); if only one appears, take it; if neither appears, return -1 and let the caller record a validation error. Both methods then hand the remaining text to the shared **AprsPositionComponents.Parse** helper, so an object and an item and a plain position report all decode their latitude/longitude/symbol through identical, well-tested code.


### Weather: Fixed Columns and Two Different Shells

**What it does:** **AprsWeatherParser** decodes reports from weather stations — wind, temperature, rain, humidity, pressure, and more — which APRS encodes as a run of single-letter-tagged number fields like `t072` (temperature 72 F) or `h55` (humidity 55%).

**Why it is the trickiest of the everyday formats:** Weather can arrive in two completely different shells. Some reports are *position weather* — a normal position packet whose symbol code happens to be `_` (the weather-station symbol), with the weather data tacked onto the end. Others are *positionless weather* — starting with `_` directly, no location at all, just the readings. The parser has to recognize both and, for position weather, figure out where the position ends and the weather begins.

```csharp
public bool CanParse(string information)
{
    return information.StartsWith('_')
        || IsPositionWeather(information);
}
```

**How it finds the weather symbol:** For position weather, the giveaway is that `_` symbol code, but its location in the string depends on whether the position is compressed or uncompressed (two different encodings) and whether the report carries a timestamp. **IsPositionWeather** computes exactly the right offset for each case before checking for the `_`:

```csharp
var symbolCodeIndex = AprsCompressedPositionDecoder.IsCompressed(information, positionStart)
    ? positionStart + CompressedSymbolCodeOffset
    : positionStart + UncompressedSymbolCodeOffset;

return information.Length > symbolCodeIndex && information[symbolCodeIndex] == '_';
```

The offsets are named constants — **UncompressedSymbolCodeOffset** is 18 (8 latitude chars + 1 symbol-table char + 9 longitude chars), **CompressedSymbolCodeOffset** is 9 — because these fixed-column arithmetic values are exactly the kind of thing that breaks silently if someone fat-fingers a number. Naming them makes the intent auditable.

**How it reads the fields:** Once positioned at the weather body, **ParseWeatherBody** walks it field by field. Each reading is a tag letter followed by a fixed number of digits — `s` for wind speed (3 digits), `g` for gust (3), `t` for temperature (3, or 4 if negative), `h` for humidity (2), `b` for barometric pressure (5), and so on. A `switch` on the tag handles each:

```csharp
case 'h':
    if (TryReadUnsigned(body, index, 2, out humidityPercent))
    {
        humidityPercent = humidityPercent == 0 ? 100 : humidityPercent;
        index += 2;
        parsedFieldCount++;
        break;
    }
    validationErrors.Add("Weather humidity is invalid.");
    index = fieldStart;
    return Finish(body, index, parsedFieldCount, validationErrors);
```

Two design details in this one case repay attention. First, the `humidityPercent == 0 ? 100` line: APRS encodes 100% humidity as `h00`, because the field is only two digits wide and "100" would not fit — so a literal zero is really one hundred. Miss this and every rain-soaked hilltop reads as bone dry. Second, when a field fails to parse, the code does not throw or guess; it rewinds the index to the start of the bad field (`index = fieldStart`) and calls **Finish**, treating everything from that point on as free-text comment. This is deliberate leniency: real transmitters send imperfect data, and a plausible partial weather report is far more useful than a discarded one.


### Telemetry: Numbers Now, Meaning Later

**What it does:** **AprsTelemetryParser** handles sensor data — a station reporting up to five analog readings and eight on/off (digital) bits, plus separate *metadata* packets that say what those numbers mean (their names, units, and scaling formulas).

**Why the split matters:** A telemetry value packet is deliberately tiny — just `T#` followed by a sequence number and comma-separated numbers — because it is sent constantly. The human-readable labels ("channel 1 is battery voltage in volts") are sent rarely, as separate PARM./UNIT./EQNS./BITS. metadata packets. Splitting definition from data is how APRS keeps the frequent packet small; the parser mirrors that split with **ParseTelemetryValues** and **ParseMetadata**.

```csharp
public bool CanParse(string information)
{
    return information.StartsWith("T#", StringComparison.Ordinal)
        || MetadataPrefixes.Any(prefix => information.StartsWith(prefix, StringComparison.Ordinal))
        || TryGetMessageEmbeddedMetadata(information, out _);
}
```

**How it reads values:** The value form splits on commas, treats the first field as the sequence number, takes up to five analog values, and — if present — reads the seventh field as a string of `0`/`1` digital bits:

```csharp
var body = rawPacket.Information[2..];
var components = body.Split(',', StringSplitOptions.None);
...
foreach (var valueText in components.Skip(1).Take(5))
{
    if (!int.TryParse(valueText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var analogValue))
    {
        validationErrors.Add("Telemetry analog value is invalid.");
        continue;
    }
    analogValues.Add(analogValue);
}
```

The `CultureInfo.InvariantCulture` is a small but important guard: it forces number parsing to use a fixed, machine-neutral convention regardless of the operator's regional settings, so a period-versus-comma locale difference can never corrupt a reading. A single bad value is logged and skipped rather than aborting the whole packet — the same lenient instinct as the weather parser.


### Messages: Text, Acks, Rejects, and Bulletins

**What it does:** **AprsMessageParser** decodes person-to-person text messages and their close relatives: *acknowledgements* (proof a message arrived), *rejects*, *bulletins* (broadcast notices), and *announcements*.

**How it works:** A message packet is `:` then a nine-character addressee, then a second `:`, then the body. The parser slices the fixed-width addressee, verifies the separator, and pulls the body:

```csharp
addressee = rawPacket.Information.Substring(1, AddresseeLength).TrimEnd();

if (rawPacket.Information[AddresseeLength + 1] != ':')
{
    validationErrors.Add("Message packet is missing second ':' body separator.");
}
else
{
    rawMessageBody = rawPacket.Information[(AddresseeLength + 2)..];
}
```

A message may end with a *message ID* introduced by `{`, which lets the sender ask for an acknowledgement. The parser finds the *last* `{` (the sender's own text might contain an earlier one) and splits the body from the ID there. It then checks whether the body itself is an ack or reject.

**Bulletins** are messages whose addressee starts with `BLN`. The character immediately after `BLN` decides the sub-type, and the parser is careful about exactly which character it trusts:

```csharp
var identifier = addressee[3];
bulletinId = identifier.ToString();
isAnnouncement = char.IsLetter(identifier);
if (!isAnnouncement && addressee.Length > 4)
{
    var group = addressee[4..].TrimEnd();
    bulletinGroup = group.Length > 0 ? group : null;
}
```

Per the spec's section 14, a *digit* after `BLN` marks a general or group bulletin, while a *letter* marks an announcement. Only that one character decides. As the code's comment warns, a group name that happens to contain letters — like `BLN1WX` — must not be misread as an announcement, because the deciding character (`1`) is a digit. This is a subtle classification trap, and the parser gets it right by looking only at position 3.


### Common DNA Across All Five

Read together, the five parsers share a set of habits worth naming, because a future maintainer adding a sixth parser should copy them.

| Parser | Starts with | The genuinely tricky bit |
| --- | --- | --- |
| MIC-E | 0x60 / 0x27 / 0x1C / 0x1D | Position split between destination and info, packed with status bits; all-zero status = Emergency |
| Object / Item | ; or ) | Fixed-width 9-char name and live/killed flag (object); first-of-two separators (item) |
| Weather | _ or a position with '_' symbol | Two shells (position vs positionless); h00 means 100%; compressed wind hides in position bytes |
| Telemetry | T# or PARM./UNIT./EQNS./BITS. | Values and their meaning arrive in separate packets; metadata can be message-wrapped |
| Message | : | Case-sensitive ack/rej; bulletin sub-type decided by one character after BLN |

First, every parser exposes the same **CanParse** / **Parse** pair, so the owning **AprsParser** can treat them uniformly — a consistent shape that makes the dispatch code readable and the parsers swappable. Second, none of them throw exceptions on bad input; they collect human-readable strings in a `validationErrors` list and press on, producing the best partial result they can. Real radio data is noisy, and a decoder that gives up at the first oddity would be useless. Third, all fixed-column offsets are named constants, never bare numbers, because column arithmetic is where silent parsing bugs breed.


## Why It Matters / Design Takeaways

The reasoning a future maintainer must preserve comes down to a few principles. **One format, one parser.** Each of these formats is complex enough that mixing them would multiply the ways things break; the separation is what makes each file understandable and independently testable.

**Follow the reference implementations, especially for the bit-twiddly formats.** MIC-E and compressed weather are error-prone enough that matching APRS 1.2 and Dire Wolf is more valuable than any clever shortcut. The inline comments citing the spec sections and WB4APR's PROTOCOL.TXT are not decoration — they are the receipts that let a maintainer verify the code against the source of truth.

**Be lenient with real-world data, strict with protocol keywords.** The parsers happily accept partial or slightly malformed packets and downgrade gracefully to comments and validation notes — because hams' equipment sends imperfect data every day. But where a keyword carries protocol meaning (`ack`, `rej`, the `_` kill flag, the Emergency status), they are exact and case-sensitive, because a loose match there would silently corrupt meaning. Knowing which situations call for tolerance and which call for precision is the single most important judgement encoded in this chapter's code.

**Name the magic numbers.** Every fixed-column offset and length in these formats is a named constant. When the next person needs to fix a one-character misalignment in a weather report, the difference between a named `UncompressedSymbolCodeOffset = 18` and a bare `18` buried in an expression is the difference between a five-minute fix and an afternoon lost.


# 8. Transports: APRS-IS, KISS, Direwolf, and AGWPE

*How raw APRS packets travel in and out of APRS-Command over the internet and over the air, through one shared contract and four purpose-built clients.*


## What This Is / What It Is For

APRS-Command's job is to move little bursts of text called *packets* — a ham radio operator's position, a status message, a weather report — between your computer and the wider world. But there is no single 'world' to plug into. Sometimes the packets ride the public internet. Sometimes they go out over a real radio, through a box that turns text into sound and back. Sometimes that box is a physical gadget on a USB cable; sometimes it is a piece of software pretending to be one. The **Transports** layer is the collection of adapters that speaks each of these links so the rest of the program does not have to.

Think of it like the mail. You have a letter to send. It does not care whether it leaves your house by the post office, a courier van, or a friend driving across town — it just needs to get there, and replies need to get back. A *transport* is one of those delivery routes. This chapter walks through the four routes APRS-Command supports: the **APRS-IS** internet backbone, and three ways of reaching a radio — **TCP KISS**, **Serial KISS**, and **AGWPE** — plus **Direwolf**, which is really TCP KISS wearing a friendlier label.

```csharp

```

> **Where this lives** — Everything in this chapter is in the `Aprs.Transport` project (folder `src/Aprs.Transport`). The real classes are `AprsIsClient`, `TcpKissClient`, `SerialKissClient`, and `AgwpeClient`, sitting behind the interfaces `IAprsIsClient`, `ITcpKissClient`, `ISerialKissClient`, and `IAgwpeClient`.


### The common contract: what every transport promises

There is a deliberately tiny shared interface that captures the essence of 'a delivery route,' called **IAprsTransport**. It is the smallest possible promise a transport can make.

```csharp
public interface IAprsTransport : IAsyncDisposable
{
    string Name { get; }
    IAsyncEnumerable<string> ReadPacketsAsync(CancellationToken cancellationToken);
    Task SendPacketAsync(string packet, CancellationToken cancellationToken);
}
```

Read that as three sentences. `Name` — tell me what you are, so a human can see it. `ReadPacketsAsync` — hand me a never-ending stream of incoming packets as plain text, one at a time, and let me stop listening whenever I pass you a *cancellation token* (a small object whose only job is to signal 'stop now'). `SendPacketAsync` — take this one packet and get it out onto your link. The `IAsyncDisposable` part means 'when I'm done with you, you get a chance to clean up — close the socket, hang up the phone.'

`IAsyncEnumerable<string>` is worth a plain-words definition because it appears everywhere here. It is a *lazy stream you loop over with `await`*: a sequence whose items may not exist yet. The consumer writes `await foreach (var packet in transport.ReadPacketsAsync(token))` and simply waits, item by item, for as long as packets keep arriving — no polling, no busy-waiting, no callback spaghetti.

> **The reality is richer than the contract** — In practice each transport does NOT just implement the three-method `IAprsTransport`. Each has its own fuller interface — `IAprsIsClient`, `ITcpKissClient`, and so on — that adds a live connection **State**, a **LastError**, connect/disconnect control, C# **events** that fire on each arrival, and a **transmit result** object describing exactly what happened. The minimal `IAprsTransport` is the lowest common denominator; the per-transport interfaces are what the app actually wires up, because a radio link genuinely needs more knobs than an idealized 'send a string' abstraction admits.

Why keep the minimal interface at all, then? Because it names the shared shape. Anyone reading the code sees instantly that these four very different clients are the same kind of thing, and any future code that only needs 'get me packets, send me packets' can lean on the small contract and ignore the differences.


### The shape every client shares

Before diving into each link, it pays to notice that all four clients are built to the same blueprint. Learn it once and every client reads the same way. Here is the per-transport interface for TCP KISS as a representative example.

```csharp
public interface ITcpKissClient : IAsyncDisposable
{
    event EventHandler<KissFrameReceivedEventArgs>? FrameReceived;
    event EventHandler<TcpKissRawPacketReceivedEventArgs>? RawPacketReceived;
    TcpKissConnectionState State { get; }
    Exception? LastError { get; }
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<KissFrame> ReadFramesAsync(CancellationToken cancellationToken);
    IAsyncEnumerable<TcpKissRawPacketReceivedEventArgs> ReadPacketsAsync(CancellationToken cancellationToken);
    Task<TcpKissTransmitResult> SendFrameAsync(int portNumber, KissCommandType commandType,
        IReadOnlyList<byte> ax25Payload, bool transmitConfirmed, CancellationToken cancellationToken);
}
```

The recurring pieces, in plain terms: **State** is a traffic light — Disconnected, Connecting, Connected, Reconnecting, or Faulted. **LastError** is the last thing that went wrong, kept around so the UI can show it instead of crashing. The two **events** are doorbells: `RawPacketReceived` rings with a ready-to-use text packet, while the lower-level `FrameReceived` rings with the raw structured frame for anyone who wants the bytes. **ReadPacketsAsync** is the same information delivered as a stream you can `await foreach` over. And **SendFrameAsync** returns not a bare success/failure but a full *result record* — a small immutable report — saying whether it went, when, from what connection state, and if it failed, why.

Why offer both events and a stream for the same data? Because they suit different consumers. A viewmodel that wants to react instantly (flash an icon) subscribes to the event; a background pipeline that wants to process packets in an orderly queue reads the stream. Both are fed from one internal buffer, so nothing is duplicated or lost.


## APRS-IS: the internet backbone

**APRS-IS** (the APRS Internet System) is a worldwide network of servers that relay APRS packets over the internet. It is the easiest link to use because it needs no radio at all — just a TCP connection to a server and a login line of plain text. `AprsIsClient` is the client that speaks it.

The protocol is charmingly simple: you open a socket, send one line identifying yourself, and then the server streams you packets as text, one per line, forever. Lines that begin with `#` are the server talking to you (comments and status), not real packets. Here is the login line the program constructs.

```csharp
var loginLine = $"user {configuration.Callsign.Trim()} pass {configuration.Passcode.Trim()} " +
                $"vers {configuration.ApplicationName.Trim()} {configuration.ApplicationVersion.Trim()}";
if (!string.IsNullOrWhiteSpace(configuration.Filter))
{
    loginLine += $" filter {configuration.Filter.Trim()}";
}
```

In English: 'I am callsign X, here is my passcode, I am running APRSCommand version Y, and please only send me packets matching this filter.' The *passcode* is a number derived from your callsign — not a secret password, just a light gate proving you own the call. A passcode of `-1` means 'receive only, I will never transmit,' which is exactly the safe default the program ships with (`ReceiveOnly: true`, `TransmitEnabled: false` in `AprsIsClientConfiguration.Default`). The *filter* is a request like 'only stations within 100 km of me,' so you are not firehosed with the entire planet's traffic.


### The logresp handshake: waiting for the green light

A subtle but important detail: after you send your login line, the server has not yet accepted you. If you blurt out a packet immediately, the server silently throws it away. So the client waits for a specific acknowledgment line before it declares itself Connected.

```csharp
// Wait for the server's logresp line before marking Connected.
// APRS-IS sends "# logresp CALLSIGN verified, server ..." or "unverified".
// Packets sent before this acknowledgment are silently discarded.
await WaitForLogrespAsync(opened, connectionCancellation.Token).ConfigureAwait(false);
SetState(AprsIsConnectionState.Connected);
```

The wait is capped at five seconds. If the server is slow or non-standard and never sends a clean `# logresp`, the client proceeds anyway rather than hanging forever — the comment in the code spells out that reasoning: 'better to attempt transmit than to hang.' One more careful touch: any real packets that arrived in the same network read as the logresp are not thrown away. They are stashed in a `pendingData` string and replayed into the receive loop the moment it starts, so nothing that arrived early is lost.

> **Failover built in** — `AprsIsClientConfiguration` carries an optional `FailoverServers` list and an `AllServers()` helper that returns the primary first, then the backups. The default primary is `rotate.aprs2.net:14580`, itself a rotating DNS name that hands out a different healthy server each time — so resilience is layered: rotating DNS, plus an explicit backup list.


### Reconnecting without leaking

Radios and internet links drop. The receive loop is written to survive that. When the stream ends (a read returns zero bytes), and reconnect is enabled, it waits a configured delay, disposes the dead stream, opens a fresh one, re-sends the login line, waits for logresp again, and carries on. The disposal is not an afterthought — the comment flags exactly why it matters: skipping it 'leaks the previous NetworkStream/socket' on every reconnect, which over days of running would exhaust the machine.


## KISS and TNCs: getting onto the air

To send APRS over an actual radio you need a *TNC* — a Terminal Node Controller. In plain terms a TNC is a modem for ham radio: it turns your text packet into the warbling tones the radio transmits, and turns received tones back into text. It can be a small hardware box, or a program like Direwolf running on the same computer.

But your computer and the TNC need an agreed way to hand bytes back and forth over a wire (or a socket). That agreement is *KISS* — 'Keep It Simple, Stupid.' KISS is a wrapping, not a language: it does not care what the packet says, it just marks where each frame begins and ends so neither side gets confused about where one message stops and the next starts. `KissFrameCodec` is the little library that does this wrapping and unwrapping.


### Frame markers and escaping

KISS uses one special byte, `0xC0`, called *FEND* ('frame end'), as a fence post at the start and end of every frame. That raises an obvious problem: what if the packet data itself contains a `0xC0` byte? Then the receiver would think the frame ended early. KISS solves this with *escaping* — a substitution trick, the same idea as typing `\"` inside a quoted string so the quote does not end the string.

```csharp
foreach (var value in payload)
{
    switch (value)
    {
        case Fend:   // 0xC0 — would look like a frame boundary
            encoded.Add(Fesc);   // 0xDB, "an escape follows"
            encoded.Add(Tfend);  // 0xDC, "...it was really a FEND"
            break;
        case Fesc:   // 0xDB — the escape byte itself must be escaped too
            encoded.Add(Fesc);
            encoded.Add(Tfesc);  // 0xDD, "...it was really a FESC"
            break;
        default:
            encoded.Add(value);
            break;
    }
}
```

So a real `0xC0` in the data becomes the two bytes `0xDB 0xDC`, and a real `0xDB` becomes `0xDB 0xDD`. The receiver reverses this in `DecodePayload`. The codec is defensive on the way back in: an escape byte with nothing after it, or an escape followed by a byte that is neither of the two legal follow-ups, is recorded as a validation error rather than silently mangled — a `KissFrame` carries its own list of `ValidationErrors` and an `IsValid` flag.


### The command byte: which port, what kind

The byte right after the opening FEND packs two things into its two halves (its 'nibbles'): which of up to 16 radio ports the frame is for, and what kind of command it is.

```csharp
var commandByte = (byte)(((portNumber & 0x0F) << 4) | ((int)commandType & 0x0F));
```

The `KissCommandType` enum lists the kinds — `DataFrame` (0) is an actual packet; the rest (`TxDelay`, `Persistence`, `SlotTime`, and so on) are tuning settings for the TNC's timing. Only `DataFrame` frames are decoded into APRS text; the codec calls the `Ax25AprsPayloadDecoder` to peel the packet out of its lower-level wrapping.


### Streaming reassembly: frames don't arrive whole

A crucial real-world detail: bytes arrive from a socket or serial port in arbitrary chunks, not tidy frames. Half a frame may come in one read, the rest in the next; or three frames may arrive glued together. The receive loops handle this by keeping a running `pending` buffer and asking the codec where the last *complete* frame ends before decoding.

```csharp
pending.AddRange(readBuffer.Take(bytesRead));
var lastCompleteEnd = KissFrameCodec.FindLastCompleteFrameEnd(pending);
if (lastCompleteEnd < 0)
{
    continue; // nothing complete yet — wait for more bytes
}
var completeBytes = pending.Take(lastCompleteEnd + 1).ToArray();
pending.RemoveRange(0, lastCompleteEnd + 1); // keep the leftover partial frame
```

This is the standard, robust way to read a *stream protocol* (one with no message boundaries of its own): buffer, find complete units, process them, and hold the remainder for next time. All three radio clients — TCP KISS, Serial KISS, and AGWPE — use exactly this pattern, which is why fixing a bug in one usually means checking the other two.


### AX.25: the packet inside the frame

Inside a KISS data frame is not raw APRS text but an *AX.25 UI frame* — the actual over-the-air radio packet format, named after the amateur adaptation of the X.25 networking standard. 'UI' means 'Unnumbered Information': a fire-and-forget broadcast with no acknowledgment, which is exactly what APRS wants. `Ax25AprsFrameEncoder` builds these, and `Ax25AprsPayloadDecoder` reads them. The header comment lays out the layout cleanly.

```csharp
// TNC2 format:  SOURCE>DEST,PATH1,PATH2:information
// AX.25 UI frame layout:
//   [dest 7 bytes][source 7 bytes][digi1 7]...[digiN 7][control 1][pid 1][info N]
private const byte ControlUi   = 0x03;   // UI frame
private const byte PidNoLayer3 = 0xF0;   // No layer 3 protocol (standard APRS)
```

Each address is seven bytes: six for the callsign and one for the *SSID* (the `-9` in `KE4CON-9`, distinguishing your handheld from your base station). A quirk worth knowing: callsign letters are each shifted left by one bit on the wire, so the decoder shifts them back with `(char)(value >> 1)`. The last address field flips its lowest bit to 1 to mark 'no more addresses follow.' The decoder walks addresses by that end bit, then insists the control byte is `0x03` and the PID is `0xF0` before trusting the frame — anything else is flagged as 'not a UI frame' or 'not a no-layer-3 APRS payload.'

> **A friendly fallback** — `Ax25AprsPayloadDecoder` first checks `LooksLikeAprsText` — if the payload is all printable characters and contains both `>` and `:`, it accepts it as APRS text directly. Some test tools and simple software TNCs send plain text rather than a full AX.25 frame, and this keeps them working without special cases.


## TCP KISS, Serial KISS, and Direwolf

There are two ways to reach a KISS TNC, and they differ only in the wire underneath. **TCP KISS** (`TcpKissClient`) reaches a TNC over a network socket — host and port. **Serial KISS** (`SerialKissClient`) reaches a TNC over a physical serial/USB cable — a COM port, a baud rate, data bits, parity, stop bits, and a handshake mode (`SerialKissHandshake`: None, XOn/XOff, RTS, or both). Above that difference they are near-identical twins: same KISS codec, same reassembly loop, same channel buffering, same transmit-result records.

The one meaningful behavioral difference is a safety flag. Serial KISS talks to hardware that is very likely wired straight to a transmitter, so its `SendFrameAsync` takes an extra `rfSafetyEnabled` argument and refuses to send without it.

```csharp
if (!configuration.TransmitEnabled)
    return "Serial KISS transmit is disabled.";
if (!rfSafetyEnabled)
    return "RF transmit safety settings are not enabled.";
if (!transmitConfirmed)
    return "Serial KISS transmit confirmation is required.";
```


### Direwolf is TCP KISS in a nicer coat

*Direwolf* is a popular free software TNC: it uses your computer's sound card as the modem, so you need no hardware TNC at all. From APRS-Command's point of view Direwolf is just a KISS TNC listening on a TCP port (8001 by default). So there is no separate 'Direwolf client' — instead there is a `DirewolfProfile`, a friendly named bundle of settings, and a service that converts it into a plain `TcpKissConfiguration`.

```csharp
public TcpKissConfiguration ToTcpKissConfiguration(DirewolfProfile profile)
{
    return TcpKissConfiguration.Default with
    {
        Host = profile.Host.Trim(),
        Port = profile.KissPort,
        Enabled = profile.Enabled,
        ReconnectEnabled = profile.AutoReconnect,
        ReceiveEnabled = profile.ReceiveEnabled,
        TransmitEnabled = profile.TransmitEnabled,
        SourceName = string.IsNullOrWhiteSpace(profile.SourceName) ? "Direwolf" : profile.SourceName.Trim()
    };
}
```

This is a deliberate design choice worth calling out: rather than duplicate a whole client for Direwolf, the code recognizes that Direwolf *is* TCP KISS and simply adapts the friendlier profile onto the existing transport. Less code, fewer bugs, one reassembly loop to maintain. There is also a `DirewolfConnectionTestService` that pokes the host and port with a short probe (3-second default timeout) so a user can click 'Test' in setup and get a clear success or failure before committing.

| Transport | Underlying link | Frame format | Extra safety knob |
| --- | --- | --- | --- |
| APRS-IS | TCP to internet server | Plain text lines | Passcode + confirmation |
| TCP KISS | TCP to a TNC | KISS-wrapped AX.25 | Transmit confirmation |
| Serial KISS | Serial/USB cable to a TNC | KISS-wrapped AX.25 | Confirmation + RF safety flag |
| Direwolf | TCP KISS (adapter over sound-card TNC) | KISS-wrapped AX.25 | Transmit confirmation |
| AGWPE | TCP to AGW packet engine | AGWPE 36-byte binary frames | Confirmation + RF safety flag |


## AGWPE: a different binary language

The last transport, `AgwpeClient`, speaks to an **AGW Packet Engine** — another software TNC family (SoundModem, AGWPE itself) with its own wire format. Unlike KISS's byte-stuffed frames, AGWPE uses a fixed **36-byte header** followed by a payload whose length is written into the header, in *little-endian* order (least significant byte first — the ordering Intel-family CPUs use natively).

```csharp
var radioPort   = raw[0];
var commandType = (char)raw[4];
var source      = DecodeCallsign(raw, 8, 10);
var destination = DecodeCallsign(raw, 18, 10);
var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(28, 4));
```

Because the length is read straight off the wire, the codec cannot trust it — a corrupt or hostile stream could claim a wildly large or negative length. The decoder clamps it hard, and the comment explains the real danger being prevented.

```csharp
// Reject a negative length and any length larger than the bytes we actually have. A single
// frame's payload can never exceed the received buffer, and clamping here also prevents the
// HeaderLength + payloadLength additions below from overflowing on a hostile length.
if (payloadLength < 0 || payloadLength > raw.Length)
{
    errors.Add("AGWPE frame payload length is invalid.");
    payloadLength = 0;
}
```

> **The infinite-loop trap this avoids** — In `DecodeMany` and `FindLastCompleteFrameEnd`, an unchecked oversize length would make `HeaderLength + length` overflow to a negative number, which would march the read offset *backwards* and spin the loop forever — a hang, or a denial-of-service from one bad packet. Bounding the length by the buffer size is what keeps a single corrupt frame from freezing the receive thread. This is a genuine hardening fix, not decoration.

On transmit, AGWPE frames are built with command character `'K'` (send raw AX.25 data), the selected radio port, and the source/destination pulled from the parsed packet. Received data frames are recognized by command characters `'K'`, `'U'`, or `'D'`. It is a different dialect from KISS, but the client wraps it in the same familiar outer shape — state, events, channels, reconnect — so it slots into the app identically.


## The safety gauntlet and the global inhibit

Transmitting on a ham radio is a licensed, regulated act, and an accidental transmission — during a drill, or from a half-configured setup — is a real harm. Every radio-capable transport therefore runs each send request through a validation gauntlet *before a single byte reaches the wire*. AGWPE's is representative.

```csharp
if (!configuration.TransmitEnabled)
    return "AGWPE transmit is disabled.";
if (!transmitConfirmed)
    return "AGWPE transmit confirmation is required.";
if (!rfSafetyEnabled)
    return "AGWPE transmit requires RF transmit safety to be explicitly enabled.";
if (stateAtRequest != AgwpeConnectionState.Connected || stream is null)
    return "AGWPE client is not connected.";
```

Each check returns a plain-English reason string, and any non-null reason turns the whole send into a *failed result* — never an exception, never a silent drop. The layered gates are intentional: transmit is off by default, must be enabled, must be individually confirmed at the moment of sending, and for the RF paths must also have an explicit safety flag set. It takes several deliberate 'yes' answers to key up a radio.


### One switch that overrides everything

Above all those per-transport checks sits a single master kill-switch, the **ITransmitInhibitGate**. It exists so that one global condition — most importantly *exercise mode*, a practice drill where nothing should ever actually go on the air — can hard-block every transmit path at once, no matter which feature (beacon, message, weather, iGate) asked to send.

```csharp
var gate = InhibitGate;
if (gate is not null && gate.IsTransmitInhibited)
{
    return AprsIsTransmitResult.Failed(
        timestamp, normalizedPacket, stateAtRequest,
        gate.InhibitReason ?? "Transmit is globally inhibited (exercise mode).");
}
```

In `AprsIsClient` this check runs *first*, before any other validation, so 'a drill can never key up APRS-IS by any path.' The gate interface deliberately lives in the transport layer, not in Services, so the lowest-level transmit code can consult it without depending upward on higher layers. The Services-layer safety authority implements the interface, and the composition root hands the same authority to every client — meaning there is no side door: no transport can be constructed that quietly forgot the inhibit check.


## The engineering patterns worth copying

Three patterns recur across all four clients, and understanding them once explains the shape of every transport in the codebase.

*Injected stream factories.* None of the clients open their own socket directly in the normal code path. Each takes a `Func<Configuration, CancellationToken, Task<Stream>>` — a small function that produces the connection — with a real TCP implementation supplied by default. The plain-words payoff: in tests you hand the client a fake stream and drive it with canned bytes, so the entire frame-parsing and reconnect logic can be tested with no network and no radio at all.

```csharp
public AprsIsClient(AprsIsClientConfiguration configuration)
    : this(configuration, CreateTcpStreamAsync) { }

public AprsIsClient(AprsIsClientConfiguration configuration,
    Func<AprsIsClientConfiguration, CancellationToken, Task<Stream>> streamFactory)
{
    this.configuration = configuration;
    this.streamFactory = streamFactory;   // tests pass a fake here
}
```

*Channels as an internal mailbox.* Each client owns an unbounded `Channel<T>` — a thread-safe producer/consumer queue. The receive loop writes each decoded item into the channel; `ReadPacketsAsync` reads from it. This cleanly separates the background thread pulling bytes off the wire from the foreground code consuming packets, with no shared state to corrupt and no locks in the hot path.

*Snapshot-under-lock for reconnects.* The TCP KISS, Serial KISS, and APRS-IS clients guard their mutable connection and state with a small `sync` lock, and — critically — never hold that lock across an `await`. The send path takes an atomic `Snapshot()` of the current stream and state, then does its I/O on that snapshot. The comment states the exact hazard: without it, a concurrent reconnect could swap the stream out 'between the write and the flush.' It is a textbook example of holding a lock for the shortest possible moment while still being correct under concurrency.

```csharp
var (active, stateAtRequest) = Snapshot();  // consistent view, taken atomically
// ... validate against that snapshot ...
await active!.WriteAsync(encoded, cancellationToken).ConfigureAwait(false);
await active.FlushAsync(cancellationToken).ConfigureAwait(false);
```

> **One inconsistency to know about** — `AgwpeClient` uses simple auto-properties for `State` and `LastError` and a plain `stream` field rather than the lock-and-snapshot discipline of the other three clients. It works for the common single-threaded-caller case, but it is the odd one out — worth remembering if you ever chase a rare reconnect race on the AGWPE path specifically.


## Why It Matters / Design Takeaways

The Transports layer earns its keep by turning five genuinely different ways of moving packets — a plain-text internet feed, byte-stuffed KISS over TCP, the same KISS over a serial cable, a sound-card TNC via Direwolf, and AGWPE's binary frames — into a handful of interchangeable clients that the rest of APRS-Command can treat almost identically. The differences that genuinely matter (a passcode here, an RF-safety flag there, a binary header versus escaped bytes) are preserved; the differences that don't (state tracking, buffering, reconnection) are solved once and reused.

The through-line is respect for two hard realities: streams don't arrive in tidy messages, so every reader buffers and reassembles; and transmitting on a radio is consequential, so every send runs a gauntlet of explicit gates topped by a single global inhibit that no code path can bypass. Injected stream factories make all of it testable without hardware, and the snapshot-under-lock pattern keeps it correct while connections drop and rebuild underneath. If you extend the program with a new link tomorrow, the blueprint is already here to copy.


# 9. Async, Streams & Back-pressure

*How APRS-Command stays responsive while waiting on slow radios and network feeds, and how packets flow from a socket to the screen without anyone blocking.*


## What This Is / What It Is For

APRS-Command spends almost all of its life *waiting*. It waits for a packet to arrive over the internet from the APRS-IS network. It waits for bytes to trickle in over a USB cable from a radio modem. It waits for a TCP connection to a Direwolf software modem to complete. Waiting is the normal state of a radio-monitoring program, because radio traffic is sporadic — a burst of packets, then thirty seconds of silence, then another burst.

The whole challenge of this chapter is a single question: *how does the app wait for slow things without freezing?* If the program simply sat and stared at the network socket until a packet showed up, the map would stop responding, buttons would stop clicking, and the window would go gray with 'Not Responding.' The machinery in this chapter is what lets the app wait patiently in the background while the user keeps panning the map and clicking around, perfectly smoothly.

Three ideas do all the work, and this chapter defines each in plain language: *async/await* (waiting without blocking), the *Channel* (a conveyor belt between the part that reads packets and the part that displays them), and *back-pressure* (what happens when packets arrive faster than they can be handled). The real code lives in three nearly-identical transport clients — `AprsIsClient`, `TcpKissClient`, and `SerialKissClient` in the **Aprs.Transport** project — plus the `LiveDataCoordinator` that hands their output to the screen.

> **The three transports, one shape** — APRS-Command talks to the outside world three ways: the internet APRS-IS feed (AprsIsClient), a TCP link to a software modem like Direwolf (TcpKissClient), and a serial/USB cable to a hardware TNC (SerialKissClient). All three follow the exact same async + Channel pattern, so once you understand one, you understand all three. This chapter uses AprsIsClient as the main example and notes where the others differ.


### Async in plain words: a waiter, not a wall

*Async* (short for asynchronous) is a way of writing code that says 'start this slow thing, and let me do other work until it finishes' instead of 'do this slow thing and make everyone stand still until it's done.' The everyday analogy is a good restaurant waiter. A *blocking* waiter would take your order, walk to the kitchen, and stand frozen at the pass until your food is cooked — serving no one else the whole time. An *async* waiter takes your order, hands it to the kitchen, and immediately goes to serve other tables; when your food is ready, they come back and deliver it. Same waiter, vastly more gets done, and nobody sits staring at a motionless employee.

In C#, the two keywords that express this are `async` and `await`. Marking a method `async` means 'this method contains waiting points.' Writing `await` in front of a slow operation means 'pause here *without* holding anyone hostage — release the thread to do other work, and resume this exact spot when the result is ready.' The critical word is *release*. A blocking wait keeps its thread; an `await` gives the thread back.

You can see this in the very first line of real network work in the app. When the APRS-IS client opens a connection to a server, it writes:

```csharp
private static async Task<Stream> CreateTcpStreamAsync(
    AprsIsClientConfiguration configuration,
    CancellationToken cancellationToken)
{
    var tcpClient = new TcpClient();
    await tcpClient.ConnectAsync(configuration.ServerHost, configuration.ServerPort, cancellationToken)
        .ConfigureAwait(false);

    return tcpClient.GetStream();
}
```

Line by line: a `TcpClient` is .NET's helper for a network connection. `ConnectAsync` reaches across the internet to the APRS-IS server — an operation that can take a fraction of a second or, on a bad connection, several seconds. The `await` in front of it means the app does not freeze during that reach-out; the thread is free to do anything else, and execution resumes at `return tcpClient.GetStream()` only once the handshake completes. A `Stream` is .NET's generic word for 'a pipe of bytes you can read from and write to' — here, the open socket to the server.

> **Why every await ends in .ConfigureAwait(false)** — You will see .ConfigureAwait(false) on nearly every await in the transport layer. In plain terms it says: 'when this finishes, resume on any available background thread — you do NOT need to come back to the special UI thread.' Library and background code adds it as a discipline: it avoids needlessly dragging work back onto the UI thread and sidesteps a classic freeze-the-app deadlock. The UI-facing code (like AsyncDesktopCommand) deliberately does the opposite with ConfigureAwait(true), because it DOES need to be back on the UI thread afterward.


### ConnectAsync: opening the link without stalling

*What it does:* `ConnectAsync` opens the connection to a radio or network feed and, once open, kicks off a background reader that will listen for incoming packets forever. *Why it is built this way:* opening a connection and then reading from it are both slow, waiting-heavy jobs, so both are async, and the never-ending read loop is pushed onto a background task so it can run for hours without ever touching the responsive part of the app.

Here is the heart of `AprsIsClient.ConnectAsync`:

```csharp
connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
lock (sync) { lastError = null; state = AprsIsConnectionState.Connecting; }

try
{
    var opened = await streamFactory(configuration, connectionCancellation.Token).ConfigureAwait(false);
    SetStream(opened);
    await WriteLoginLineAsync(opened, connectionCancellation.Token).ConfigureAwait(false);

    // Wait for the server's logresp line before marking Connected.
    await WaitForLogrespAsync(opened, connectionCancellation.Token).ConfigureAwait(false);

    SetState(AprsIsConnectionState.Connected);
    receiveTask = Task.Run(() => ReceiveLoopAsync(connectionCancellation.Token), CancellationToken.None);
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    Fault(exception);
    throw;
}
```

This reads almost like a checklist. First it opens the byte-pipe (`streamFactory` is the `CreateTcpStreamAsync` shown earlier — passed in as a plug-in so tests can substitute a fake stream). Then it writes the APRS-IS login line and *waits for the server to acknowledge it* (`WaitForLogrespAsync`) — a real-world detail, because APRS-IS silently throws away anything you send before it says 'you're verified.' Only after the acknowledgment does the client flip its state to `Connected`.

The pivotal line is the last one inside the `try`:

```csharp
receiveTask = Task.Run(() => ReceiveLoopAsync(connectionCancellation.Token), CancellationToken.None);
```

`Task.Run` says 'start this running on a background thread and *don't wait for it.*' `ReceiveLoopAsync` is an infinite loop that reads packets until the connection closes — if the code had written `await ReceiveLoopAsync(...)`, then `ConnectAsync` would never return, because the loop never ends. Instead it hands the loop off to the background and stores a handle to it (`receiveTask`) so it can be cleanly shut down later. This is the seam between 'connecting' (a one-time job that finishes) and 'receiving' (a forever job that runs quietly underneath everything).

> **CancellationToken: the shared 'stop' button** — A CancellationToken is a small object threaded through every async call so a single request to stop can ripple through all of them at once. Think of it as a whistle everyone is listening for: when DisconnectAsync blows it (connectionCancellation.Cancel()), the in-flight network read, the reconnect delay, and the receive loop all hear it and unwind together. Every async method in this layer accepts one, exactly as the project's coding standards require.


### The receive loop: a tireless background reader

*What it does:* `ReceiveLoopAsync` is the background worker that sits on the open connection, reads incoming text line by line, throws away the server's chatter, and publishes real APRS packets. *Why it is built this way:* reading from a network or radio is the single most 'waiting-heavy' thing the app does, so it is isolated on its own background task where it can block on reads all day without the user ever noticing.

The APRS-IS version reads whole lines of text:

```csharp
while (!cancellationToken.IsCancellationRequested)
{
    var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
    if (line is null)
    {
        break;
    }

    if (line.StartsWith('#'))
    {
        continue;
    }

    PublishPacket(line);
}
```

`await reader.ReadLineAsync(...)` is the key waiting point: it pauses here, releasing the thread, until a full line of text arrives from the server. A `null` line means the server hung up, so the loop breaks out (and, if reconnection is enabled, tries to reopen the connection after a delay). A line starting with `#` is an APRS-IS server comment — status chatter, not a real packet — so it is skipped. Everything else is a genuine packet and goes to `PublishPacket`.

The TCP-modem and serial versions differ only in the shape of the data. Because a KISS modem sends raw binary frames rather than tidy text lines, their loops read into a byte buffer and stitch fragments together until a complete frame boundary is found:

```csharp
var bytesRead = await active.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
if (bytesRead == 0) { /* connection closed → reconnect or stop */ }

pending.AddRange(readBuffer.Take(bytesRead));
var lastCompleteEnd = KissFrameCodec.FindLastCompleteFrameEnd(pending);
if (lastCompleteEnd < 0)
{
    continue;   // haven't received a whole frame yet — wait for more bytes
}
```

The idea is the same — `await ...ReadAsync` waits for bytes without freezing — but here the loop keeps a `pending` buffer of leftover bytes, because a single network read might contain half a packet, or two-and-a-half packets. It only pulls out *complete* frames and leaves the fragment behind for the next read to finish. This is the reality of stream I/O: bytes arrive in whatever chunks the network feels like delivering, and the reader is responsible for finding the real boundaries.


### Channel<T>: the conveyor belt in the middle

*What it does:* a `Channel<T>` is a thread-safe pipe — one part of the program drops items in at one end (the *writer*), and another part takes them out at the other end (the *reader*), and the two never have to know about each other or run at the same speed. *Why it is built this way:* the background receive loop and the on-screen consumer live on different threads running at different rhythms; a Channel is the safe, purpose-built handoff between them, so neither one has to reach into the other's world.

The real-world picture is a conveyor belt in a kitchen. The cook (the receive loop) plates dishes and sets them on the belt whenever they're ready. The server (the consumer) picks them off the belt whenever they're free. Neither has to stand and wait for the other; the belt absorbs the difference in pace. Each transport client declares one at the top of the class:

```csharp
private readonly Channel<AprsIsRawPacketReceivedEventArgs> receivedPackets
    = Channel.CreateUnbounded<AprsIsRawPacketReceivedEventArgs>();
```

`Channel.CreateUnbounded` makes a belt with *no length limit* — it will hold as many items as are dropped on it. (We will come back to why 'unbounded' is a deliberate and slightly risky choice, in the back-pressure section.) The `<AprsIsRawPacketReceivedEventArgs>` part says what rides on the belt: in this case, one received APRS packet plus the moment it arrived.

The receive loop puts packets on the belt in `PublishPacket`:

```csharp
private void PublishPacket(string line)
{
    var packet = new AprsIsRawPacketReceivedEventArgs(line, DateTimeOffset.UtcNow);
    receivedPackets.Writer.TryWrite(packet);
    RawPacketReceived?.Invoke(this, packet);
}
```

`receivedPackets.Writer.TryWrite(packet)` drops one packet onto the belt. It is called `TryWrite` because in the general case a bounded belt could be full and reject the item — but with an unbounded belt it always succeeds instantly and never blocks the receive loop. Notice the belt is not the only way out: the same method also fires a plain C# `event` (`RawPacketReceived`). The app therefore offers two ways to consume the same packets — a classic event for fire-and-forget subscribers, and the Channel for anyone who wants to pull packets in a controlled, awaitable loop. The `LiveDataCoordinator` uses the event; other consumers use the Channel.

> **Why a Channel and not just the event?** — An event calls its subscribers immediately, on whatever thread raised it — the background receive thread. That is fine for a quick hand-off, but it means the receiver runs inside the reader's thread and can slow the reader down. A Channel decouples them completely: the reader drops the packet and moves on instantly, and the consumer processes it later, on its own schedule and its own thread. The Channel is the tool that lets the fast reader and a possibly-slower consumer coexist without either one dragging on the other.


### ReadPacketsAsync: taking packets off the belt

*What it does:* `ReadPacketsAsync` is the reader end of the belt, exposed as a stream of packets that a consumer can loop over with `await foreach`. *Why it is built this way:* it turns 'packets arriving unpredictably over time' into something as easy to consume as reading a list — the consumer just loops, and the loop naturally pauses when the belt is empty and resumes when something shows up.

```csharp
public async IAsyncEnumerable<AprsIsRawPacketReceivedEventArgs> ReadPacketsAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    while (await receivedPackets.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
    {
        while (receivedPackets.Reader.TryRead(out var packet))
        {
            yield return packet;
        }
    }
}
```

This tiny method carries a lot of meaning. The return type `IAsyncEnumerable<...>` is C#'s term for *an asynchronous stream* — a sequence you can walk through with `await foreach`, where each step may involve waiting. The outer `await ...WaitToReadAsync` is the patient part: it pauses, releasing the thread, until at least one packet is available on the belt — no busy-spinning, no wasted CPU. When packets are present, the inner `while (...TryRead(...))` drains *every* item currently waiting, one by one, before looping back to wait again. `yield return` hands each packet to the consumer as it comes.

The two-loop shape is a small efficiency: rather than wake up, take one packet, and go back to sleep, it wakes up once and empties the whole belt in a tight loop, only sleeping again when the belt is truly empty. Under a burst of traffic this drains the backlog quickly; under silence it costs nothing. A consumer's use of it is disarmingly simple:

```csharp
await foreach (var packet in client.ReadPacketsAsync(token))
{
    // handle one packet
}
```

That loop looks like ordinary iteration over a collection, but it is really 'for each packet that ever arrives, whenever it arrives, until cancelled.' The complexity of waiting, threading, and buffering is entirely hidden behind those four words, `await foreach ... in`.


### Back-pressure, and the deliberate unbounded choice

*Back-pressure* is what happens when items arrive faster than they can be consumed — the pressure of a backlog pushing back on the producer. The plumbing analogy is exact: if water pours into a pipe faster than it drains out the far end, pressure builds; if the pipe has a fixed size, eventually it is full and the source must slow down or overflow. In software, a *bounded* Channel (one with a maximum length) creates back-pressure on purpose: when it fills, the writer's `WriteAsync` waits until the reader catches up, which throttles the producer to the consumer's pace.

APRS-Command makes the opposite choice, and it is worth understanding why. Every transport uses `Channel.CreateUnbounded` — a pipe with no size limit and therefore *no* back-pressure. The writer never waits, never throttles, never rejects a packet. This is a considered trade-off, not an oversight:

| Consideration | Why unbounded is the right call here |
| --- | --- |
| The producer must never stall | The receive loop is reading a live radio/network feed. If a full pipe forced it to wait, incoming bytes would pile up in the OS socket buffer and packets could be lost. A radio feed cannot be told 'please resend' — a dropped packet is gone. Keeping the writer non-blocking protects the capture. |
| The volume is genuinely small | APRS is low-bandwidth by design — packets are short text bursts, and even a busy feed is a trickle by modern standards. The belt is drained continuously by a consumer that is far faster than the feed, so in practice the backlog stays near zero and memory growth is a non-issue. |
| The consumer is fast and coalesced | The UI side does not process packets one-at-a-time on screen; it batches and refreshes on a timer (see below). So the pipe empties in quick bursts, and there is nothing to push back against. |

> **The honest downside of unbounded** — An unbounded pipe trades a throttle for a risk: if a consumer ever stopped draining while packets kept arriving, the belt would grow without limit and memory would climb. The design is safe here only because the feed is low-volume and the consumer is always running and always faster than the feed. If APRS-Command ever ingested a genuinely high-rate firehose, this is the first place that would need a bounded channel with an explicit full-pipe policy — that is the standard fix, and the TryWrite/WaitToReadAsync shape is already the right shape to switch to it.


### Crossing to the UI thread — safely and without thrash

Reading packets in the background is only half the story. The map, the packet log, and every visible control live on one special thread — the *UI thread* — and a rule of every desktop UI framework, Avalonia included, is that *only that thread may touch the screen.* A background reader that tried to update the map directly would corrupt it or crash. So there must be a safe border crossing from the background world to the UI world.

The `LiveDataCoordinator` is that crossing. When it wires up a receive-only APRS-IS connection, it subscribes to the client's event and immediately marshals each packet onto the UI thread:

```csharp
aprsIsClient.RawPacketReceived += (_, e) =>
    Dispatcher.UIThread.Post(() =>
        ingestion.IngestReceivedLine(e.RawPacketLine, AprsPacketSource.AprsIs, e.ReceivedAtUtc));
```

`Dispatcher.UIThread.Post(...)` means 'schedule this small piece of work to run on the UI thread as soon as it's free.' It is the official, thread-safe doorway. The packet arrives on a background thread, but the actual work of feeding it into the station database happens on the UI thread — which, as the code's own comment notes, keeps all database and log access single-threaded and therefore free of the subtle bugs that plague shared multi-threaded data.

There is one more piece of responsiveness engineering here, and it is a direct back-pressure-adjacent idea applied to the screen. Even though packets are ingested one at a time, the map is *not* redrawn on every single packet — that would be visual thrash under a busy feed, repainting dozens of times a second for no benefit. Instead the coordinator sets a `dirty` flag on each packet and repaints on a gentle timer:

```csharp
this.ingestion.PacketIngested += (_, _) => dirty = true;

// ...in Start():
refreshTimer = new DispatcherTimer(
    TimeSpan.FromMilliseconds(500),
    DispatcherPriority.Background,
    (_, _) => RefreshIfDirty());
```

Every ingested packet just flips `dirty = true` — cheap, instant. A separate timer fires twice a second and, only if something actually changed, redraws the map and refreshes the log. This *coalescing* (collapsing many small updates into one periodic refresh) is the display-side echo of back-pressure thinking: the input can burst as fast as it likes, but the expensive downstream work is paced to a rate a human eye and a GPU are comfortable with. A hundred packets in half a second cost exactly one repaint, not a hundred.


### The UI command wrapper: awaiting without freezing a click

One last small but telling piece rounds out the async story on the UI side. When a user clicks a button that starts slow async work — connecting, exporting, downloading — that work must not freeze the click. `AsyncDesktopCommand` is the wrapper that makes an async handler behave like a normal button command:

```csharp
public async void Execute(object? parameter)
{
    if (isRunning) return;
    isRunning = true;
    RaiseCanExecuteChanged();
    try
    {
        await executeAsync().ConfigureAwait(true);
    }
    finally
    {
        isRunning = false;
        RaiseCanExecuteChanged();
    }
}
```

Two things are worth naming. First, the `if (isRunning) return` guard plus the `isRunning` flag mean a double-click cannot start the same slow job twice — the button effectively disables itself while its work is in flight, then re-enables in the `finally`. Second, note `ConfigureAwait(true)` — the exact opposite of the transport layer's `ConfigureAwait(false)`. This is deliberate: a button command *wants* to resume on the UI thread when its work finishes, so it can safely re-enable itself and update the screen. The same keyword, tuned in opposite directions in the two layers, is a compact illustration of the whole chapter's discipline: know which thread you are on, and only ever touch the UI from the UI thread.


## Why It Matters / Design Takeaways

Responsiveness in APRS-Command is not luck — it is the payoff of a consistent pattern applied in three transport clients and one coordinator. Grasping this pattern explains why the app can monitor a live feed for hours while remaining perfectly fluid, and it tells a future maintainer exactly where to look when something stalls or a packet goes missing.

- *Async is waiting-without-blocking.* Every slow operation — connect, read, write — is awaited, which releases the thread instead of freezing it. That single discipline is what keeps the window alive while the app waits on the world.
- *The receive loop is isolated on a background task* via Task.Run, because it never ends. Separating the one-time 'connect' from the forever 'receive' is what lets ConnectAsync return while reading continues underneath.
- *The Channel is the safe seam between threads.* It lets a fast background reader hand packets to a possibly-slower consumer with neither one dragging on the other — the conveyor belt that absorbs differences in pace.
- *Unbounded is a deliberate trade-off, not neglect.* The app chooses no back-pressure so the live radio/network reader can never stall and drop an unrecoverable packet — safe precisely because APRS is low-volume and the consumer is always faster than the feed.
- *Coalescing paces the expensive work.* On the display side, a dirty flag plus a 500 ms timer collapse a burst of packets into a single repaint — back-pressure thinking applied to the screen, so a busy feed never thrashes the UI.
- *Thread awareness is enforced by convention.* ConfigureAwait(false) everywhere in the background layer, ConfigureAwait(true) and Dispatcher.UIThread.Post at the UI border, and single-threaded database access — together they make the multi-threaded reality safe and boring, which is exactly what you want.

If you remember one sentence: *packets flow socket → receive loop → Channel → consumer → (UI thread) → coalesced repaint,* and every arrow in that chain is an await or a thread-safe handoff designed so no link can ever freeze the one behind it.


# 10. The Event Bus

*How the parts of APRS-Command tell each other what happened without ever knowing who is listening.*


## What This Is / What It Is For

Inside APRS-Command, dozens of little machines are running at once. One machine reads raw radio text off a serial cable. Another decodes that text into a real station on the map. Another writes a plain-language line into an on-screen event log. Another streams the same news out over a WebSocket to any external tool that's listening. The obvious way to wire these together would be to have each machine hold a direct reference to every other machine it wants to notify — the decoder would call the map, then call the log, then call the WebSocket service, one after another. That works until it doesn't: every time you add a new listener, you have to go back and edit the decoder to tell it about the new listener. The decoder becomes a tangled hub that knows about the entire application.

The *event bus* is the app's answer to that tangle. An event bus is a shared bulletin board: one part of the program pins up a note ("a raw packet just arrived," "a packet was successfully parsed," "a station expired"), and any other part that cares about that kind of note gets handed a copy — without the note-writer knowing or caring who is reading. The writer is called a *publisher* (or producer); the reader is called a *subscriber* (or consumer). The bus sits in the middle so the two never have to know each other exists. In APRS-Command this board lives in `AprsEventBus.cs` in the `Aprs.Services` project.

> **The name to remember** — The interface is IAprsEventBus and the concrete board is AprsEventBus. The kinds of notes are the values of the AprsEventType enum — RawPacketReceived, AprsPacketParsed, StationUpdated, and about forty more.


### Why a bus at all — the problem it prevents

The design goal is *decoupling*: letting two parts of a program cooperate without either one holding a reference to the other. Think of a newspaper. The reporter writes a story and hands it to the paper; subscribers get it on their doorstep. The reporter has never met the subscribers, doesn't know how many there are, and doesn't change a word of the story when someone new subscribes or cancels. That independence is exactly what the event bus buys the codebase.

The concrete payoff shows up every time a feature is added. When APRS-Command grew a live WebSocket feed for external tools, the packet-decoding code did not change at all. The WebSocket service simply subscribed to the bus and started receiving the events that were already being published. The publisher (the decoder) and the consumer (the WebSocket feed) were developed, and can be tested, in complete isolation. That is the whole point: *new consumers are additive, not invasive.*

> **The test that proves it** — A healthy event bus lets you answer "who reacts when a packet is parsed?" by searching for subscribers, and "what does the decoder notify?" by searching for publishes — as two separate lists. You never have to read one to understand the other.


### The vocabulary, defined once

| Term | Plain meaning |
| --- | --- |
| Publish | Pin a note on the board. "Here's something that happened; whoever cares can react." |
| Subscribe | Ask the board to hand you a copy of every note of a certain kind, from now on. |
| Event | The note itself — a small immutable record describing something that happened, plus metadata about when and where. |
| Handler | The function you give the bus that runs each time your kind of note appears. |
| Subscription | A little receipt the bus gives you back. Throw it away (dispose it) and you stop receiving notes. |
| Decoupling | Two parts cooperating without either holding a direct reference to the other. |


### An event is a note with a stamped envelope

Every note on the board implements one tiny contract, `IAprsEvent`, which promises exactly one thing: a `Metadata` property. Everything the bus needs to route, timestamp, and remember a note lives in that metadata; the actual contents ride along separately.

```csharp
// IAprsEvent.cs — the whole contract
public interface IAprsEvent
{
    AprsEventMetadata Metadata { get; }
}

// AprsEventBase.cs — a ready-made base for events
public abstract record AprsEventBase(AprsEventMetadata Metadata) : IAprsEvent;
```

`AprsEventMetadata` is the stamped envelope. It is an immutable *record* (a C# type built for holding data that never changes after it's created — like a printed receipt) carrying a unique `EventId`, the `EventType` (which of the ~forty kinds of note this is), an `EventCategory` (a coarser grouping — Packet, Station, Weather, and so on), a UTC timestamp, where the data came from, a severity, and a set of optional "related" fields — the callsign, object name, message id, or packet id this note is about. Those related fields are what let a consumer filter ("only notes about station W1AW") without cracking open the payload.

```csharp
// AprsEventMetadata.cs (fields, abbreviated)
public sealed record AprsEventMetadata(
    Guid EventId,
    AprsEventType EventType,
    AprsEventCategory EventCategory,
    DateTimeOffset TimestampUtc,
    ExternalSourceMetadata SourceMetadata,
    AprsEventSeverity Severity = AprsEventSeverity.Info,
    string? RelatedCallsign = null,
    string? RelatedObjectName = null,
    string? RelatedMessageId = null,
    string? RelatedPacketId = null,
    string? Summary = null,
    string? Notes = null);
```

The contents of the note — the actual station, log entry, or text — travel in a generic wrapper, `AprsEventEnvelope<TPayload>`. "Generic" here means the box is the same shape no matter what you put in it; the `<TPayload>` is a fill-in-the-blank for the cargo type. One publisher stuffs a `DecodedEventLogEntry` in the box, another stuffs a plain `string`, and the bus treats both identically because both are just an `IAprsEvent` with metadata.

```csharp
// AprsEventEnvelope.cs — one box, any cargo
public sealed record AprsEventEnvelope<TPayload>(
    AprsEventMetadata Metadata,
    TPayload? Payload = default,
    IReadOnlyDictionary<string, string>? Attributes = null) : AprsEventBase(Metadata);
```


### The kinds of note: the AprsEventType menu

`AprsEventType` is the fixed menu of every kind of note the bus understands. It reads like a table of contents for everything interesting that can happen in the app — and the two the outline for this chapter singles out are right at the top.

```csharp
// AprsEventType.cs (first several of ~44 values)
public enum AprsEventType
{
    RawPacketReceived,     // radio text arrived, not yet understood
    RawPacketTransmitted,
    AprsPacketParsed,      // that text was decoded into a real packet
    AprsPacketParseFailed,
    StationCreated,
    StationUpdated,
    StationExpired,
    ObjectCreated,
    // ... WeatherUpdated, MessageReceived, GpsUpdated,
    //     PacketTransmitBlocked, IGatePacketGated, AlertTriggered, ...
}
```

> **Two notes, two moments** — RawPacketReceived and AprsPacketParsed describe the same packet at two different stages of its life. RawPacketReceived means "undigested text just came off a transport." AprsPacketParsed means "we successfully turned that text into a structured packet." A consumer that wants to log everything off the air subscribes to the first; a consumer that only cares about packets it can actually plot subscribes to the second. Because they are separate note types, each consumer asks for exactly the stage it needs.


### Subscribing: asking the board for a kind of note

There are two ways to subscribe. `Subscribe(eventType, handler)` says "hand me only this one kind of note." `SubscribeAll(handler)` says "hand me everything." Both take a *handler* — a function the bus will call for each matching note — and both hand back an `AprsEventSubscription` receipt.

```csharp
// IAprsEventBus.cs
AprsEventSubscription Subscribe(
    AprsEventType eventType,
    Func<IAprsEvent, CancellationToken, ValueTask<AprsEventHandlerResult>> handler);

AprsEventSubscription SubscribeAll(
    Func<IAprsEvent, CancellationToken, ValueTask<AprsEventHandlerResult>> handler);
```

Read that handler signature in plain words: "give me an event and a cancellation token, and I'll go do some work and eventually report back whether I handled it." The `CancellationToken` is a standard .NET stop-signal — a way for the caller to say "never mind, abandon this" — and it's threaded through so a slow handler can bail out cleanly. The return value, an `AprsEventHandlerResult`, is the handler's honest yes/no: did I cope with this note, or did I fail?

```csharp
// AprsEventHandlerResult.cs — a handler's answer
public sealed record AprsEventHandlerResult(
    bool Success, string? ErrorMessage = null, Exception? Exception = null)
{
    public static AprsEventHandlerResult Handled { get; } = new(true);
    public static AprsEventHandlerResult Failed(string errorMessage, Exception? exception = null)
        => new(false, errorMessage, exception);
}
```

Here is a real subscribe, from the live WebSocket feed. When the feed starts, it subscribes to everything and forwards each event to its connected clients, then reports success. Notice what the WebSocket service does not do: it never mentions the decoder, the map, or any publisher. It only mentions the bus.

```csharp
// WebSocketEventStreamService.cs — a real SubscribeAll
subscription ??= eventBus?.SubscribeAll(async (evt, token) =>
{
    await BroadcastAsync(evt, token).ConfigureAwait(false);
    return AprsEventHandlerResult.Handled;
});
```

> **Reading ??= and ?.** — subscription ??= ... means "only subscribe if we haven't already" — it prevents a double-subscription if Start is called twice. eventBus?. means "only if a bus was actually supplied" — the service tolerates running without one. Small guards, but they keep the feed from misbehaving on restart.


### The receipt is the off-switch

The `AprsEventSubscription` the bus hands back is more than a token — it is the only way to stop listening. It implements `IDisposable`, the standard .NET pattern for "something you're expected to clean up when you're done." Disposing it runs the exact un-hook logic the bus prepared at subscribe time, and it's written to be safe to call twice.

```csharp
// AprsEventSubscription.cs
public sealed class AprsEventSubscription : IDisposable
{
    private readonly Action dispose;
    private bool disposed;
    // ...
    public void Dispose()
    {
        if (disposed) return;   // idempotent: calling twice is harmless
        disposed = true;
        dispose();              // runs the bus's Unsubscribe for this exact id
    }
}
```

> **Why this matters: the lapsed-listener leak** — If a subscriber holds a handler on the bus but the object that owns it is otherwise finished, the bus keeps a reference to it forever — the object can never be garbage-collected, and it keeps reacting to events it no longer cares about. This is the classic "lapsed listener" memory leak. The cure is discipline: whoever subscribes must dispose the subscription when they shut down. The WebSocket service does exactly this — subscription?.Dispose() in its StopAsync — which is why starting and stopping the feed repeatedly doesn't slowly leak.


### Publishing: pinning the note

Publishing is the mirror image, and even simpler for the caller: build an event, hand it to `Publish`. Here is a real one from `DecodedEventLogService`, which turns each decoded log entry into a bus event so the rest of the app can react. It assembles the metadata, wraps the entry and some attributes in an envelope, and drops it on the board.

```csharp
// DecodedEventLogService.cs — a real Publish
var metadata = new AprsEventMetadata(
    entry.EventId,
    MapEventType(entry.EventType),
    MapEventCategory(entry.EventCategory),
    entry.EventTimestampUtc,
    CreateSourceMetadata(entry),
    MapSeverity(entry.Severity),
    RelatedCallsign: entry.SourceCallsign,
    // ... related object / message / packet ids, summary, notes
    Summary: entry.Summary,
    Notes: entry.Notes);

eventBus.Publish(new AprsEventEnvelope<DecodedEventLogEntry>(metadata, entry, attributes));
```

The publisher's whole world is those two lines: build the note, call `Publish`. It has no idea whether zero or five consumers will react, and it doesn't wait around to find out in any way it has to reason about. That ignorance is the feature.

> **Guard before you publish** — DecodedEventLogService checks if (eventBus is null) return; before doing any of this. The bus is an optional collaborator — the service is fully functional without one, and only lights up the event stream when a bus is wired in. That keeps the service easy to test in isolation and impossible to crash by leaving the bus out.


### Inside the board: how Publish actually routes a note

`PublishAsync` is the heart of the bus, and it's worth reading line by meaningful line because it encodes three deliberate safety decisions.

```csharp
// AprsEventBus.cs — the routing core (abridged)
public async Task<AprsEventPublishResult> PublishAsync(IAprsEvent aprsEvent, CancellationToken ct = default)
{
    Subscriber[] subscribers;
    lock (syncRoot)                                   // (1) take a consistent snapshot
    {
        AddToHistory(aprsEvent);
        var typed = typedSubscribers.TryGetValue(aprsEvent.Metadata.EventType, out var t) ? t : [];
        subscribers = allSubscribers.Concat(typed).ToArray();
    }

    var results = new List<AprsEventHandlerResult>(subscribers.Length);
    foreach (var subscriber in subscribers)
    {
        if (ct.IsCancellationRequested) { results.Add(AprsEventHandlerResult.Failed("...cancelled.")); break; }
        try
        {
            var result = await subscriber.Handler(aprsEvent, ct).ConfigureAwait(false);
            results.Add(result);
        }
        catch (Exception ex)                          // (2) one bad handler can't sink the rest
        {
            results.Add(AprsEventHandlerResult.Failed(ex.Message, ex));
        }
    }
    return new AprsEventPublishResult(aprsEvent, subscribers.Length, results);
}
```

*Decision 1 — snapshot under a lock.* The `lock (syncRoot)` block copies the current subscriber list into a fresh array, then releases the lock before any handler runs. `syncRoot` is a private object used purely as a "talking stick": only one thread may hold it at a time, so the subscriber lists can't be corrupted by a subscribe happening on another thread mid-publish. Crucially, the handlers themselves run outside the lock — so a slow handler can't freeze the whole bus, and a handler that subscribes or unsubscribes while reacting won't deadlock or throw "collection modified."

*Decision 2 — one bad handler can't sink the rest.* Each handler runs inside its own `try/catch`. If a subscriber throws, the bus catches it, records the failure as a `Failed` result, and keeps going down the list. A crash in the WebSocket feed can't stop the on-screen log from updating. This directly honors the project rule "do not swallow exceptions silently" — the exception isn't discarded, it's captured into the result the publisher can inspect afterward.

> **Sequential, not fan-out** — Handlers run one after another with await, in order: all-subscribers first, then the type-specific ones. This is a deliberate simplicity choice over firing them all in parallel — it makes the order predictable and the failure handling easy to reason about. The trade-off is that one genuinely slow handler delays the ones behind it. For this app's event volumes that's a fair bargain; if it ever isn't, the seam to change is this single loop.

*Decision 3 — a full report card.* `PublishAsync` returns an `AprsEventPublishResult` bundling the original event, how many subscribers it reached, and every handler's result. Its `Success` is true only if all of them succeeded. A publisher that cares can check; one that doesn't can ignore it. There's also a synchronous `Publish` that simply waits on the async version — a convenience for callers (like the log service above) that aren't themselves async.

```csharp
// AprsEventPublishResult.cs
public sealed record AprsEventPublishResult(
    IAprsEvent Event, int SubscriberCount, IReadOnlyList<AprsEventHandlerResult> HandlerResults)
{
    public bool Success => HandlerResults.All(result => result.Success);
}
```


### The board also remembers: recent-event history

The bus isn't only a pass-through; it keeps a short rolling memory. Every published event is added to a `recentEvents` list, capped at 500 by default, oldest dropped first. That memory is what lets a screen that opens late still show what just happened, instead of a blank slate.

```csharp
// AprsEventBus.cs — bounded history
private void AddToHistory(IAprsEvent aprsEvent)
{
    if (configuration.MaximumRecentEvents <= 0) return;
    recentEvents.Add(aprsEvent);
    while (recentEvents.Count > configuration.MaximumRecentEvents)
        recentEvents.RemoveAt(0);          // drop the oldest
}

// AprsEventBusConfiguration.cs
public sealed record AprsEventBusConfiguration(int MaximumRecentEvents)
{
    public static AprsEventBusConfiguration Default { get; } = new(500);
}
```

The on-screen Event Monitor is the natural customer for that memory. `EventMonitorViewModel` doesn't subscribe to a live firehose at all in its main path — it just asks the bus, "what happened recently?" via `GetRecentEvents()`, and can wipe the board with `ClearHistory()`. Because the history is capped, this stays cheap no matter how long the app runs.

```csharp
// EventMonitorViewModel.cs
IEnumerable<IAprsEvent> events = eventBus.GetRecentEvents();   // newest first
// ...
eventBus.ClearHistory();
```

> **Stateless-friendly by design** — The 500-event cap keeps this consistent with APRS-Command's broader habit of session-local, bounded memory rather than unbounded history. The board remembers enough to be useful and then forgets — no file grows without limit behind your back.


### One bus per family: the second, simpler board

There are actually two buses, and the difference is instructive. `AprsEventBus` carries *domain* events — the radio-and-station facts, rich with metadata, async handlers, and history. Alongside it, `ApplicationEventBus` carries lighter *application/UI* events, and it's deliberately plainer: synchronous handlers (`Action<ApplicationEvent>`, no return value, no async), no history, no report card.

```csharp
// ApplicationEventBus.cs — the lighter sibling
public IDisposable Subscribe(ApplicationEventType eventType, Action<ApplicationEvent> handler) { /* ... */ }
public IDisposable SubscribeAll(Action<ApplicationEvent> handler) { /* ... */ }
public void Publish(ApplicationEvent applicationEvent)
{
    Action<ApplicationEvent>[] handlers;
    lock (syncRoot) { /* snapshot, same pattern */ }
    foreach (var handler in handlers) handler(applicationEvent);
}
```

They share the same publish/subscribe DNA — snapshot under a lock, hand back a disposable receipt, un-hook on dispose — but the domain bus pays for extras (async, results, history) that the UI bus doesn't need. Splitting them keeps each fit for purpose: heavy machinery where the physics of radio traffic warrants it, a featherweight version where a menu just needs to tell a window that a setting changed.

> **Which bus to reach for** — If the thing that happened is about packets, stations, weather, messages, or transmit safety — anything in the AprsEventType menu — it belongs on AprsEventBus. If it's an app-level nudge between UI pieces with no domain weight, it belongs on ApplicationEventBus. When in doubt, the presence of an AprsEventType value is the tell.


### Where the bus comes from: one board, shared by all

For the bulletin-board metaphor to work, everyone must be looking at the same board. That's guaranteed at startup in `DesktopRuntime`, where the bus is registered as a *singleton* — one instance for the entire application's lifetime — in the dependency-injection container. Every part of the app that asks for an `IAprsEventBus` gets that same one.

```csharp
// DesktopRuntime.cs — one board for everybody
services.AddSingleton<IAprsEventBus, AprsEventBus>();
// ...later, handed to each collaborator by the container:
//   new LocalRestApiService(..., eventBus: provider.GetRequiredService<IAprsEventBus>());
//   new WebSocketEventStreamService(..., eventBus: provider.GetRequiredService<IAprsEventBus>());
//   new EventMonitorViewModel(provider.GetRequiredService<IAprsEventBus>());
```

This is also why every collaborator depends on the interface `IAprsEventBus`, never the concrete `AprsEventBus`. In a test you can hand a service a fake bus and watch exactly what it publishes, or feed a subscriber events by hand — no radios, no threads, no real board required. The seam that decouples producers from consumers at runtime is the very same seam that makes each of them testable alone.

> **Singleton, not static** — The board is a single shared instance, but it is not a global static variable. It's handed to each part through its constructor by the DI container. That distinction is what keeps the code honest: a class's dependencies are visible in its constructor, and a test can supply a different bus without any global state to reset.


## Why It Matters / Design Takeaways

The event bus is the app's nervous system: it lets the many independent machines inside APRS-Command react to each other's news without any of them being wired directly together. A publisher builds a small immutable note and drops it on a shared board; subscribers that asked for that kind of note get a copy; nobody on either side holds a reference to anybody on the other. New features arrive as new subscribers, not as edits to old publishers — which is exactly how a codebase stays workable long after its author has moved on.

- *Decoupling is the whole point.* Publishers name a note type, not a recipient. The decoder doesn't know the WebSocket feed exists — and that ignorance is what lets the feed be added, tested, and removed without touching the decoder.
- *Events are stamped, immutable notes.* Every event is an IAprsEvent carrying AprsEventMetadata (who, when, what kind, related callsign/object/message) with the real cargo riding in a generic AprsEventEnvelope<T>. Route on the envelope, unwrap the payload only if you care.
- *RawPacketReceived vs. AprsPacketParsed are the same packet at two life stages* — undigested text versus a structured packet — so each consumer subscribes to exactly the moment it needs.
- *The subscription is the off-switch.* Subscribe returns a disposable receipt; disposing it un-hooks you. Skip that and you get the lapsed-listener leak. The WebSocket service disposing on stop is the model to copy.
- *The board is defensive.* Publish snapshots subscribers under a lock, runs each handler outside the lock in its own try/catch, and returns a per-handler report — so one slow or throwing consumer can't freeze or crash the others, and nothing is swallowed silently.
- *The board remembers, but only a little.* A 500-event rolling history lets late-opening screens catch up, capped so it never grows without bound — consistent with the app's session-local memory habit.
- *Two buses, fit for purpose.* Heavyweight AprsEventBus (async, results, history) for domain facts; featherweight ApplicationEventBus (synchronous, no history) for UI nudges. Same DNA, different weight.
- *One shared board, injected as an interface.* Registered as a singleton in DesktopRuntime and handed around as IAprsEventBus — which is simultaneously what makes it shared at runtime and trivially fakeable in tests.


# 11. The Transmit-Safety Authority

*The single locked door every outgoing transmission must pass through, closed by default so nothing keys the radio unless every safety condition is explicitly satisfied.*


## What This Is / What It Is For

*APRS-Command* talks to the world over ham radio. It can push small digital packets out over the air through an RF (radio-frequency) port, and it can push them across the internet through a network called *APRS-IS*. In ham radio, sending anything is a serious act: you must identify yourself with a real callsign, you must not jam a shared frequency, and during a training drill you must be able to guarantee that nothing you do leaks onto the real air. `TransmitSafetyAuthority.cs` is the one place in the entire program that decides whether a transmission is allowed to happen.

Picture it as a single locked door between the whole application and the antenna. Every outgoing packet — a position *beacon*, a text message, an APRS *object*, a relayed *digipeat*, an *iGate* gate-in — has to walk through this one door and get a yes. There is no side door. That is the whole point of the design. This chapter explains what the door checks, why it was built as one central door instead of a dozen scattered checks, and how the code physically forces every path through it.

> **The one-sentence version** — TransmitSafetyAuthority is a closed-by-default gate: a transmission is denied unless every safety condition is explicitly satisfied, and the code is arranged so no transmit path — not even a future one someone forgets to wire up — can key the radio while a global inhibit is on.


### Why receive-first, and why a central authority

The design starts from a philosophy: *receive-first*. Listening to the air costs nothing and breaks no rules, so the app receives freely by default. Transmitting is the opposite — it has to be earned. Every condition that would make a transmission legal or safe defaults to "no," and only an explicit, correct configuration turns it to "yes." A blank callsign is no. The placeholder callsign *N0CALL* is no. An APRS-IS *passcode* of `-1` (the receive-only sentinel) is no. A port that has not been explicitly transmit-enabled is no. You cannot accidentally transmit; you can only deliberately transmit.

The tempting way to enforce that is to sprinkle `if` statements everywhere: the beacon code checks for a callsign, the message code checks for a passcode, the digipeater checks the port. That approach breaks the instant someone adds a seventh transmit path and forgets one check — and the failure stays invisible until a drill accidentally beacons on a live frequency. Scattered checks mean the safety rules live in seven heads and seven files; a *central authority* means they live in exactly one, are tested in exactly one place, and every path is physically routed through them.

> **Jargon, in plain words** — An interface is a contract — a wall socket. Any class that implements it fits, and callers use the socket without caring what is plugged in. A record is an immutable little data holder: once built, its fields never change. An enum is a fixed menu of named choices. Dependency injection just means a class is handed the collaborators it needs from outside, rather than building them itself — so tests can hand it fakes.


### What the door actually checks

The public face is one interface, `ITransmitSafetyAuthority`. It exposes the global inhibit state, lets code turn the inhibit on and off, and — the heart of it — offers one method, `Evaluate`, that answers a single transmit request with a precise yes or no. The comment on the interface states the mission directly: it is the authority "every transmit path consults before keying up... so no caller can transmit by a side path that forgot a check."

```csharp
public interface ITransmitSafetyAuthority
{
    bool IsInhibited { get; }
    string? InhibitReason { get; }
    void Inhibit(string reason);   // globally block all transmit (e.g. exercise mode)
    void Release();                // lift the global inhibit
    TransmitDecision Evaluate(TransmitRequest request);
}
```

A `TransmitRequest` is a tiny immutable record — just the port to transmit on and where the packet is bound (`Rf` over the air, or `AprsIs` to the internet). The answer is a `TransmitDecision` record carrying three things: whether it is allowed, a machine-readable `TransmitDenyReason` enum (so callers can react to the *category* of refusal), and a human-readable explanation string (so the operator sees exactly why). The record even hands you two named constructors so the intent reads cleanly at the call site:

```csharp
public sealed record TransmitDecision(bool IsAllowed, TransmitDenyReason Reason, string Explanation)
{
    public static TransmitDecision Allow() => new(true, TransmitDenyReason.None, "Transmit allowed.");
    public static TransmitDecision Deny(TransmitDenyReason reason, string explanation) =>
        new(false, reason, explanation);
}
```

Splitting the reason into a machine enum *and* a human string is a deliberate two-audience move. Code branches on the enum ("was this a passcode problem?") without brittly parsing English, while the operator reads a sentence that tells them what to fix. Neither audience is served by the other's format, so the design serves both.


### The evaluation order — highest stakes first

`Evaluate` walks four gates in a fixed priority order, and returns a denial the moment any one fails. The order is not cosmetic: the most sweeping, safety-critical condition is checked first, so a drill can never be overridden by a lower-level detail.

| Order | Gate | Denies when | Why it ranks here |
| --- | --- | --- | --- |
| 1 | Global inhibit | Exercise / training mode is on | A drill must hard-block everything, unconditionally, before any other logic runs |
| 2 | Identity | No real callsign (blank or N0CALL) | Transmitting without your callsign is illegal; nothing else matters if you have no ID |
| 3 | Destination | APRS-IS bound but no valid passcode | The internet uplink is receive-only until a real numeric passcode proves you may post |
| 4 | Per-port | Port disabled, not transmit-enabled, disconnected, or receive-only | The concrete port opt-in — the last, most specific check |

```csharp
public TransmitDecision Evaluate(TransmitRequest request)
{
    ArgumentNullException.ThrowIfNull(request);

    // 1) Master inhibit wins over everything.
    bool isInhibited; string? reason;
    lock (gate) { isInhibited = inhibited; reason = inhibitReason; }
    if (isInhibited)
        return TransmitDecision.Deny(TransmitDenyReason.GlobalInhibit, reason ?? "Transmit is inhibited.");

    // 2) Identity: never transmit without a real callsign.
    if (!policy.HasValidStationCallsign)
        return TransmitDecision.Deny(TransmitDenyReason.NoValidCallsign,
            "No valid station callsign is set. Transmit is blocked until a real callsign replaces the placeholder.");

    // 3) Destination policy: APRS-IS transmit needs a real passcode.
    if (request.Destination == TransmitDestination.AprsIs && !policy.HasValidAprsIsPasscode)
        return TransmitDecision.Deny(TransmitDenyReason.AprsIsPasscodeRequired,
            "A valid APRS-IS passcode is required to transmit to the internet (the connection is receive-only).");

    // 4) Per-port checks.
    var portResult = portManager.CheckTransmitSafety(request.PortId, globalTransmitSafetyEnabled: true);
    if (!portResult.IsSafe)
        return TransmitDecision.Deny(TransmitDenyReason.Port,
            portResult.FailureReason ?? "The port is not ready to transmit.");

    return TransmitDecision.Allow();
}
```

Read it top to bottom and the closed-by-default nature is literally visible: there is exactly one `return TransmitDecision.Allow()`, and it sits at the very bottom, reachable only after all four gates have been passed. Every other exit is a denial. The default answer of this method — the answer you get unless you clear every hurdle — is no.

Notice too where the identity and passcode facts come from. The authority does not read settings files or know where a callsign is stored. It asks a second small interface, `ITransmitPolicyContext`, for two boolean facts: `HasValidStationCallsign` and `HasValidAprsIsPasscode`. The real implementation, `SettingsTransmitPolicyContext`, reads the live persisted settings on each query (so a callsign typed into the UI takes effect immediately) and lives up in the composition layer. This keeps the engine-side authority ignorant of *where* settings live — it just consumes facts. That is a clean layering boundary: the rule lives in the authority, the data source is pluggable.


### Two layers of defense: Evaluate() and the inhibit gate

Here is the subtle, crucial part. Calling `Evaluate` is voluntary — a transmit path has to remember to call it. Voluntary safety is exactly the weakness the whole design set out to kill. So the authority carries a second, non-negotiable layer. It implements a second interface as well, `ITransmitInhibitGate`, which lives down in the low-level transport layer and exposes just one fact: `IsTransmitInhibited`.

```csharp
public sealed class TransmitSafetyAuthority
    : ITransmitSafetyAuthority, Aprs.Transport.ITransmitInhibitGate
{
    // The minimal contract the transport transmit chokepoints consult so the global inhibit
    // hard-blocks every path, not just the ones that remember to call Evaluate().
    bool Aprs.Transport.ITransmitInhibitGate.IsTransmitInhibited => IsInhibited;
```

The two interfaces are two views of the same object. `Evaluate` is the rich, per-request checkpoint the higher-level services use. `IsTransmitInhibited` is the dumb, always-on tripwire that the lowest-level code — the actual byte-writing transmit methods — consults right before touching the wire. The tripwire exists specifically because the rich checkpoint can be skipped, and the tripwire cannot. It is the backstop for human forgetfulness. Keeping it in the transport layer is what lets the lowest-level code check it without an upward dependency on the Services layer — the dependency arrow only ever points down.

You can see the tripwire fire at the real chokepoints. Inside the APRS-IS client's `SendRawPacketAsync`, before any validation or any network I/O, the gate is checked first:

```csharp
// AprsIsClient.SendRawPacketAsync
// Global inhibit (exercise/training mode) wins over everything and is checked before any
// other validation so a drill can never key up APRS-IS by any path.
var gate = InhibitGate;
if (gate is not null && gate.IsTransmitInhibited)
    return AprsIsTransmitResult.Failed(timestamp, normalizedPacket, stateAtRequest,
        gate.InhibitReason ?? "Transmit is globally inhibited (exercise mode).");
```

The RF side does exactly the same in `KissRfBeaconTransmitClient.SendBeaconAsync`, and a test proves it blocks before it ever queries an RF port at all (`Assert.False(anyClientQueried)`). Those two methods are the only two places in the program where bytes actually leave for the internet or the radio. Guarding both means the inhibit is enforced at the true perimeter, not merely on the polite paths.

> **Why the double check is not redundant** — Higher-level services also call Evaluate (which checks inhibit as gate #1), so during exercise mode an inhibited transmit is often caught early with a friendly message — ObjectTransmitService, for instance, checks IsInhibited up front to avoid a misleading 'sent' attempt. But 'often' is not 'always.' The transport-layer tripwire is what turns 'often' into 'always': even a path that skipped Evaluate entirely still dies at the wire. Belt and suspenders, on purpose.


### How every path is forced through the one door

A central authority only works if every path is genuinely wired to it, and that wiring happens in the composition root, `DesktopRuntime`. First the authority and its policy context are registered as *singletons* — meaning the whole app shares exactly one instance, so an inhibit toggled anywhere is seen everywhere:

```csharp
services.AddSingleton<IAprsPortManager, AprsPortManager>();
services.AddSingleton<ITransmitPolicyContext, SettingsTransmitPolicyContext>();
services.AddSingleton<ITransmitSafetyAuthority, TransmitSafetyAuthority>();
```

Then that single authority is cast to its inhibit-gate face and physically handed to each transmit client, so the low-level tripwire and the high-level checkpoint are the same object:

```csharp
// The transmit-safety authority doubles as the global inhibit gate (exercise/training mode).
// Hand it to every transmit chokepoint so no path can key up while inhibited.
var transmitAuthority = provider.GetRequiredService<ITransmitSafetyAuthority>();
var inhibitGate = (ITransmitInhibitGate)transmitAuthority;
rfTransmitClient.InhibitGate = inhibitGate;
```

The digipeater is wired to the full authority and calls `Evaluate` before repeating anyone's packet — layering the global inhibit and the identity gate on top of its own per-port check ("never digipeat as a placeholder callsign"). The beacon pipeline threads the same inhibit gate into every APRS-IS client it builds, even after a settings change rebuilds the client, so "exercise mode blocks it even after a settings-triggered rebuild." The object transmitter checks `IsInhibited` up front. Different paths, one shared brain.

| Transmit path | How it reaches the authority | Layer |
| --- | --- | --- |
| Position beacons (scheduled & Beacon Now) | APRS-IS / RF client carries the shared InhibitGate | Transport tripwire |
| APRS objects | ObjectTransmitService checks IsInhibited, then client tripwire | Both |
| Digipeat (relaying others) | DigipeaterService calls Evaluate() before transmit | Rich checkpoint |
| iGate gate-in & messages | Go out via the same APRS-IS client chokepoint | Transport tripwire |
| Any future RF transmit | Same KissRfBeaconTransmitClient chokepoint | Transport tripwire |


### Exercise mode: one switch, total silence

All of this pays off in one operator gesture. When a ham runs a training *exercise*, they must be certain the software cannot leak real traffic onto the air. The main window's TX badge toggles exercise mode, and it does so by flipping the single shared authority — nothing else:

```csharp
if (authority.IsInhibited)
{
    authority.Release();                 // back to normal (identity/port checks still apply)
    TxBadgeText.Text = "TX Enabled";
}
else
{
    authority.Inhibit("Exercise mode — all transmit inhibited");
    TxBadgeText.Text = "EXERCISE — TX INHIBITED";
}
```

Because that one object is the gate #1 of every `Evaluate` call *and* the tripwire in both wire-level chokepoints, a single `Inhibit(...)` silences beacons, objects, messages, digipeat, and iGate at once — and `Release()` restores normal operation while leaving the identity, destination, and per-port checks fully in force. One switch, provably total, precisely because the safety logic was centralized rather than scattered.

> **Thread safety, quietly handled** — The inhibit flag is guarded by a lock (the private 'gate' object). A background beacon timer on one thread and an operator toggling exercise mode on the UI thread are different threads; the lock guarantees the toggle is seen consistently, with no half-updated state. Evaluate snapshots the flag and reason together inside the lock, so it always acts on a coherent view.


## Practice vs. a Real On-Air Exercise

Because the global inhibit is labeled **Exercise Mode** in the interface, it is easy to assume it is the mode you would run a real drill in. It is the opposite. Exercise Mode is a guarantee that *nothing* transmits — it exists so an operator can rehearse the whole application safely. While it is on, a station communicates with no one: every attempted send is recorded in the log as `TransmitBlocked` and dies at the gate. The lifelike "other stations" seen during practice are fabricated locally by *Simulation*, *Replay*, or *Training*, not received from anyone.

A real multi-operator drill — where stations genuinely exchange traffic — is the other case, and it runs with transmit **enabled** (the deliberate step this authority forces). To keep drill traffic off the live nationwide network, operators isolate it onto a dedicated exercise or simplex RF frequency, and/or a private APRS-IS server on their own network. In the terms of this chapter: Exercise Mode means the global inhibit is **on**; a communicating drill means the inhibit is **off** and the normal per-request checks (identity, destination, per-port) do their job.

> **Exercise Mode is not "the mode you run an exercise in"** — Its name describes what it protects, not what it enables. Exercise Mode = a hard guarantee of no transmission, for safe solo rehearsal. A drill where operators actually talk to each other runs with Exercise Mode OFF and transmit turned on, ideally on an isolated frequency or private server.


## The Sibling: Exercise Traffic Marking

The global inhibit answers "may I transmit at all?" — a single yes/no on an opaque string, which is why it lives at the transport gate. A real EmComm drill needs the opposite: you DO transmit, and every packet must be stamped *EXERCISE* so it is never mistaken for real traffic. That is a different job, and it lives in a small sibling class, `src/Aprs.Services/ExerciseMarking.cs`.

Why it cannot share the inhibit gate's choke point: by the time a packet reaches the transport it is already a finished raw string — the transport has no idea which part is a message body, an object's 9-character name, or a comment. Marking has to change specific semantic fields in different ways (prefix a message, append to a comment, leave a name alone), so it is applied at each *per-type formatter* where those fields are still separate, not at the transport.

The service is deliberately tiny and pure: a session-only mutable state plus a few string helpers. It is not persisted — it defaults OFF at every launch, the same receive-first instinct as the transmit switches, so marking can never silently bleed into real operations a week later.

```csharp
public sealed class ExerciseMarking
{
    public const string Tag = "EXERCISE";
    public bool Active { get; private set; }
    public int  Repeat { get; private set; } = 2;   // message/status prefix count, clamped 1..3

    public string MessagePrefix       => Active ? repeat-many "EXERCISE " : "";
    public int    ReservedMessageLength => MessagePrefix.Length;

    public string MarkBody(string? body);      // prefix a message/status; no-op or de-dup if already tagged
    public string MarkComment(string? comment); // append the tag to a comment; de-dup if present
}
```

[['MarkBody', 'c'], ' prefixes a message or status body and refuses to double-tag (so the built-in EXERCISE template never yields four copies); ', ['MarkComment', 'c'], ' appends the tag to a comment. Both are no-ops when marking is off, so every call site uses the same one-liner with no if-statement.']


### How it reaches every transmit path

Each outbound formatter takes the marking service as an OPTIONAL constructor parameter (defaulting to null), so existing code and tests that build a formatter without one keep compiling unchanged. The composition root registers one shared instance right next to the transmit-safety authority and hands it to the five formatters that assemble outgoing fields:

- [['AprsMessageRetryEngine.FormatMessagePacket', 'c'], ' prefixes the message body; ', ['AprsMessageStoreService', 'c'], ' validates the MARKED length against the 67-character limit so a labeled message can never overflow on the air.']
- [['AprsObjectEditorService.BuildObjectPacket', 'c'], ' appends the tag to the object comment, and pointedly leaves the 9-character name alone.']
- [['AprsBeaconFormatter.CreateInputFromProfile', 'c'], ' marks the position-beacon comment.']
- [['WeatherBeaconScheduler.CreateFormatterOptions', 'c'], ' marks the weather comment.']

> **Why the object NAME is left untouched** — An object's name is its identity: to move, update, or kill it later you must re-send the exact same name. If marking silently prefixed the name, you could not retire the object unless marking were still on — a foot-gun. So the code marks the comment (safe) and the manual documents an operator-applied "X-" name convention (e.g. X-EOC) instead. Correctness beats a slightly louder label.

> **The operator's side** — The user-facing half — the Tools toggle, the amber EXERCISE MARKING badge, the message template, and the channel-isolation guidance — is documented in the User Manual chapter "Operating in a Live Exercise."


## Why It Matters / Design Takeaways

If a future maintainer preserves only one thing about this file, preserve the shape: one object, two interfaces, four ordered gates, closed by default. The rich `Evaluate` checkpoint and the minimal `IsTransmitInhibited` tripwire are two faces of the same shared singleton precisely so that convenience and guarantee do not have to compete — services get friendly, specific denials, while the wire-level chokepoints get an unskippable hard stop.

The rules that must never erode: transmitting is earned, not assumed (receive-first); the single `Allow()` stays at the bottom, reachable only after every gate passes (closed by default); new transmit paths route through the shared client chokepoints so the inhibit reaches them for free; and the authority stays ignorant of where settings live, consuming facts through `ITransmitPolicyContext` rather than reading files itself. Add a transmit feature someday and the safe move is not to write new checks — it is to send it through the door that already exists.

> **The maintainer's rule** — If you ever find yourself writing a fresh 'can I transmit?' check outside TransmitSafetyAuthority, stop. That is the exact scattering this design was built to prevent. Route the new path through the existing authority and the existing transport chokepoints instead — one door, always, closed until proven open.


# 12. Ingestion & Station State

*How a stream of raw radio text becomes the living, aging list of stations you see on the map.*


## What This Is / What It Is For

APRS-Command listens to radios and internet feeds that spit out a constant stream of short text lines. Each line is one *packet* — a burst of data a ham radio station transmitted, saying something like "here I am, this is my position, here's my weather." Raw, those lines are gibberish. This chapter is about the machinery that turns that firehose of text into the thing you actually care about: a clean, always-current list of who is out there, where they are, and how recently you heard from them.

Two pieces do this work. The first is the *ingestion service* — think of it as the building's single front door and mail room. Every packet, no matter which radio or internet source it came from, walks in through this one door, gets logged, gets read, and gets filed. The second is the *station store* (also called the station database) — the filing cabinet that keeps exactly one folder per station, always holding the newest information, and quietly marks folders as going stale when a station stops being heard.

> **Why one front door matters** — APRS-Command can receive from many sources at once — a radio over serial, a TCP connection, the APRS-IS internet backbone, a replayed recording, a simulator. If each source filed its own packets its own way, the code would drift into five slightly-different versions of the same logic. Funneling everything through one AprsIngestionService means parsing, logging, and de-duplication happen exactly once, in exactly one place.


### The front door: AprsIngestionService

The whole service is small on purpose. Its one real job, in plain terms: take a line of text, write it to the raw log, try to understand it, and if that succeeds, update the right filing cabinet. It lives in `Aprs.Services/Runtime/AprsIngestionService.cs` and deliberately knows nothing about screens or radios — transports push lines in, and the desktop layer listens for the "done" signal.

```csharp
public void IngestReceivedLine(string rawLine, AprsPacketSource source, DateTimeOffset receivedAtUtc)
{
    if (string.IsNullOrWhiteSpace(rawLine))
        return;

    rawPacketLog.AddReceivedRawPacket(rawLine, source, timestampUtc: receivedAtUtc);

    AprsPacket? packet = null;
    if (parser.TryParse(rawLine, receivedAtUtc, out packet, out _) && packet is not null)
    {
        var target = source == AprsPacketSource.Replay && replayStationDatabase is not null
            ? replayStationDatabase
            : stationDatabase;
        target.ProcessPacket(packet, source);
    }

    PacketParsed?.Invoke(this, new ParsedPacketEventArgs(packet, source));
    PacketIngested?.Invoke(this, EventArgs.Empty);
}
```

Reading it line by meaningful line: an empty or whitespace line is dropped immediately — no point logging nothing. The raw line is then recorded to the *raw packet log* (a verbatim, unparsed history — the "security camera footage" of everything received) *before* any parsing, so even a line the parser chokes on is still on record. `parser.TryParse` attempts to decode the text; the *Try* naming convention means it returns true/false instead of throwing an error, so a malformed packet is a normal, expected outcome, not a crash. Only if a real packet came back does it get filed via `ProcessPacket`. Finally two events fire — `PacketParsed` (carries the decoded packet, or null if it couldn't be read) and `PacketIngested` (a bare "something happened, you may want to refresh" nudge).

> **It never throws on bad input** — The XML doc on IngestReceivedLine promises it is "safe to call repeatedly; never throws on malformed input." That is a load-bearing guarantee. Packets arrive from the open airwaves and the public internet — a meaningful fraction are corrupt, truncated, or simply weird. The receive pipeline treats a bad packet as data to be flagged, never as an exception to be handled. One garbled line can never take down the feed.


### Live vs. replay: the two-database split

One line above quietly does something important: it chooses *which* filing cabinet to write to based on where the packet came from. APRS-Command keeps two separate station stores. The *live database* holds the real world — everything actually being heard right now. The *replay database* is a scratch space used only when you play back a recorded session. When a packet's source is `Replay`, it is filed in the replay store; everything else goes to the live store.

The reason is stated right in the constructor comment: replayed packets go to "a dedicated station database so a replay session can be shown on the map in isolation without polluting — or clearing — the live station state." If replay and live shared one cabinet, watching a recording of last weekend's event would overwrite the position of a station you are tracking *right now*. The split guarantees that never happens. Notably, everything *else* still runs for replay packets — they are logged, parsed, and can trigger alerts — only the station-state filing is diverted.

```csharp
// DesktopRuntime.cs — the two stores are registered as different types
services.AddSingleton<IStationDatabase>(_ => new Persistence.SqliteStationDatabase());
// Ephemeral in-memory station database that holds ONLY replayed packets
services.AddSingleton<StationDatabase>(_ => new StationDatabase());
```

The live store is a `SqliteStationDatabase` (it survives restarts — more on that below). The replay store is a plain in-memory `StationDatabase` that is thrown away when you exit replay. Registering them under two different types lets the dependency-injection container hand each one to whoever needs it without confusion.

The `LiveDataCoordinator` decides which cabinet the map reads from. A one-line property, `ActiveStationDatabase => replayMode ? replayStationDatabase : stationDatabase`, is the switch. Entering replay mode calls `Clear()` on the replay store so playback always starts from a blank map, while the live store keeps ingesting underneath. Exiting replay clears the replay store again and flips the view back — the live cabinet already holds everything that arrived during playback.


### The station store and the StationSnapshot

The store, `StationDatabase`, is at heart three dictionaries — fast lookup tables keyed by callsign. One holds the current record for each station, one holds each station's position trail, one holds tactical labels. All three use a case-insensitive key so `w4abc` and `W4ABC` are the same station.

```csharp
private readonly Dictionary<string, StationSnapshot> stations = new(StringComparer.OrdinalIgnoreCase);
private readonly Dictionary<string, List<StationTrailPoint>> trails = new(StringComparer.OrdinalIgnoreCase);
private readonly Dictionary<string, TacticalLabel> tacticalLabels = new(StringComparer.OrdinalIgnoreCase);
```

The *key* each station is filed under is not always the callsign. For a normal position or status packet it is the sender's callsign plus its *SSID* (the `-9` in `W4ABC-9`, a number 0–15 that lets one operator run several stations — a truck, a handheld, a weather box). But for an *Object* or *Item* packet — where a station is reporting *something else*, like a shelter or an incident marker — the key is that object's name instead. `GetStationKey` handles all three cases, and `NormalizeStationKey` trims whitespace and upper-cases so the key is always consistent.

Each folder in the cabinet is a `StationSnapshot` — an *immutable record*, meaning once created it is never edited in place. This is a deliberate design choice. Instead of reaching into a station's record and changing a field (which is easy to get wrong when several things touch it), the code builds a brand-new snapshot every time a packet arrives, copying forward whatever the new packet didn't mention. That is what the `with` keyword does throughout: "give me a copy of this record, but with these fields changed."

```csharp
public void ProcessPacket(AprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown)
{
    if (!packet.IsValid)
        return;

    var stationKey = GetStationKey(packet);
    if (string.IsNullOrWhiteSpace(stationKey))
        return;

    var existing = stations.GetValueOrDefault(stationKey);
    var updated = CreateBaseUpdate(stationKey, packet, packetSource, existing);
    updated = ApplyPacketSpecificFields(updated, packet);

    stations[stationKey] = updated;
    AddTrailPointIfNeeded(stationKey, packet, packetSource);
}
```

The flow: reject an invalid packet outright; find the station's key; pull the existing folder if there is one; build a fresh *base* snapshot (fields common to every packet — last-heard time, packet count, path); then layer on the *packet-specific* fields; store the result back; and finally record a trail point if the packet carried a position.

Why the two-step "base then specific" build? Different packet types carry different information. A position packet has latitude and longitude; a status packet carries a text message; a weather packet carries temperature and wind; a message packet just proves the station can exchange messages. `ApplyPacketSpecificFields` is a `switch` over the packet type that merges in only the fields that type actually provides — and critically, it uses the pattern `position.Latitude ?? station.Latitude`, meaning "use the new value if present, otherwise keep what we already knew." So a station that sends a status update doesn't lose the position it reported five minutes ago.

> **Copy-forward is the quiet hero** — Because every field falls back to the existing value, a station's folder accumulates a complete picture over many packets of different kinds — position from one, weather from another, messaging-capability from a third — without any one packet erasing what the others contributed. The immutable-record design makes this composition safe and obvious to read.


### Not hearing double: duplicate detection

APRS packets are often repeated — a *digipeater* (a relay station that rebroadcasts what it hears to extend range) can bounce the same transmission back to you within seconds. Counting those as separate reports would inflate a station's packet count and muddy the picture. The store defends against this with a lightweight fingerprinting scheme.

```csharp
var contentHash = ComputePacketHash(packet.RawLine);
var isDuplicate = recentPacketHashes.TryGetValue(contentHash, out var lastSeen)
    && (packet.ReceivedAtUtc - lastSeen) < DupeWindow;   // DupeWindow = 30 seconds
recentPacketHashes[contentHash] = packet.ReceivedAtUtc;
```

`ComputePacketHash` deliberately hashes only the *information element* — the meaningful payload after the routing header — so the same content relayed through a different path still fingerprints identically. If a matching fingerprint was seen within the last 30 seconds (`DupeWindow`), the new packet is flagged a duplicate: it still bumps the station's `DuplicatePacketCount` but is understood as an echo, not a fresh report. The fingerprint table self-prunes once it grows past 2,000 entries so it can't leak memory during a long, busy session.


### The lifecycle: Active, Stale, Expired, Hidden

A station you heard three hours ago is not the same as one you heard three seconds ago, and the map needs to show that difference. Every station carries a *lifecycle state* — one of four values — that answers "how fresh is this?" These are defined in a simple enum: `Active`, `Stale`, `Expired`, `Hidden`.

| State | Plain meaning | Default trigger (time since last heard) |
| --- | --- | --- |
| Active | Heard recently; fully trustworthy | 0 to 30 minutes |
| Stale | Getting old; probably still there | 30 minutes to 2 hours |
| Expired | Old enough to doubt; kept but dimmed | 2 hours to 24 hours |
| Hidden | Off the normal lists (aged out or hidden by hand) | 24 hours or more, or manually hidden |

The thresholds live in a `StationAgingConfiguration` record, so they are tunable rather than hard-coded magic numbers. The defaults: Active up to 30 minutes, Stale threshold at 2 hours, Expired threshold at 2 hours, Hidden threshold at 24 hours. The classification itself is done by `CalculateLifecycleState`:

```csharp
private StationLifecycleState CalculateLifecycleState(StationSnapshot station, DateTimeOffset now)
{
    if (station.IsManuallyHidden)
        return StationLifecycleState.Hidden;

    var age = now - station.LastHeardUtc;
    if (age < TimeSpan.Zero) age = TimeSpan.Zero;   // guard against clock skew

    if (age >= agingConfiguration.HiddenThreshold)   return StationLifecycleState.Hidden;
    if (age >= agingConfiguration.ExpiredThreshold)  return StationLifecycleState.Expired;
    if (age > agingConfiguration.ActiveThreshold && age < agingConfiguration.StaleThreshold)
        return StationLifecycleState.Stale;
    if (age >= agingConfiguration.StaleThreshold)    return StationLifecycleState.Expired;
    return StationLifecycleState.Active;
}
```

Line by line: a station hidden by hand short-circuits to Hidden regardless of age. The *age* is now minus when we last heard it, clamped to zero so a slightly-off clock can't produce a negative age. Then a cascade of thresholds, oldest first: past 24 hours is Hidden, past the Expired threshold is Expired, in the 30-minutes-to-2-hours band is Stale, and anything that falls through is Active.

> **A subtlety worth knowing** — In the default configuration the Expired and Stale thresholds are the same value (2 hours). Because the Expired check runs first, the later `age >= StaleThreshold -> Expired` line can never be reached with the defaults — it exists to stay correct if someone tunes the two thresholds apart. If you ever change these numbers, re-read this method carefully; the branch order, not just the values, determines the outcome.


### The clock that drives aging

A station doesn't age itself — nothing changes in its folder just because time passed. Something has to periodically walk the cabinet and recompute states. That is `UpdateAgeStates(now)`, which rebuilds each snapshot with a freshly-calculated lifecycle state for the supplied moment. It is called by the `LiveDataCoordinator`'s refresh loop.

```csharp
// LiveDataCoordinator — a coalesced refresh runs at most a couple times a second
private void RefreshIfDirty()
{
    if (!dirty) return;
    dirty = false;
    var source = ActiveStationDatabase;
    source.UpdateAgeStates(DateTimeOffset.UtcNow);
    map.UpdateStations(source.GetVisibleStations());
    rawPacketLog.Refresh();
}
```

The coordinator sets a `dirty` flag whenever a packet is ingested, and a timer firing every 500 milliseconds does the actual work only if something changed. This is *coalescing* — under a heavy feed, dozens of packets might arrive between ticks, but the map is recomputed and redrawn at most twice a second instead of dozens of times. Each refresh re-ages every station against the current UTC time, then hands the map only the *visible* stations.

"Visible" is its own filter, `IsVisible`, honoring two configuration switches: whether Expired stations should still show, and whether Hidden stations belong in normal lists (by default: show Expired, hide Hidden). This is how an old-but-not-ancient station stays dimly on the map while a station last heard yesterday drops off it — without either being deleted.


### Trails and tactical labels

Alongside the current snapshot, the store keeps two other kinds of state per station. A *trail* is the breadcrumb history of where a moving station has been — a list of `StationTrailPoint`s, added by `AddTrailPointIfNeeded` whenever a positioned packet arrives. Trails are bounded on three axes so they can't grow without limit: a maximum number of points per station (default 100), an optional minimum-distance filter (don't record a point unless the station actually moved), and an optional maximum age. Exact-duplicate points (same time and place) are rejected outright.

A *tactical label* is a human-friendly name an operator pins to a callsign — "NET CONTROL" onto `W4ABC-9`, say — stored as a `TacticalLabel` record with the real callsign, the label, optional notes, and created/updated timestamps. When a label is set or removed, `RefreshStationDisplayName` rebuilds that station's snapshot so its `DisplayName` reflects the label (falling back to the real callsign when there's no label). This keeps the display name a computed consequence of the label, never a separately-maintained copy that could drift out of sync.


### Surviving a restart: the SQLite layer

The live store needs memory that outlives the app — close APRS-Command and reopen it, and the stations you knew should still be there. That is `SqliteStationDatabase`, a wrapper that holds a plain `StationDatabase` inside it and mirrors changes to an on-disk SQLite file (a lightweight single-file database) at `%AppData%/APRSCommand/stations.db`.

It implements the same `IStationDatabase` interface, so the rest of the app can't tell it apart from the in-memory store — it just does more. Every method is a pass-through to the inner store, and the write methods additionally persist. `ProcessPacket`, for example, updates the in-memory store synchronously (so the UI sees the change instantly) and then serializes the fresh snapshot to disk on a background thread so the UI is never blocked waiting on a disk write.

```csharp
public void ProcessPacket(AprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown)
{
    inner.ProcessPacket(packet, packetSource);
    var callsign = packet.SourceCallsign;
    if (!string.IsNullOrWhiteSpace(callsign))
    {
        var snapshot = inner.GetStation(callsign);
        if (snapshot is not null)
            Task.Run(() => PersistSnapshot(snapshot));   // off the UI thread
    }
}
```

Snapshots are stored as JSON text keyed by callsign, and a database *trigger* (a rule the database runs automatically) prunes the table to the 2,000 most-recently-heard stations after every insert, capping the file's size. On startup, `LoadSnapshots` reads those rows back and feeds each through `RestoreSnapshot`, which drops it straight into the in-memory dictionary *without* re-running it through the parser — and skips anything the current session already heard more recently. Every persistence operation is wrapped in a swallow-and-continue `try/catch`: a corrupt row or a disk hiccup is skipped, never fatal.

> **What is and isn't remembered** — Station snapshots and tactical labels persist across restarts. Position trails deliberately do not — the comment in the file notes they "rebuild from new packets." Trail history is cheap to regenerate and would otherwise bloat the file, so it is treated as disposable session state. The replay database, being in-memory only, persists nothing at all — which is exactly what you want from a scratch space.


## Why It Matters / Design Takeaways

Ingestion and station state are the spine of APRS-Command: almost everything the operator sees — the map, the station list, alerts, exports — is ultimately a view onto the store this pipeline maintains. Getting its shape right is what lets the rest of the app stay simple.

- One front door, many sources: every packet enters through AprsIngestionService, so parsing, logging, and de-duplication are written once and can't drift between sources.
- Fail soft, never crash: malformed packets are flagged and logged, not thrown — a mandatory property when your input is the open airwaves and the public internet.
- Two databases keep replay honest: playing back a recording can never overwrite the live picture, because replayed packets are filed in a separate, disposable store.
- Immutable snapshots rebuilt with copy-forward: each packet produces a fresh record that keeps what it didn't mention, so a station's folder composes a full picture from many partial reports without any field-mutation bugs.
- Freshness is computed, not stored: a coalesced timer re-ages every station against the current clock, so state reflects the passage of time without the app hammering the CPU or the screen.
- Memory that survives, cheaply: snapshots and labels persist to a self-pruning SQLite file on background threads; trails and replay state are treated as disposable and rebuilt on demand.

The deeper lesson is separation of responsibility. The ingestion service knows how to receive; the store knows how to remember and age; the SQLite wrapper knows how to persist; the coordinator knows when to refresh. None reaches into another's job. That is why a future maintainer can change the aging thresholds, swap the persistence backend, or add a new receive source without touching — or even fully understanding — the other three.


# 13. Replay, Simulation & Training

*The three transmit-safe ways to feed APRS-Command without a live radio — and the tagging that keeps every fake packet off the air.*


## What This Is / What It Is For

APRS-Command is a program for watching *APRS* traffic — the short digital position and message packets that ham radios broadcast on the air. Normally the packets arrive from a real radio plugged into the computer. But there are many times you want the app to be *doing something* without any radio attached: demonstrating it at a club meeting, practicing before a public-service event, debugging a display bug on a train with no antenna, or teaching a newcomer which button does what.

For all of those, APRS-Command has three ways to feed itself packets that never came from the air: **Replay**, **Simulation**, and **Training**. This chapter explains what each one is, why it was built as its own separate service, and — most importantly — the safety design that guarantees none of this invented traffic can ever accidentally get transmitted back out over a real radio.

> **The one rule that shapes everything here** — A ham radio license carries a legal duty not to transmit garbage. Fake stations, replayed old positions, and practice messages must NEVER reach the air. Every design choice in this chapter exists to make that impossible — not merely discouraged, but structurally unreachable.

Define the key term once: a *packet* is a single line of APRS text, for example `SIM001>APRS:!3903.50N/08430.50W-Fixed simulated station`. It names who sent it, where they are, and maybe a short comment. The whole app is a machine for turning lines like that into markers on a map. Replay, Simulation, and Training are simply three different *factories* that produce those lines when no radio is doing it.


### The three factories at a glance

The three features answer three different questions, so they are three separate services rather than one mode with switches. Keeping them apart means each can be tested, reasoned about, and turned on independently.

| Feature | Plain-language what | Where the packets come from | Service class |
| --- | --- | --- | --- |
| Replay | Play back real traffic you already recorded | A saved log file or the app's own raw packet log | ReplayService |
| Simulation | Invent believable traffic from nothing | A generator that fabricates stations, weather, objects, messages | SimulationService |
| Training | Run a guided practice scenario with a checklist | Orchestrates Simulation and/or Replay underneath | TrainingModeService |

Notice the layering: Training does not generate its own packets. It is a *conductor* that starts and stops the other two. That is a deliberate reuse decision — the practice scenarios get realistic traffic for free by leaning on machinery that already exists and is already tested.


### The source tag: one small enum that carries the whole safety story

Every packet that flows through APRS-Command carries a label saying where it came from. That label is an *enum* — a fixed list of named choices, like a drop-down menu the code chooses from — called **AprsPacketSource**, defined in `C:\Dev\APRS-Command\src\Aprs.Services\StationSnapshot.cs`.

```csharp
public enum AprsPacketSource
{
    Unknown,
    AprsIs,        // came from the internet APRS-IS servers
    Rf,            // came in over real radio
    TcpKiss,
    SerialKiss,
    Direwolf,
    Agwpe,
    Replay,        // <- played back from a log
    Simulation,    // <- invented by the generator
    External,
    LocalGenerated
}
```

This tiny list is load-bearing. Because `Replay` and `Simulation` are their own distinct values — not lumped in with real radio sources — every downstream part of the app can look at a packet and know, with certainty, that it is fake. The map can style it differently, the station list can filter it out, and the raw packet log can show a column that reads `Simulation` next to every synthetic line. The tag rides along on the `StationSnapshot`, the `StationTrailPoint`, and the raw log entry, so the fakeness is never lost.

> **Why a tag and not a separate window** — An earlier, simpler design might have shown replayed and simulated traffic in a totally separate throwaway view. Instead APRS-Command feeds it through the *same* map and lists as real traffic, tagged. That means you practice with the actual interface you will use for real — the markers, the trails, the detail popups are identical. The tag, not a parallel UI, is what keeps it honest.


## Replay: turning a recording back into a live map

*Replay* takes a file of packets you captured earlier — a real day of traffic — and feeds those lines back into the app one at a time, in their original time order, so the map comes alive again exactly as it did that day. Think of it as a DVR for radio traffic. It lives in `C:\Dev\APRS-Command\src\Aprs.Services\ReplayService.cs`.


### Step one: loading and understanding the file

Replay is forgiving about file formats because your recording might come from several places. `LoadFromFileAsync` reads all the lines, then `ParseLines` sniffs the first non-blank line to decide whether it is a spreadsheet-style *CSV* (comma-separated values, like a table saved as text) with a proper header, or just plain packet lines one per row.

```csharp
private IReadOnlyList<ReplayLogEntry> ParseLines(IReadOnlyList<string> lines)
{
    var firstLine = lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
    if (firstLine is not null && LooksLikeCsvHeader(firstLine))
    {
        return ParseCsvLines(lines);   // structured export with timestamps, source, notes
    }

    return lines
        .Select((line, index) => ParseTextLine(line, index))
        .Where(entry => entry is not null)
        .Select(entry => entry!)
        .ToArray();
}
```

The plain-text path (`ParseTextLine`) skips blank lines and lines starting with `#` (treated as comments), and can even peel a leading timestamp off the front of a line if one is present. If no timestamp is found, it invents an increasing one using the clock plus the line's position, so the entries still have a stable order to play in. Every loaded entry is then run through the real APRS parser so the app knows the callsign, position, and packet type before playback even starts.

After loading, the entries are sorted by their original timestamp and any time-window filter is applied, so playback marches forward in true chronological order:

```csharp
entries.AddRange(ApplyFilters(loaded));
entries.Sort((left, right) => left.OriginalTimestampUtc.CompareTo(right.OriginalTimestampUtc));
```


### Step two: playback, and where the source tag gets stamped

Playback walks the list. For each entry, `PlayNextAsync` builds a fresh copy stamped with the moment it is being replayed and, crucially, with its source overwritten to `Replay`:

```csharp
var replayTimestamp = clock.UtcNow;
var dispatchEntry = entry with
{
    ReplayTimestampUtc = replayTimestamp,
    PacketSource = AprsPacketSource.Replay   // stamped fake, always
};

await sink.PublishReplayPacketAsync(
    new ReplayPacketDispatch(dispatchEntry, dispatchEntry.RawPacketText,
                             replayTimestamp, AprsPacketSource.Replay),
    cancellationToken).ConfigureAwait(false);
```

The `with` keyword makes a copy of the record with just those fields changed — the original is a *record*, an immutable data object, so nothing is mutated in place. Note that even if the recording *said* a packet originally came from real radio, the replayed copy is forced to `Replay`. The recording's original source is preserved separately in the entry (as `originalPacketSource`) for reference, but what leaves this method is unambiguously tagged as a replay.

Replay also supports the controls you would expect from a media player — `Pause`, `Resume`, `Stop`, `SeekToEntryIndex`, `SeekToTimestamp`, an adjustable `SpeedMultiplier`, and a `LoopReplay` option — all implemented as simple state changes on a small `ReplaySessionState` state machine (Stopped, Loading, Ready, Playing, Paused, Completed, Faulted).


### Where replayed packets go — and the quarantine that keeps them from polluting live state

The `ReplayService` itself does not know about the map or the UI. It hands each packet to an *IReplayPacketSink* — a *sink* being simply a destination that accepts something and does whatever it wants with it. This is the seam that keeps the service testable: in a test the sink can just count packets; in the real app the sink routes them onto the map.

The real desktop sink is `LiveReplayPacketSink` in `C:\Dev\APRS-Command\src\Aprs.Desktop\Runtime\LiveReplayPacketSink.cs`:

```csharp
public Task PublishReplayPacketAsync(ReplayPacketDispatch dispatch, CancellationToken ct = default)
{
    if (!string.IsNullOrWhiteSpace(dispatch.RawPacketText))
    {
        ingestionService.IngestReceivedLine(
            dispatch.RawPacketText,
            AprsPacketSource.Replay,          // tag travels with it
            dispatch.ReplayTimestampUtc);
    }
    return Task.CompletedTask;
}
```

It calls `IngestReceivedLine` — the app's single **receive** pipeline. That is the same door real radio packets come in through, which is exactly why replayed traffic looks and behaves identically on the map. But look at what the ingestion service does with the `Replay` tag, in `AprsIngestionService.cs`:

```csharp
var target = source == AprsPacketSource.Replay && replayStationDatabase is not null
    ? replayStationDatabase   // a SEPARATE database, just for replay
    : stationDatabase;        // the real, live one
target.ProcessPacket(packet, source);
```

Replayed positions are applied to a *dedicated* replay station database, not the live one. The comment in the source says it plainly — a replay session can be shown on the map in isolation "without polluting — or clearing — the live station state." The desktop `LiveDataCoordinator` swaps the map over to this replay database when you enter replay mode and swaps back when you leave, and the live radio feed keeps quietly filling its own database underneath the whole time. Play back a two-hour-old log without losing track of who is on the air right now.

> **The two sinks pattern** — There is also a NoOpReplayPacketSink that accepts packets and throws them away. It is the safe default: a ReplayService constructed without a real sink simply produces nothing observable. You have to deliberately wire in the live sink to make replay appear on screen — feeding the map is opt-in, not accidental.


## Simulation: believable traffic invented from nothing

*Simulation* makes up APRS traffic that never existed — a handful of fixed stations, a couple of moving vehicles that actually drive around, a weather station reporting wind and temperature, an object or two, plus the occasional message and bulletin. It exists so you can see a busy, moving map with zero radio and zero recording. It lives in `C:\Dev\APRS-Command\src\Aprs.Services\SimulationService.cs`.


### How a batch of fake traffic is born

The heart is `GenerateNextBatchAsync`. Each call produces one round of packets — a snapshot of the pretend world at this instant — by asking a generator for each kind of station the configuration calls for:

```csharp
for (var i = 1; i <= Configuration.FixedStationCount; i++)
{
    generated.Add(CreatePacket(generator.GenerateFixedStationPacket(i, Configuration), now, "FixedStation", $"SIM{i:000}"));
    generated.Add(CreatePacket(generator.GenerateStatusPacket(i), now, "Status", $"SIM{i:000}"));
}

for (var i = 0; i < mobileStations.Count; i++)
{
    mobileStations[i] = generator.UpdateMobileStation(mobileStations[i], elapsed, Configuration);
    generated.Add(CreatePacket(generator.GenerateMobileStationPacket(mobileStations[i]), now, "MobileStation", mobileStations[i].Callsign));
}
```

The actual text is built by `SimulatedAprsPacketGenerator`. It produces real, valid-looking APRS packets — for example a fixed station is `SIM001>APRS:!3903.50N/08430.50W-Fixed simulated station`. The callsigns are deliberately unmistakable fakes: `SIM001`, `TESTWX1` for weather, `OBJTEST1` for objects, and `N0CALL` (the universally recognized "no real station" placeholder) for messages. Anyone glancing at the map can tell at once this is not real traffic.

The moving stations are genuinely simulated with basic dead-reckoning navigation — `UpdateMobileStation` converts speed in knots and a compass course into a real distance traveled since the last batch, nudges the latitude and longitude accordingly, and turns the station around if it wanders past the configured area radius so it stays on-screen:

```csharp
var distanceMeters = station.SpeedKnots * 0.514444 * elapsed.TotalSeconds;
var courseRadians = station.CourseDegrees * Math.PI / 180.0;
var deltaNorth = Math.Cos(courseRadians) * distanceMeters;
var deltaEast  = Math.Sin(courseRadians) * distanceMeters;
// ... convert meters to degrees and add to lat/lon ...
if (distance from center > AreaRadiusMeters)
    next = next with { CourseDegrees = (next.CourseDegrees + 180) % 360 }; // U-turn
```


### Every simulated packet is born tagged

Unlike replay, which stamps the tag during playback, simulation stamps it at the moment of creation. `CreatePacket` builds a `SimulatedAprsPacket` record whose source is fixed to `Simulation` — there is no code path that produces a simulated packet with any other source:

```csharp
private SimulatedAprsPacket CreatePacket(string rawPacket, DateTimeOffset timestamp, string packetKind, string? entityName)
{
    return new SimulatedAprsPacket(Guid.NewGuid(), rawPacket,
        AprsPacketSource.Simulation,   // hard-coded, not a parameter
        timestamp, Configuration.SimulationSourceName, packetKind, entityName);
}
```

From there the story mirrors replay exactly. The service pushes each packet to an `ISimulatedAprsPacketSink`; the desktop's `LiveSimulatedPacketSink` forwards it into `IngestReceivedLine` carrying the `Simulation` tag, so it flows onto the same map and lists as everything else — visibly labeled as fake. Simulated stations feed the *live* station database (there is no separate quarantine like replay has), which is fine precisely because the tag makes them filterable and identifiable everywhere they appear.


## Training: guided practice built on top of the other two

*Training* mode turns loose simulation and replay into structured, guided exercises — a named scenario with a difficulty level, a plain-language description, and a checklist of practice tasks you tick off. It is for learning the app deliberately rather than just watching it move. It lives in `C:\Dev\APRS-Command\src\Aprs.Services\TrainingModeService.cs`.


### A conductor, not a new instrument

The most important design fact about Training is what it does *not* do: it does not generate a single packet of its own. It holds references to the `ISimulationService` and `IReplayService` and simply starts, pauses, and stops them according to the selected scenario's settings:

```csharp
if (Configuration.UseSimulatedAprsSource && Configuration.AutoStartSimulation && simulationService is not null)
{
    await simulationService.StartAsync(cancellationToken).ConfigureAwait(false);
}

if (Configuration.UseReplaySource && Configuration.AutoStartReplay && replayService is not null)
{
    await replayService.StartReplayAsync(cancellationToken).ConfigureAwait(false);
}
```

Pause, resume, stop, and reset all forward straight through to both underlying services. This is why training inherits every safety property of the other two automatically — because the traffic it puts on screen *is* simulation and replay traffic, tagged `Simulation` and `Replay` just as before. There is no third, less-careful path to worry about.


### The scenarios and the checklist

`CreateBuiltInScenarios` ships eight ready-made exercises, from "Beginner APRS map familiarization" up to an "Alert response practice" rated Advanced. Each is a `TrainingScenario` record carrying a difficulty, a scenario type, a suggested duration, and a list of `TrainingScenarioTask` items — the checklist:

```csharp
CreateScenario("Beginner APRS map familiarization",
    TrainingScenarioDifficulty.Beginner,
    TrainingScenarioType.BasicMapFamiliarization,
    "Find simulated stations and inspect map markers.",
    ["Locate SIM001 on the map", "Open station details", "Identify packet source"],
    now)
```

You mark each task done through `SetTaskStatus`, and the service tracks progress. When every task is Completed or Skipped, the scenario flips to a `Completed` state and reports a percentage. Note the very last task of that beginner scenario — "Identify packet source" — literally teaches the newcomer to read the fake-vs-real tag this whole chapter is about.

Each built-in scenario is created with its notes field reading, verbatim, "Built-in offline training scenario using fake data only." Training also writes a running event trail through `AddEvent`, and every one of those entries is logged with `packetSource: AprsPacketSource.Simulation` — even the audit trail of a training session is stamped as non-real.


## The air-gap: why none of this can transmit

This is the payoff. Two independent design properties together make it structurally impossible for replayed, simulated, or training traffic to reach a real radio.


### Property one: these sinks only speak to the receive pipeline

Look again at both live sinks. `LiveReplayPacketSink` and `LiveSimulatedPacketSink` each call exactly one method: `ingestionService.IngestReceivedLine(...)`. That method's name is not decoration — as its own summary comment states, it is the "central receive pipeline," the door packets come *in* through. There is no transmit method anywhere in these code paths. A fake packet physically cannot travel toward the radio because the only wiring that exists points inward, toward the map. Transmitting would require calling code that these classes simply never reference.


### Property two: TransmitDisabled is hard-wired true, not a setting

Each of the three configuration records exposes a `TransmitDisabled` property — and in all three it is a computed value that is always `true`, with no setter to flip it:

```csharp
// ReplaySessionConfiguration.cs
public bool TransmitDisabled => true;

// SimulationConfiguration.cs
public bool TransmitDisabled => true;

// TrainingModeConfiguration.cs
public bool TransmitDisabled => true;
```

The `=>` makes it a read-only computed property. There is no `{ get; set; }` and no `init` — no configuration screen, no saved file, and no line of code can ever set it to `false`, because there is nothing to set. The UI honestly surfaces this to the operator: the simulation panel shows "Simulation transmit disabled" and the training panel shows "Training transmit disabled," both driven straight from that always-true flag. Even the app's REST API contract, `ReplayStatusDto`, defaults `TransmitDisabled` to `true`.

> **Belt and suspenders on purpose** — Either property alone would arguably be enough. Having both — no wiring to the transmit path AND a flag that cannot be turned off — is intentional defense in depth. If some future change accidentally created a transmit route, the always-true flag would still be there to gate it; if the flag were ever weakened, there would still be no code path to carry the packet outward. A licensed operator's legal duty deserves two locks, not one.


## Why It Matters / Design Takeaways

Replay, Simulation, and Training let APRS-Command be genuinely useful with no radio at all — for demos, for practice, for debugging on the couch, and for teaching. That flexibility could easily have been dangerous. The design makes it safe instead.

- **One tag, everywhere.** The AprsPacketSource enum marks every packet as Replay or Simulation at birth and carries that label onto the map, the trails, and the logs. Fakeness is never inferred or lost — it is stamped and permanent.
- **Reuse over reinvention.** Training generates nothing itself; it conducts Simulation and Replay. Simulation and Replay both feed the one shared receive pipeline. Fewer code paths means fewer places for a safety bug to hide.
- **Feed the real UI, not a toy.** Synthetic traffic runs through the exact same map and lists as real traffic, so what you practice on is what you will use for real — the only difference is the honest tag.
- **Quarantine where it counts.** Replayed positions land in a separate station database so reviewing an old log never disturbs — or erases — knowledge of who is on the air right now.
- **Safety by structure, not by discipline.** Nothing here relies on a developer remembering to be careful. The sinks only touch the receive door, and TransmitDisabled is a constant true with no setter. The unsafe action is not forbidden by policy — it is unreachable by construction.

If you maintain this code, treat those two air-gap properties as sacred. Adding a transmit capability to any of these paths, or making `TransmitDisabled` settable, would break the single most important promise the feature makes. Extend the factories all you like — new scenarios, richer simulated behavior, smarter replay filters — but the fake packets must always stay fake, tagged, and off the air.


# 14. Weather, GPS, Digipeater & iGate

*A tour of APRS-Command's four outward-facing services — and how each one earns the right to transmit.*


## What This Is / What It Is For

APRS-Command is fundamentally a listening station: it hears amateur-radio packets over the air and shows them on a map. But four of its feature services can also *talk back* — either onto the radio, onto the internet, or both. This chapter walks through those four: the **weather** services (reading a backyard weather station and, optionally, broadcasting its readings), **GPS/location** (knowing where the station physically is), the **digipeater** (a radio relay that repeats other people's packets so they travel farther), and the **iGate** (a bridge that copies packets between the radio world and the internet). They are the app's outward-facing muscle.

They are grouped together here for one reason above all others: each one can key a transmitter or push data onto a shared public network, and every single one of them ships *off*. Nothing beacons, repeats, or gates until a human deliberately turns it on and the central safety checks all pass. The rest of the chapter shows what each service does, why it was built the way it was, and exactly where its transmit path bends the knee to the app's single transmit-safety authority.

> **A note on the two identities** — The repository on disk is APRS-Command. The CLAUDE.md project file in this workspace describes a sibling project (Activation Planner) that shares an author and house style but no code. Everything in this chapter is verified against the real APRS-Command source under src/Aprs.Services and src/Aprs.Desktop/Runtime.


### The vocabulary, in plain words

A few terms recur throughout. Reading them once here makes the rest effortless.

| Term | Plain-language meaning |
| --- | --- |
| *APRS* | Automatic Packet Reporting System — a ham-radio convention where stations broadcast short text bursts (their location, weather, a message) that any nearby receiver can decode. |
| *Packet* | One of those short bursts. It has a sender (source callsign), a destination, a *path* (the list of relays it's allowed to hop through), and a payload (the actual information). |
| *RF* | Radio frequency — i.e. the packet arrived or left over the actual radio, as opposed to over a wire or the internet. |
| *APRS-IS* | APRS Internet Service — a worldwide network of servers that carries the same packets over the internet, so a station in Georgia can be seen from Germany. |
| *Callsign* | A ham operator's government-issued ID (e.g. KE4CON). Transmitting without a real one is both illegal and, here, blocked. |
| *Beacon* | To transmit your own information on a timer (your position, your weather) without being asked. |


## The safety spine every transmit path leans on

*What it does:* `TransmitSafetyAuthority` is a single gatekeeper object that every would-be transmission must ask permission from. It answers one question — "is it safe and legal to send this, right now, on this port?" — and returns either Allow or a specific Deny reason.

*Why it exists:* Without a central authority, each feature would re-implement its own "am I allowed?" logic, and the day one of them forgot a check is the day the app transmits as a placeholder callsign or during a training exercise. Centralizing the decision means a single, testable place holds the rules, and a single global "stop" switch can hard-block every path at once. The class documents its own priority order, highest first.

```csharp
// TransmitSafetyAuthority.Evaluate — evaluation order, highest priority first
// 1) Master inhibit wins over everything.
if (isInhibited)
    return TransmitDecision.Deny(TransmitDenyReason.GlobalInhibit, reason ?? "Transmit is inhibited.");

// 2) Identity: never transmit without a real callsign.
if (!policy.HasValidStationCallsign)
    return TransmitDecision.Deny(TransmitDenyReason.NoValidCallsign,
        "No valid station callsign is set. Transmit is blocked until a real callsign replaces the placeholder.");

// 3) Destination policy: APRS-IS transmit needs a real passcode.
if (request.Destination == TransmitDestination.AprsIs && !policy.HasValidAprsIsPasscode)
    return TransmitDecision.Deny(TransmitDenyReason.AprsIsPasscodeRequired, ...);

// 4) Per-port checks (enabled / transmit-enabled / connected / not receive-only).
var portResult = portManager.CheckTransmitSafety(request.PortId, globalTransmitSafetyEnabled: true);
if (!portResult.IsSafe)
    return TransmitDecision.Deny(TransmitDenyReason.Port, ...);

return TransmitDecision.Allow();
```

Line by line: the *global inhibit* (set when the operator enters an exercise or training mode) is checked first and blocks unconditionally — a drill must never leak real packets onto the air. Next, *identity*: a real callsign must be present, so the app can never key up as `N0CALL` or a blank placeholder. Third, a *destination rule*: sending to APRS-IS requires a valid passcode, because the internet side is receive-only without one. Only after all three does it delegate to the *per-port* check, which confirms that specific port is enabled, transmit-enabled, connected, and not receive-only. The inhibit flag is read under a `lock` so a mode toggle on the UI thread is seen consistently by an evaluation running on a background thread.

> **One gate, also a choke-point** — TransmitSafetyAuthority also implements ITransmitInhibitGate, the minimal contract the low-level transport code consults directly. So even a transmit path that forgot to call Evaluate() still hits the global inhibit at the wire. Belt and suspenders — the drill-mode block cannot be routed around.


## Weather: reading the sky, and optionally telling the world

*What it does:* The weather feature has two halves that never blur together. The *ingest* half reads observations from a physical or software weather station and displays them. The *beacon* half, if switched on, formats the newest reading into an APRS weather packet and transmits it on a timer.

*Why split this way:* Reading weather is harmless — it touches no transmitter. Broadcasting it is a transmission with your callsign on it. Keeping ingest and beaconing in separate services means you can watch your weather station all day with zero risk of ever sending anything, and the transmit half stays small, auditable, and off by default.


### Ingest: many stations, one common shape

APRS-Command speaks to a surprising range of weather hardware and software — WeatherFlow Tempest, Davis WeatherLink, Ambient Weather, Ecowitt/Fine Offset, Peet Bros Ultimeter, plus file imports from CumulusMX, WeeWX, and Weather Display, and a manual-entry path. Each has its own *driver* (a small adapter that knows one device's quirks). `WeatherInputDriverManager` registers these drivers and subscribes to a single event they all raise.

```csharp
// WeatherInputDriverManager.OnObservationReceived — every driver funnels through here
var observation = ApplyStaleState(args.Observation, args.ReceivedAtUtc, ...StaleDataThreshold);
var validator   = new WeatherObservationValidator(...StaleDataThreshold);
var validationResult = validator.Validate(observation, args.ReceivedAtUtc);
registration.LastObservation     = observation;
registration.LastValidationResult = validationResult;
// ...
if (!validationResult.IsValid)
    return;                     // bad reading never reaches the map
weatherDisplayService.UpsertWeatherStation(ToDisplayRecord(observation, args.DriverId, args.ReceivedAtUtc));
```

Every driver, no matter the vendor, hands its reading to the same funnel. The reading is normalized into a `CommonWeatherObservation` (a vendor-neutral record — wind, temperature, rain, pressure, humidity, and more), stamped *stale* if it is older than the driver's threshold, and validated. Only a valid reading is pushed to the display. This "many drivers, one common observation" design means the rest of the app — the map, the graphs, the beacon formatter — never has to know or care which brand of station produced a number.


### Beaconing: the transmit half, guarded twice

`WeatherBeaconScheduler` owns the outbound side. Its configuration ships fully off — `WeatherBeaconEnabled: false`, `AprsIsWeatherTransmitEnabled: false`, `RfWeatherTransmitEnabled: false` — with a default 30-minute interval and a hard 5-minute floor (`MinimumAllowedTransmitInterval`) so an operator cannot accidentally flood the network. Before any transmission it builds a preview, checks the reading is not stale (if `RejectStaleData` is set), validates the packet looks well-formed, and confirms the local station profile permits the chosen transport.

```csharp
// WeatherBeaconScheduler.TransmitWeatherNowAsync — the two-transport fork
if (destinationTransport == WeatherBeaconTransmitTransport.AprsIs)
{
    var aprsIsResult = await aprsIsClient.SendRawPacketAsync(
        preview.Packet,
        configuration.RequireConfirmationBeforeTransmit,   // explicit opt-in gate
        cancellationToken).ConfigureAwait(false);
    // ...
}
else
{
    var rfResult = await rfTransmitClient!.SendBeaconAsync(preview.Packet, cancellationToken)...;
}
```

The eligibility checks (`IsAprsIsEligible` / `IsRfEligible`) require the local profile to be valid, `TransmitEnabled` to be true, the specific transport (APRS-IS or RF) to be individually enabled, and — for APRS-IS — the client to actually be connected. The `RequireConfirmationBeforeTransmit` flag is threaded down into the send call as an explicit opt-in. So a weather packet clears both the scheduler's own eligibility wall and the underlying transmit client's safety before a single byte leaves. `TickAsync` is the timer heartbeat: it only fires when the scheduler is enabled and the clock has passed the next scheduled time, and it picks APRS-IS first, then RF, then reports a block if neither transport is on.


## GPS / location: knowing where you are

*What it does:* The GPS service turns a stream of position sentences from a receiver into a single, always-current answer to "where is this station?" It never transmits anything — it only knows.

*Why it matters and how it's shaped:* A mobile station's position feeds beacons, the map's "me" marker, and dead-reckoning. But GPS data arrives piecemeal — one sentence carries latitude and longitude, the next carries altitude, another carries satellite count. If the service simply replaced its state with each sentence, a message that lacked altitude would erase the altitude it already knew. The fix is a *merge*: each new field overwrites only if present, otherwise the previous value survives.

```csharp
// GpsService.Merge — new field wins only if it has a value, else keep the old one
return new GpsPosition(
    update.Latitude     ?? current.Latitude,
    update.Longitude    ?? current.Longitude,
    update.AltitudeMeters ?? current.AltitudeMeters,
    update.SpeedKnots   ?? current.SpeedKnots,
    // ... every field follows the same ?? pattern
    update.FixValid,                              // fix validity always taken fresh
    ...);
```

The `??` is C#'s *null-coalescing operator*: "use the left value, but if it's null, fall back to the right." So a satellites-only update refreshes the satellite count and leaves your coordinates intact. `HasValidFix` is deliberately strict — it demands `FixValid == true` *and* a non-null latitude and longitude — so the rest of the app never trusts a position that the receiver itself hasn't confirmed.


### Two sources, one background loop

`GpsCoordinator` (in the desktop Runtime layer) wires the service to hardware. It supports two mutually exclusive sources: a *serial NMEA* source (a USB or serial GPS puck speaking the standard NMEA sentence format) or a *GPSD* client (a TCP connection to the Linux `gpsd` daemon, common on Raspberry Pi). It runs one background read loop, feeds each incoming reading through the service, and raises `PositionUpdated` for the UI.

```csharp
// GpsCoordinator.Start — pick exactly one source, GPSD taking priority
public void Start()
{
    if (gpsdClient is not null)     StartGpsd();
    else if (serialSource is not null) StartSerial();
}
```

Only one source runs. The GPSD path even routes its already-parsed positions through a *synthetic* `GpsdParseResult` so they flow through the exact same `AcceptGpsdReport` code as real GPSD traffic — one update path, not two. On shutdown, `DisposeAsync` cancels the loop, awaits it, and disposes the client, so the background task never outlives the coordinator.


## The digipeater: a disciplined radio relay

*What it does:* A *digipeater* (digital repeater) listens for packets on the air and re-transmits them so they reach farther than the original sender's radio could. It's how a low-power handheld in a valley still gets heard across a county.

*Why it's the most safety-critical service:* A digipeater keys your transmitter automatically, in response to other people's traffic, with no human in the loop for each repeat. Done carelessly it becomes a menace — repeating packets endlessly, echoing its own output, or amplifying interference. `DigipeaterService` is therefore built as a long gauntlet of blocking checks, and it repeats a packet only if *every* check passes.

```csharp
// DigipeaterService.EvaluateBlocked — the opening gates (each returns a block reason)
if (!runtimeEnabled)                 return (TransmitDisabled, "Digipeater is disabled.", ...);
if (!configuration.DigipeaterEnabled) return (TransmitDisabled, "Digipeater mode is disabled.", ...);
if (!configuration.RfTransmitEnabled) return (TransmitDisabled, "RF transmit is disabled ...", ...);
if (string.IsNullOrWhiteSpace(configuration.RfTransmitPort))
                                     return (TransmitDisabled, "No RF transmit port is selected.", ...);

var portSafety = portManager.CheckTransmitSafety(configuration.RfTransmitPort, ...);
if (!portSafety.IsSafe)              return (TransmitDisabled, portSafety.FailureReason ..., ...);

// Central transmit authority (when wired): global inhibit + identity gate on top of the port check.
if (transmitSafety is not null)
{
    var decision = transmitSafety.Evaluate(new TransmitRequest(configuration.RfTransmitPort, TransmitDestination.Rf));
    if (!decision.IsAllowed) return (TransmitDisabled, decision.Explanation, [decision.Explanation]);
}
```

Notice the layering: the service checks its own enable flags and the per-port safety, and *then* consults the shared `TransmitSafetyAuthority` from the previous section. That's how the exercise-mode global inhibit and the never-transmit-as-a-placeholder identity rule reach the digipeater without it re-implementing them. The defaults in `DigipeaterConfiguration.Default` are all conservative: `DigipeaterEnabled: false`, `RfTransmitEnabled: false`, duplicate suppression on, at most 10 repeats per minute and 3 per station.


### The two rules that stop a relay from destroying itself

Beyond the enable gates, two checks matter most. The first is *loop prevention*: a digipeater must never repeat a packet that already carries its own callsign as a used hop, or it would echo its own transmissions forever.

```csharp
// AlreadyRepeatedByUs — refuse anything already stamped with our own callsign
return packet.Path.Any(component =>
    IsUsed(component)                                        // hop marked used (ends with '*')
    && string.Equals(StripUsedMarker(component), mine, StringComparison.OrdinalIgnoreCase));
```

The second is *duplicate suppression*. The same packet often reaches a digi from several neighbors at once. The fingerprint used to spot a repeat deliberately *excludes the path* — because the path is exactly what changes as different relays stamp it — and keys on source + destination + payload instead, matching the standard APRS dupe-detection basis. Within the 30-minute window, a packet already repeated once is dropped.

```csharp
// BuildFingerprint — identity of a packet, independent of who relayed it
return string.Join("|", source.ToUpperInvariant(), destination.ToUpperInvariant(), information);
```

The path-rewriting logic (`EvaluatePath`) also honors the APRS *WIDEn-N* convention: a `WIDE2-2` alias means "repeat me up to two more hops." The digi consumes one hop, decrements the counter to `WIDE2-1`, and stamps itself in — and it explicitly traps the malformed `remaining > total` case (e.g. `WIDE2-3`), a known abuse pattern, by refusing to propagate it.


## The iGate: a one-way bridge with hard rules

*What it does:* An *iGate* (internet gateway) copies packets it hears on the radio up onto APRS-IS, so RF-only stations become visible on the global internet map. `IGateService` handles that RF-to-internet direction.

*Why its rules are non-negotiable:* Gating is powerful and easy to abuse — gate the wrong packet and you create an internet feedback loop, or you republish a packet whose sender explicitly asked to stay off the internet. So alongside the user-tunable filters, the iGate enforces a set of *mandatory* blocks that no setting can switch off.

```csharp
// IGateService.MandatoryNoGateReason — always-on "do not gate" rules
if (!string.IsNullOrEmpty(candidate.QConstruct))
    return "Packet carries a q-construct, so it originated on APRS-IS (loop prevention).";

foreach (var component in candidate.Path)
{
    var element = component.Trim().TrimEnd('*');
    if (element.Equals("NOGATE", ...)) return "Path contains NOGATE — the sender opted this packet out ...";
    if (element.Equals("RFONLY", ...)) return "Path contains RFONLY — the sender restricted this packet to RF.";
    if (element.StartsWith("TCPIP", ...) || element.StartsWith("TCPXX", ...))
        return "Path shows the packet already traversed APRS-IS (loop prevention).";
}
```

Reading these: a *q-construct* is a marker APRS-IS servers add to show a packet came from the internet; seeing one means gating it back would loop. `NOGATE` and `RFONLY` are the sender's own written instructions to keep the packet off the internet — the iGate obeys them absolutely. `TCPIP`/`TCPXX` in the path mean the packet already crossed APRS-IS. These four are checked before any user filter, so a misconfigured allow-list can never override them.


### Then the tunable layer, then the transmit

After the mandatory blocks come the adjustable ones: source-port allow-lists, packet validity, per-type toggles (`IGateConfiguration.Default` gates position, weather, object/item, and message packets, but leaves third-party and telemetry off), duplicate suppression, path allow/block patterns, and rate limits (10/minute overall, 3 per station). Only when all of that passes does it hand the raw packet to the APRS-IS client — carrying the same `RequireExplicitConfirmationBeforeEnabling` opt-in flag seen elsewhere.

```csharp
// IGateService.EvaluateAndGateAsync — the actual gate, after every check has passed
var transmitResult = await aprsIsClient.SendRawPacketAsync(
    candidate.RawPacket,
    configuration.RequireExplicitConfirmationBeforeEnabling,
    cancellationToken).ConfigureAwait(false);
```

> **Sibling: the iGate monitor** — IGateMonitorService is the read-only companion. It watches both the RF and APRS-IS sides, remembers which packets it has already seen on the internet, and marks RF packets that were also seen on APRS-IS — feeding the duplicate detection above. It decides nothing and transmits nothing; it only observes, which is why it needs no safety authority at all.


### Aside: the After-Action & ICS Export Family

Alongside the live services sits a small family of *export generators* that turn a session's data into the paperwork a served agency expects. They live in `src/Aprs.Desktop/Services/Ics*ExportService.cs`, and each is a static class with a single `Generate…` method — no state, no I/O — that takes plain snapshot records (stations, messages, roster) and returns formatted text.

The family covers the FEMA/NIMS forms an EmComm operator files: **ICS-205** (communications plan), **ICS-211** (check-in list), **ICS-213** (one general-message form per message), **ICS-214** (activity log), and **ICS-309** (a single chronological communications log of every message). The `AfterActionExportViewModel` gathers the session's stations, messages, and net-control roster, lets the operator tick which forms to include, and calls each generator — raising one save-file request per file produced.

The shape is deliberate: because every generator is a *pure function over snapshot records*, it is trivial to unit-test (feed inputs, assert the text) and carries no dependency on the running app — the same discipline that keeps the parsers pure. Adding a new form is correspondingly small: ICS-309 was added as exactly one new static class plus one checkbox bound to a boolean, nothing more.


## Why It Matters / Design Takeaways

These four services are where APRS-Command stops being a passive viewer and starts affecting the shared radio and internet networks — which is precisely why they are engineered to be timid. Read-only is always separated from transmit (weather ingest from weather beaconing; the iGate monitor from the iGate). Every configuration default is off. Every transmit path, no matter which feature owns it, ultimately answers to the one `TransmitSafetyAuthority`, so the global exercise-mode inhibit and the real-callsign requirement can never be bypassed by a forgetful feature.

- *Off by default, everywhere.* Digipeater, iGate, and weather-beacon configs all ship with their enable flags false and explicit-confirmation flags true — silence until a human opts in.
- *One safety authority, consulted by all.* The digipeater and every beacon route through TransmitSafetyAuthority.Evaluate, which enforces global inhibit, real-callsign identity, passcode, and per-port checks in a fixed priority order.
- *Read is divorced from transmit.* You can ingest weather and monitor the iGate indefinitely with zero transmit risk, because those halves live in separate services.
- *Mandatory rules outrank user settings.* The iGate's NOGATE/RFONLY/q-construct/TCPIP blocks and the digipeater's loop-prevention run before any tunable filter and cannot be switched off.
- *Normalize early, decide simply.* Many weather vendors collapse into one CommonWeatherObservation, and piecemeal GPS sentences merge field-by-field, so downstream code stays vendor- and protocol-agnostic.
- *Rate limits and duplicate windows are built in.* Both the digipeater and iGate cap repeats per minute and per station and suppress duplicates over a 30-minute window, so a single busy channel can't turn the station into a firehose.


# 15. Persistence & Settings

*How APRS-Command remembers who you are and what you prefer between runs — a single plain-text JSON file, loaded so carefully it can never stop the app from starting.*


## What This Is / What It Is For

Every program that is worth using has to *remember things* — who you are, where you are, which knobs you have set — so you do not re-enter them every single time you open it. This chapter is about the part of APRS-Command that does that remembering: the *persistence* layer (a fancy word for "the code that saves your stuff to disk and reads it back next time") and the *settings store* that sits on top of it.

The whole design rests on one deliberately boring decision: everything the app remembers is written to a single, plain, human-readable text file called `settings.json`. You could open it in Notepad and read it. That choice — plain text over a database, one file over many — shapes everything else in this chapter, and there are good reasons for it that we will walk through.

Two things make this layer worth a careful read even though "save a file" sounds trivial. First, the loader is built so that it can *never* be the reason the app fails to start — a missing file, a half-written file, or a file somebody hand-edited into garbage all resolve to something usable instead of a crash. Second, the *defaults* baked into a fresh install are chosen for safety: a new station is anonymous and silent (it listens but will not transmit) until the operator deliberately turns transmitting on. In ham radio that is not a nicety; transmitting with a bad configuration can put wrong data on the air under your callsign.

> **Where this lives** — All the files discussed here are in src/Aprs.Desktop/Configuration/ — chiefly JsonAppSettingsStore.cs, AppSettings.cs, StationProfile.cs, DistanceUnit.cs, plus the per-feature section records (ConnectionSettings.cs, IGateSettings.cs, and so on). The folder-path helper is ApplicationFolderLayout.cs over in src/Aprs.Services/.


### The big picture: one tree, one file

*What it does:* Everything the app persists hangs off a single object called `AppSettings`. Think of it as a filing cabinet with one labeled drawer per feature — one drawer for your station identity, one for your radio connections, one for the iGate feature, one for GPS, and so on. Saving the settings means writing the whole cabinet to disk as one JSON file; loading means reading that one file back into the cabinet.

*Why built this way:* The alternative — a scattering of little files, or a small embedded database like SQLite — was rejected on purpose. One file is trivial to back up (copy it), trivial to inspect (open it), trivial to reason about (there is no "which file wins" question), and trivial to reset (delete it and you are back to a clean install). A database would add a dependency and a binary format you cannot eyeball. For a single-user desktop app whose settings are measured in kilobytes, plain JSON is the right amount of technology.

```csharp
public sealed record AppSettings(
    int SchemaVersion,
    StationProfile Station,
    ConnectionSettings Connections,
    IGateSettings IGate,
    DigipeaterSettings Digipeater,
    AudioSettings Audio,
    WindowStates Windows,
    GpsSettings Gps,
    ManagedModemSettings ManagedModem,
    bool DarkMode,
    MessageTemplatesSettings MessageTemplates,
    SmartBeaconingSettings SmartBeaconing,
    GpsdSettings Gpsd,
    FrequencyReferenceSettings FrequencyReference,
    NetScriptSettings NetScripts,
    WinlinkSettings Winlink,
    SessionTemplateSettings SessionTemplates,
    VoiceSettings Voice,
    RepeaterBookSettings RepeaterBook,
    CalTopoSettings CalTopo)
```

This is `AppSettings.cs`. It is a C# *record* — a compact way to declare an immutable data object (immutable means: once created, its fields never change; to "change" one you make a fresh copy with the one field swapped). Each parameter is a *section*: `Station` holds identity and position, `Connections` holds the list of radios/network links, `IGate` holds the internet-gateway options, and so on. The very first field, `SchemaVersion`, is a version stamp we will come back to.

The file's own comment states the payoff of this shape directly: adding a whole new area of configuration is just adding one more section property here — *no change to the storage code itself*. The saver already writes the entire tree; the loader already reads the entire tree. A new feature that needs to remember something drops in its own little settings record, adds one line to `AppSettings`, and it persists for free.

```csharp
public const int CurrentSchemaVersion = 2;

public static AppSettings Default { get; } = new(
    SchemaVersion: CurrentSchemaVersion,
    Station: StationProfile.Default,
    Connections: ConnectionSettings.Default,
    IGate: IGateSettings.Default,
    Digipeater: DigipeaterSettings.Default,
    ...
    DarkMode: false,
    ...);
```

Every section knows its own sensible starting point through a static `Default`, and `AppSettings.Default` simply assembles all of them. This is the object you get on a brand-new install where no file exists yet. Notice there is one authoritative default per section, defined next to that section's own code — so the person who owns the iGate feature owns its defaults, not some central list far away.


### The StationProfile: who you are, and why you start as "N0CALL"

*What it does:* `StationProfile` is the drawer holding your on-air identity and everything tied to it — your callsign, your position, the map symbol that represents you, how often you beacon (announce your position), and the switches that control whether you transmit at all.

```csharp
public sealed record StationProfile(
    string Callsign,
    int Ssid,
    double Latitude,
    double Longitude,
    int FilterRadiusKm,
    char SymbolTable,
    char SymbolCode,
    string StationComment,
    string BeaconPath,
    int AprsIsBeaconMinutes,
    int RfBeaconMinutes,
    bool FixedStationMode,
    bool TransmitEnabled,
    bool AprsIsTransmitEnabled,
    bool RfTransmitEnabled,
    string? PhgData,
    DistanceUnit DistanceUnit = DistanceUnit.Miles,
    int Ring1Distance = 10,
    int Ring2Distance = 25,
    int Ring3Distance = 50)
```

A few terms defined in passing, since APRS is dense with them. A *callsign* is your government-issued radio identifier (the author's is KE4CON). An *SSID* is a small number tacked onto it to distinguish your gadgets — KE4CON-7 might be your handheld, KE4CON-9 your car. A *symbol* is the little icon that marks you on everyone's map, encoded as two characters (a table character and a code character). *Beaconing* is broadcasting your position on a timer. A *beacon path* (here `WIDE1-1,WIDE2-1`) tells the network how far to relay your beacon.

```csharp
public static StationProfile Default { get; } = new(
    Callsign:             "N0CALL",
    Ssid:                 0,
    Latitude:             39.5,
    Longitude:            -98.35,
    FilterRadiusKm:       200,
    SymbolTable:          '/',
    SymbolCode:           '-',
    StationComment:       "APRS Command",
    BeaconPath:           "WIDE1-1,WIDE2-1",
    AprsIsBeaconMinutes:  30,
    RfBeaconMinutes:      60,
    FixedStationMode:     true,
    TransmitEnabled:      false,
    AprsIsTransmitEnabled:false,
    RfTransmitEnabled:    false,
    PhgData:              null,
    ...);
```

Look at the defaults with a safety eye. The callsign is `N0CALL` — a well-known placeholder in ham radio meaning "not a real station." The position (39.5, -98.35) is the geographic center of the continental United States, a neutral "somewhere in the middle" until you set your real spot. And the three transmit flags — `TransmitEnabled`, `AprsIsTransmitEnabled`, `RfTransmitEnabled` — are all *false*.

> **Receive-first is a safety stance, not a default nobody thought about** — A fresh install listens and shows you traffic, but it will not put a single packet on the air until you deliberately enable transmitting. This prevents the worst first-run mistake in APRS: an un-configured station beaconing a placeholder callsign at the center of the country onto the live network. The master switch (TransmitEnabled) and the two channel switches (APRS-IS and RF) all start off, so it takes a conscious choice to go from silent to transmitting.

The record also carries small conveniences that keep this logic in one place. `IsConfigured` returns true only when the callsign is present and is not still `N0CALL` — a clean way for the rest of the app to ask "has this operator actually set themselves up?" `FullCallsign` stitches the SSID back on when it is non-zero ("KE4CON-7"), and `SymbolDisplay` joins the two symbol characters for display. These are computed on demand, so they can never drift out of sync with the raw fields.

```csharp
public static StationProfile Load() => JsonAppSettingsStore.Default.Load().Station;

public void Save() => JsonAppSettingsStore.Default.Update(s => s with { Station = this });
```

These two lines are the bridge from the profile to the storage engine. `Load()` pulls the whole settings tree through the shared store and hands back just the `Station` slice. `Save()` does the careful thing — it does not overwrite the whole file with a station-only view; it uses `Update` to swap *only* the station section and leave every other drawer untouched. The `s with { Station = this }` syntax is the record "copy with one change" move mentioned earlier.


### How the pieces are turned into text: the JSON options

*What it does:* A single shared configuration object tells .NET's built-in JSON library exactly how to translate the settings objects to and from text. Getting these options right is what makes the saved file readable, tolerant of hand edits, and stable over time.

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter() }
};
```

Each option earns its place. `WriteIndented = true` pretty-prints the file with line breaks and indentation so a human can actually read it. `PropertyNamingPolicy = CamelCase` writes field names in the JavaScript style (`filterRadiusKm`) that looks natural in a `.json` file. `PropertyNameCaseInsensitive = true` means reading back does not care about capitalization — so a file written by an older build with different casing still loads. `DefaultIgnoreCondition = WhenWritingNull` skips fields that are empty, keeping the file tidy rather than littering it with `null`s.

The last one is the star of this section. `JsonStringEnumConverter` controls how an *enum* is written. An enum is a fixed menu of named choices — `DistanceUnit` is either `Miles` or `Kilometres`, nothing else. By default .NET would save an enum as a bare number: `Miles` becomes `0`, `Kilometres` becomes `1`. This converter makes it save the *name* instead.

```csharp
public enum DistanceUnit
{
    /// <summary>Miles (default for US operators).</summary>
    Miles,

    /// <summary>Kilometres (default for operators outside the US).</summary>
    Kilometres
}
```

| Approach | What the file contains | What happens if someone reorders the enum later |
| --- | --- | --- |
| Default (numbers) | "distanceUnit": 0 | 0 now means something else — every saved file silently changes meaning |
| JsonStringEnumConverter (names) | "distanceUnit": "Miles" | Names still match by name; reordering is harmless |

> **Why names beat numbers** — Saving "Miles" instead of 0 does two things at once. It makes the file self-explanatory — you can read your own settings without a decoder ring — and it makes the format resilient: if a future developer inserts a new option in the middle of the enum, the numeric positions all shift, but a file that stored the name "Miles" still lands on Miles. This is exactly the kind of quiet decision that keeps old files loadable years later.

The same converter is why the connection settings can round-trip things like a serial-port *parity* setting (`SerialKissParity.Even`) or a port type (`ConnectionPortType.NetworkTncKiss`) as readable words in the file. The test suite explicitly checks that an enum and a `TimeSpan` survive a save-and-reload unchanged, which is the guarantee this converter underwrites.


### The loader that refuses to fail

The interface that defines the store, `IAppSettingsStore`, states the contract in one uncompromising sentence: implementations *must never throw on load*. A missing or corrupt file has to fall back to defaults so the app always starts. Everything in `JsonAppSettingsStore.Load()` exists to honor that promise. It defends on four fronts.

```csharp
public AppSettings Load()
{
    if (!File.Exists(settingsFilePath))
    {
        var migrated = TryImportLegacy();
        if (migrated is not null)
        {
            Save(migrated);          // persist the imported profile in the new location
            return migrated;
        }
        return AppSettings.Default;
    }

    string json;
    try { json = File.ReadAllText(settingsFilePath); }
    catch { return AppSettings.Default; }  // unreadable file: start from defaults, never crash

    // Fast path: deserialize the whole file at once.
    try
    {
        var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        if (settings is not null) return Normalize(settings);
    }
    catch { /* fall through to per-section salvage */ }

    return SalvageLoad(json);
}
```

*Front one — nothing saved yet:* if the file does not exist, try a one-time legacy import (covered below), and otherwise return `AppSettings.Default`. Note the file is *not* written at this point — a first run with no configuration leaves the disk untouched until you actually save something, which the test `Load_WithNoFile_ReturnsDefaults` verifies by asserting the file still does not exist.

*Front two — file present but unreadable* (locked by another process, permissions, whatever): the `try/catch` around `File.ReadAllText` swallows the failure and returns defaults. The app opens; it does not die because a file was busy.

*Front three — the fast path:* in the normal case it deserializes the whole file in one shot and hands off to `Normalize` (which fills in any missing sections and runs migrations). This is the path taken every ordinary launch.

*Front four — salvage:* if that whole-file parse throws — because someone hand-edited the file and broke it — it does not give up on everything. It falls through to `SalvageLoad`.

```csharp
private static AppSettings SalvageLoad(string json)
{
    JsonObject? root;
    try { root = JsonNode.Parse(json) as JsonObject; }
    catch { return AppSettings.Default; }
    ...
    var station = TryDeserializeSection(root, "station", StationProfile.Default);
    ...
    var connections = (TryDeserializeSection(root, "connections", ConnectionSettings.Default)
        ?? ConnectionSettings.Default).Normalized();
    ...
}
```

*Salvage reads the file drawer by drawer.* Instead of demanding that the entire file be valid, it parses the JSON into a loose tree and then deserializes *each section independently*, dropping back to that one section's default if it is malformed. So if you botch an edit to the station section, you lose only the station section — your carefully configured list of radio connections survives intact. The test `Load_WithBrokenStationSection_SalvagesConnections` deliberately corrupts just the station block and confirms the connections come back with the right host address. This is the difference between "one typo resets everything" and "one typo resets one thing."

> **Why swallowing these exceptions is correct here — and the exception to the rule** — The project's coding standards say never swallow exceptions silently, and that is the right default. The loader is the deliberate exception: the entire point is that a bad file must never stop the app from starting, so each failure quietly degrades to a safe fallback rather than propagating. The trade-off is accepted knowingly and documented in the interface contract itself. This is not sloppiness leaking through — it is a narrow, intentional carve-out at exactly the boundary where a crash would hurt most.


### Saving without ever corrupting the file

*What it does:* Writing settings uses an *atomic write* — a technique that guarantees the real file is either the complete old version or the complete new version, and never a half-written mess in between.

```csharp
public void Save(AppSettings settings)
{
    ArgumentNullException.ThrowIfNull(settings);

    // Always persist with the current schema version stamped.
    var toWrite = settings with { SchemaVersion = AppSettings.CurrentSchemaVersion };
    ...
    var json = JsonSerializer.Serialize(toWrite, JsonOptions);

    // Atomic write: temp file then move-with-overwrite so a partial write cannot corrupt the file.
    var tempPath = settingsFilePath + ".tmp";
    File.WriteAllText(tempPath, json);
    File.Move(tempPath, settingsFilePath, overwrite: true);
}
```

*Why built this way:* if the code wrote directly into `settings.json` and the machine lost power (or the app was killed) halfway through, you would be left with a truncated, unparseable file — your configuration gone. The fix is a two-step: write the full new content to a scratch file (`settings.json.tmp`), then `File.Move` it over the real file. On a normal filesystem a move-with-overwrite is effectively instantaneous and all-or-nothing. If a crash happens during the write, it happens to the throwaway `.tmp` file; the real `settings.json` still holds the last good version. The test `Save_IsAtomic_NoLeftoverTempFile` confirms the real file exists and no stray `.tmp` is left behind afterward.

Two more details in `Save` matter. `ArgumentNullException.ThrowIfNull` means the *save* path does throw on a genuinely invalid call — the never-throw promise is specifically about *load*, where a fallback is always possible; being handed a null to save is a programming bug worth surfacing. And `settings with { SchemaVersion = CurrentSchemaVersion }` means every save re-stamps the file with today's format version, so the version marker is always truthful.


### Change one drawer, keep the rest: the Update method

```csharp
public AppSettings Update(Func<AppSettings, AppSettings> mutate)
{
    ArgumentNullException.ThrowIfNull(mutate);

    var updated = mutate(Load());
    Save(updated);
    return updated;
}
```

*What it does:* `Update` is the safe way to change one section. You hand it a small function describing the change ("take the current settings, give me back a copy with a new station"), and it loads the current file, applies your change, saves the result, and returns it. Because it always starts from a fresh `Load`, it can never accidentally wipe a section that some other part of the app changed a moment ago — it reads, tweaks, writes. This is why `StationProfile.Save()` routes through `Update` rather than `Save`: it changes the station and provably leaves connections, GPS, window positions, and everything else exactly as they were. The test `Update_ChangesOneSection_PreservesOthers` locks that behavior in.


### Growing the format safely: schema versions and migration

*What it does:* The `SchemaVersion` stamp lets the app read files written by *older* versions of itself and quietly upgrade them, rather than choking on an outdated shape. `CurrentSchemaVersion` is `2` today.

```csharp
private static AppSettings Migrate(AppSettings settings)
{
    if (settings.SchemaVersion >= AppSettings.CurrentSchemaVersion)
    {
        return settings;
    }

    // v1 -> v2 introduced the connection port-list. A v1 file's old flat connections object has
    // no "ports" array, so it deserializes to an empty port list and Normalize() above has
    // already replaced it with the default single APRS-IS port. Nothing more is needed here; the
    // version is simply stamped forward.

    return settings with { SchemaVersion = AppSettings.CurrentSchemaVersion };
}
```

*Why built this way:* formats change as features are added, and users must never lose data because they upgraded the app. The real migration this code has already lived through is instructive. Version 1 stored connection settings as a single flat object (one TCP link, one APRS-IS login). Version 2 changed that to a *list* of ports so an operator can run several radios and network links at once. When a v1 file is read, its old flat connection block has no `ports` array, so it comes through as an empty list — and `Normalize` (via `ConnectionSettings.Normalized()`) replaces an empty list with the default single receive-only APRS-IS port. The `Migrate` step then just stamps the version forward to 2. The test `Load_MigratesPrePortListConnections_ToDefaultPort` feeds in a genuine hand-written v1 JSON file and confirms it comes back as a valid v2 with one APRS-IS port and the operator's callsign preserved.

> **The migration hook is deliberately a no-op today** — Migrate() currently does no data transformation — the v1-to-v2 change was absorbed entirely by the "missing section becomes its default" behavior. But the method exists and is wired into the load path so that the day a future format change does need real rewriting (say, splitting a field in two), there is one obvious, tested place to add that step — applied incrementally, version by version, so saved data is never dropped.


### Not losing a returning operator: legacy import

```csharp
private AppSettings? TryImportLegacy()
{
    if (string.IsNullOrWhiteSpace(legacyStationProfilePath) || !File.Exists(legacyStationProfilePath))
    {
        return null;
    }

    try
    {
        var legacy = JsonSerializer.Deserialize<StationProfile>(
            File.ReadAllText(legacyStationProfilePath), JsonOptions);

        if (legacy is not null && !string.IsNullOrWhiteSpace(legacy.Callsign))
        {
            return AppSettings.Default with { Station = legacy };
        }
    }
    catch { /* ignore a bad legacy file; caller falls back to defaults */ }

    return null;
}
```

*What it does:* An earlier build of APRS-Command saved just the station profile to its own little file (`station-profile.json`) in a different folder. When the new unified store finds no `settings.json` yet, it looks for that old file first, and if it holds a real callsign, it lifts the profile into a fresh settings tree and saves it in the new location. The operator upgrades and their identity is simply there — no re-entry. The whole thing is wrapped in a `try/catch` so a corrupt legacy file is ignored rather than fatal, and it only runs on first launch (once a `settings.json` exists, this path is never taken again). The `Default` store wires up the old path automatically; the tests `Load_ImportsLegacyStationProfile...` and `Load_IgnoresMissingLegacyFile` cover both the hit and the miss.


### Where the file actually lives, and the test-only twin

The default store does not hardcode a path; it asks `ApplicationFolderLayout` for the standard per-user application-data folder ("APRS Command" under the OS's roaming app-data location) and places `settings.json` in that folder's `config` subfolder. `ApplicationFolderLayout` is the single source of truth for *every* folder the app uses — logs, map cache, exports, backups, plugins — so paths are consistent and defined in exactly one place. Its `GetDefaultApplicationDataFolder` also degrades gracefully across platforms, trying roaming app-data, then local app-data, then the user profile, then the executable's own folder, so it always resolves to something writable on Windows, macOS, and Linux alike.

```csharp
public sealed class InMemoryAppSettingsStore : IAppSettingsStore
{
    private AppSettings settings;
    ...
    public AppSettings Load() => settings;
    public void Save(AppSettings value) { ... settings = value with { SchemaVersion = ... }; }
}
```

Because the store is defined behind the `IAppSettingsStore` interface, there is a second implementation, `InMemoryAppSettingsStore`, that keeps settings in a variable and never touches the disk. It exists for two reasons: *design-time* view models (so the visual designer can show realistic data without reading your real profile) and *unit tests* (so a test can exercise a settings-backed screen without leaving files behind). This is the payoff of coding to an interface — the same view models run against the real JSON store in production and the throwaway memory store in tests, with no changes.


## Why It Matters / Design Takeaways

The persistence layer is small, but it embodies a philosophy that shows up all over APRS-Command: *be boring where boring is safe, and paranoid where a failure would hurt.* One plain-text file is boring on purpose — easy to read, back up, and reset. The loader is paranoid on purpose — four independent fallbacks so it can never be the reason the app fails to open.

- One file, one tree. All persisted state hangs off AppSettings and lands in a single readable settings.json. Adding a feature's settings means adding one section — the storage code never changes.
- The loader can never crash the app. Missing file, unreadable file, or corrupt file each resolve to safe defaults; a broken section is salvaged in isolation so it cannot take good sections down with it.
- Saves are atomic. Write to a temp file, then move it into place — a crash mid-write can never leave a truncated, unparseable settings.json.
- Enums are stored by name, not number. JsonStringEnumConverter keeps the file self-explanatory and immune to later reordering of enum options.
- Receive-first is baked into the defaults. A fresh profile is N0CALL with every transmit switch off — it listens but stays silent until the operator deliberately enables transmitting.
- The format can grow without losing data. A version stamp plus a migration hook means files from older builds are read and upgraded, and an older single-profile file is imported once so returning operators keep their identity.
- Behind an interface. The same view models run against the real JSON store in production and an in-memory store in tests and the designer — persistence is testable without ever touching disk.

If you are maintaining this app years from now, the one rule to preserve is the never-throw-on-load contract stated in `IAppSettingsStore`. Every defensive branch in the loader serves it, and every test in `AppSettingsStoreTests` guards it. When you add a new settings section, give it a `Default`, add it to `AppSettings` and to both the `Normalize` and `SalvageLoad` paths, and write the round-trip test — and the operator in the field will keep their configuration through corruption, upgrades, and power failures alike.


# 16. Composition and Startup: Dependency Injection

*How APRS-Command assembles every service in one place at launch, and hands each object exactly the collaborators it needs.*


## What This Is / What It Is For

When APRS-Command launches, dozens of separate pieces have to come to life and find each other: the thing that reads radio packets off the internet, the thing that draws stations on a map, the thing that decides whether it is safe to key up a transmitter, the message center, the GPS reader, the weather poller, and many more. Something has to build all of those, in the right order, and connect each one to the exact helpers it depends on. That job is called *composition*, and the single place where it happens is called the *composition root*.

In APRS-Command that composition root is one file: `DesktopRuntime.cs`. It is the workshop where the whole application is assembled before the first window ever appears. A second file, `App.axaml.cs`, then takes the assembled runtime and connects its live data streams to the on-screen UI. This chapter walks through both, in plain language first and real code second.

> **The one-sentence version** — DesktopRuntime.Create() builds every service and hands each object the collaborators it needs; App.axaml.cs plugs the resulting live data into the windows the operator sees.


### Dependency injection, in plain words

*Dependency injection* (DI) sounds technical but the idea is simple. A *dependency* is just something a class needs in order to do its job — a helper object it calls. *Injection* means the class is *handed* that helper from outside, instead of building the helper itself.

A real-world analogy: think of a coffee machine. A badly designed machine would grow its own coffee beans and drill its own water well every morning — it would be tangled up with where those things come from. A well-designed machine has a bean hopper and a water inlet: you pour beans and water *in*, and the machine just brews. It does not care where they came from. Dependency injection is the water inlet. A class declares 'I need an `IStationDatabase` and an `IAprsParser`,' and whoever builds it pours those in through the constructor.

The opposite — a class reaching out and building its own helpers with `new` scattered through the codebase — creates two problems. First, you can never swap a helper for a test double or a different implementation, because the choice is welded inside the class. Second, nobody can see the whole wiring diagram; it is smeared across a hundred files. Centralizing construction in one composition root fixes both: every wiring decision lives in one readable place, and every class stays ignorant of where its collaborators came from.

> **Why one wiring place beats scattered new** — With a single composition root you can read the app's entire dependency graph top to bottom, change one wire without hunting through feature code, and substitute fakes for tests. Classes that build their own dependencies can do none of this.


### The container: registering the recipe book

APRS-Command uses the standard .NET DI container, `Microsoft.Extensions.DependencyInjection`. A *container* is a registry that maps 'when someone asks for interface X, build it like this.' You fill it with recipes, then ask it for finished objects and it constructs them — and everything they transitively depend on — for you.

The build begins by creating an empty `ServiceCollection` (the recipe book) and adding recipes to it:

```csharp
public static DesktopRuntime Create()
{
    var services = new ServiceCollection();

    // --- Core + services (real implementations) ---
    services.AddSingleton<IAprsParser, AprsParser>();
    services.AddSingleton<IStationDatabase>(_ => new Persistence.SqliteStationDatabase());
    services.AddSingleton<StationDatabase>(_ => new StationDatabase());
    services.AddSingleton<IRawPacketLogService>(
        sp => new RawPacketLogService(sp.GetRequiredService<IAprsParser>()));
```

Line by meaningful line. `AddSingleton<IAprsParser, AprsParser>()` says: whenever any object asks for an `IAprsParser` (the interface — the *promise* of a packet parser), give it one shared `AprsParser` (the concrete implementation). *Singleton* means 'build exactly one and share it everywhere' — as opposed to building a fresh copy per request. Because a parser holds no per-caller state and is expensive to think about twice, one shared instance is correct.

The next lines show the two ways to register. `IStationDatabase` uses a *factory lambda* — `_ => new Persistence.SqliteStationDatabase()` — a little recipe that says exactly how to build the object. The underscore is the container itself, ignored here because this constructor needs no other services. `IRawPacketLogService` uses the same lambda form but this time reaches back into the container: `sp => new RawPacketLogService(sp.GetRequiredService<IAprsParser>())`. Here `sp` is the *service provider*, and `GetRequiredService<IAprsParser>()` means 'go fetch the parser I registered a moment ago and hand it in.' That is dependency injection happening by hand, inside a recipe.

> **Why explicit factories here** — A comment in the source explains it: `SqliteStationDatabase` and `RawPacketLogService` have constructor parameters the container cannot supply on its own, so they are built with explicit factory lambdas rather than letting the container guess a constructor it cannot fully satisfy.


### The heart of the app: the ingestion service

A little further down, the recipe that ties the receive side together shows the injection pattern at full strength:

```csharp
services.AddSingleton<AprsIngestionService>(sp => new AprsIngestionService(
    sp.GetRequiredService<IAprsParser>(),
    sp.GetRequiredService<IStationDatabase>(),
    sp.GetRequiredService<IRawPacketLogService>(),
    sp.GetRequiredService<StationDatabase>()));
```

The `AprsIngestionService` is the pipeline that takes a raw line of text off the air and turns it into a known station on the map. It needs four collaborators: a parser to decode the text, the real station database to remember stations, the raw-packet log to keep an audit trail, and a separate in-memory `StationDatabase` used only for isolated replay. Notice that `AprsIngestionService` does not build any of these — it is *handed* all four through its constructor. That is the whole philosophy in one statement: a class lists what it needs and stays ignorant of where those things come from.


### The transmit-safety authority: one guard shared everywhere

The single most important thing the composition root does correctly is share one *transmit-safety authority* across every path that can key up a radio. In plain terms: transmitting on the air is the one action in this app that can break the law or interfere with other operators, so there is a single guard object that every transmit path must ask 'am I allowed to send right now?' — and the composition root's job is to make sure they all ask the *same* guard.

Three registrations set this up:

```csharp
services.AddSingleton<IAprsPortManager, AprsPortManager>();
services.AddSingleton<ITransmitPolicyContext, SettingsTransmitPolicyContext>();
services.AddSingleton<ITransmitSafetyAuthority, TransmitSafetyAuthority>();
```

Because all three are singletons, there is exactly one `TransmitSafetyAuthority` in the whole process. It owns a master switch — the *global inhibit* — that exercise and training modes flip to hard-block every transmission at once. The interface makes its job explicit:

```csharp
public interface ITransmitSafetyAuthority
{
    bool IsInhibited { get; }
    string? InhibitReason { get; }
    void Inhibit(string reason);
    void Release();
    TransmitDecision Evaluate(TransmitRequest request);
}
```

`Evaluate` is the gate every transmit path calls. Its implementation checks four things in priority order: the global inhibit first (exercise mode blocks everything), then that a real callsign is set (never transmit as a placeholder like N0CALL), then that APRS-IS has a valid passcode, then the per-port rules. Putting all four checks in one method means no caller can accidentally transmit by a side path that forgot a check.


### How the guard reaches the lowest-level transmit code

There is a subtlety worth understanding. The safety authority lives in the `Aprs.Services` layer, but the code that actually pushes bytes onto a KISS radio port lives lower, in `Aprs.Transport` — and a lower layer must never depend upward on a higher one. The solution is a tiny second interface, `ITransmitInhibitGate`, that lives down in the transport layer:

```csharp
public interface ITransmitInhibitGate
{
    bool IsTransmitInhibited { get; }
    string? InhibitReason { get; }
}
```

The `TransmitSafetyAuthority` class implements *both* interfaces. To the high-level services it looks like a full `ITransmitSafetyAuthority`; to the low-level radio code it looks like a minimal `ITransmitInhibitGate` — the same single object wearing two faces. The composition root is what connects the low-level radio client to that shared object:

```csharp
var transmitAuthority = provider.GetRequiredService<ITransmitSafetyAuthority>();
var inhibitGate = (ITransmitInhibitGate)transmitAuthority;
rfTransmitClient.InhibitGate = inhibitGate;

var beaconService = BeaconService.CreateFromSettings(
    provider.GetRequiredService<IAppSettingsStore>().Load(),
    rfBeaconClient: rfTransmitClient,
    inhibitGate: inhibitGate);
```

Line by line: it fetches the one authority from the container, casts it to its narrow inhibit-gate face, and hands that gate to the RF transmit client. Then it builds the beacon service with the *same* gate. The object-transmit service, the message-ACK coordinator, and the weather-beacon scheduler all get handed the same shared client and authority a few lines later. Because it is one object shared by construction, flipping exercise mode once silences every path at the same instant.

```csharp
// Inside KissRfBeaconTransmitClient.SendBeaconAsync — the low-level check:
var gate = InhibitGate;
if (gate is not null && gate.IsTransmitInhibited)
    return Fail(gate.InhibitReason ?? "Transmit is globally inhibited (exercise mode).", rawPacket);
```

> **Why this matters** — Sharing one authority by construction is a safety guarantee, not a convenience. If each transmit path built its own guard, exercise mode might silence some paths and miss others. One object, injected everywhere, means the master switch is truly master.


### Breaking startup cycles: the deferred client

Assembly is rarely a clean straight line. Sometimes object A needs object B, but B cannot exist until A is partly built — a *circular dependency*, like needing your house keys to get into the garage where you keep your house keys. APRS-Command solves this in two ways, both visible in the composition root.

The first is the *deferred proxy*. The internet-gateway (iGate) service needs an APRS-IS client at the moment it is registered, but the real client only comes to life later, after the beacon service is built. So the root registers a stand-in:

```csharp
var deferredIGateClient = new DeferredAprsIsClient();
services.AddSingleton<IIGateService>(_ => new IGateService(
    deferredIGateClient, IGateConfiguration.Default, null));

// ...much later, once the real client exists:
deferredIGateClient.InnerClient = beaconService.AprsIsClient;
```

`DeferredAprsIsClient` is an empty vessel that implements the full client interface. Until its `InnerClient` is set, every call is a harmless no-op that reports 'disconnected.' Once the real client exists, one assignment slots it in and all calls pass through. The iGate never knows the difference — it was handed a promise, and the promise was quietly fulfilled.


### Breaking cycles: settable delegates

The second technique is the *settable delegate*. The RF transmit client needs to know which radio connections are open, but those connection coordinators are not built until later in `Create()`. Rather than force an ordering that cannot exist, the client exposes function-shaped hooks that default to 'nothing,' then get filled in once the coordinators are ready:

```csharp
public Func<IReadOnlyList<TcpKissClient>> GetTcpClients { get; set; }
    = static () => Array.Empty<TcpKissClient>();

// ...after the coordinators are built:
rfTransmitClient.GetTcpClients    = () => kissTcpCoordinator.GetTransmitClients();
rfTransmitClient.GetSerialClients = () => serialKissCoordinator.GetTransmitClients();
```

The delegate starts as a function that returns an empty list, so the client is safe to use even before wiring. After the coordinators exist, the root replaces those functions with ones that fetch the live clients. This keeps the DI graph acyclic while still letting late-built pieces feed early-built ones.

> **Two phases, one method** — Create() is deliberately split in half. The first half registers recipes into the container and calls BuildServiceProvider(). The second half pulls key objects back out and hand-wires the runtime-only connections — RF delegates, the deferred client, the coordinators — that a pure container cannot express.


### Assembling the main viewmodel

Once the container is built, the root constructs the *MainWindowViewModel* — the single object the main window binds to, holding a live viewmodel for every feature panel. Each is fed real services pulled from the container. A representative slice:

```csharp
var mainViewModel = new MainWindowViewModel(
    map,
    GpsStatusViewModel.FromGpsService(new Aprs.Services.GpsService(), DateTimeOffset.UtcNow),
    rawPacketLog,                                   // LIVE
    new DecodedEventLogViewModel(provider.GetRequiredService<IDecodedEventLogService>()),
    new EventMonitorViewModel(provider.GetRequiredService<IAprsEventBus>()),
    provider.GetRequiredService<MessageCenterViewModel>(),
    // ...roughly thirty feature viewmodels, each handed its live service...
    new SmartBeaconingViewModel(provider.GetRequiredService<IAppSettingsStore>()));
```

Every `LIVE` comment marks a panel wired to a real service rather than sample data. The pattern is uniform: build a viewmodel, hand it the service it needs from the container. A couple of viewmodels — like the GPS status and the simulation panel — are given a neutral initial state here and then connected to their live event source afterward, because their data arrives as a stream of events rather than a single fetch.


### From Create to Start

`Create()` finishes by packing the assembled objects into a `DesktopRuntime` instance via its private constructor and returning it. Construction and *running* are kept separate: nothing has connected to a network or started a timer yet. That happens in a distinct `Start()` method:

```csharp
public void Start()
{
    Coordinator.Start();
    BeaconService.Start();
    GpsCoordinator.Start();
    MessageAckCoordinator.Start();
    KissTcpCoordinator.Start();
    SerialKissCoordinator.Start();
    // ...
    Coordinator.ConnectAprsIsReceiveOnly(
        settings.Callsign, serverHost: serverHost,
        serverPort: serverPort, filter: filter);
}
```

Separating build from start means the object graph can be fully assembled and inspected before anything with side effects runs. It also gives a clean, deliberate moment where the app opens its receive-only connection to the APRS-IS network so live stations appear on launch — using the operator's own configured server and filter, not hardcoded defaults.


### The other half: App.axaml.cs wires the UI

The composition root builds services and viewmodels, but something still has to connect live event streams to the screen — turn an incoming packet into a toast notification, a GPS fix into a status readout, a weather packet into the weather panel. That is the job of `App.axaml.cs`, Avalonia's application entry class, and specifically its `OnFrameworkInitializationCompleted` method.

It first decides between two paths based on whether the operator has ever set up a station:

```csharp
if (StationProfile.Load().IsConfigured)
{
    runtime = DesktopRuntime.Create();
    var mainWindow = new MainWindow { DataContext = runtime.MainViewModel };
    // ...restore saved window size/position...
    desktop.MainWindow = mainWindow;
    runtime.Start();
    WireSoundAlerts(runtime);
    WireMessageToast(runtime);
    WireGpsWriteback(runtime);
    // ...roughly twenty Wire* calls...
}
else
{
    var setup = new SetupWindow();
    setup.SetupCompleted += () => { /* build runtime, then wire */ };
    desktop.MainWindow = setup;
}
```

On a configured install it builds the runtime, binds the main window to `runtime.MainViewModel`, calls `runtime.Start()`, then runs a long list of `Wire*` helper methods. On first-ever run it instead shows a setup window and defers all of that until the operator finishes. Each `Wire*` method is a small, single-purpose connector. For example:

```csharp
private static void WireWeather(DesktopRuntime rt)
{
    rt.Coordinator.PacketParsed += (_, e) =>
    {
        if (e.Packet is Aprs.Core.WeatherAprsPacket wp)
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                rt.MainViewModel.Weather.AcceptWeatherPacket(wp, e.Source));
    };
}
```

It subscribes to the coordinator's `PacketParsed` event, filters for weather packets, and pushes each one to the weather panel. The `Dispatcher.UIThread.Post` wrapper is essential: packets arrive on background threads, but UI objects may only be touched on the UI thread, so the update is marshaled onto it. Every `Wire*` method that touches the screen follows this same discipline.

> **A real division of labor** — Keeping event-to-UI wiring in App.axaml.cs rather than DesktopRuntime keeps the composition root free of Avalonia and threading concerns. The root knows about services; App.axaml.cs knows about the screen. Mixing them would make the service graph impossible to test without a UI.


### Startup, end to end

Pulling the whole sequence together, here is the path from the operating system launching the process to a running window:

| Step | Where | What happens |
| --- | --- | --- |
| 1 | Program.Main | Velopack update hook runs; global exception handlers are installed; the Avalonia app is built and started. |
| 2 | App.OnFrameworkInitializationCompleted | Persisted dark-mode theme is applied before any window opens. |
| 3 | App (branch) | If a station profile exists, build the runtime; otherwise show the setup window first. |
| 4 | DesktopRuntime.Create | Register all services into the container, build the provider, then hand-wire coordinators, the transmit authority, and the main viewmodel. |
| 5 | App | Bind MainWindow to runtime.MainViewModel; restore saved window size and position. |
| 6 | runtime.Start | Start every coordinator and open the receive-only APRS-IS connection. |
| 7 | App Wire* methods | Connect live event streams (packets, GPS, weather, alerts) to the on-screen viewmodels. |

```csharp
// Program.cs — the true entry point
public static void Main(string[] args)
{
    VelopackApp.Build().Run();
    AppDomain.CurrentDomain.UnhandledException += /* write a fatal-error log */;
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}
```


### Shutdown: tearing down in reverse

A composition root that builds things carefully must also tear them down carefully. `DesktopRuntime` implements `IAsyncDisposable`, and its `DisposeAsync` disposes objects in roughly the reverse order they were created — later, dependent pieces first, foundational pieces last:

```csharp
public async ValueTask DisposeAsync()
{
    await LocalRestApiService.StopAsync().ConfigureAwait(false);
    await AgwpeCoordinator.DisposeAsync().ConfigureAwait(false);
    // ...coordinators, watchdog, GPS, beacon...
    await Coordinator.DisposeAsync().ConfigureAwait(false);
    if (provider.GetService<IStationDatabase>() is IDisposable db)
        db.Dispose();
    await provider.DisposeAsync().ConfigureAwait(false);
}
```

Reverse order matters: you close the coordinators that *use* the database before you close the database itself, exactly as you would unplug appliances before shutting off the power at the panel. The final `provider.DisposeAsync()` disposes the container and, with it, every singleton it built. `App.axaml.cs` calls this from its `OnShutdownRequested` handler, so a clean exit always releases sockets, serial ports, and the SQLite connection.


## Why It Matters / Design Takeaways

The composition root is the one place in APRS-Command where you can see the entire application as a single wiring diagram. Everything else is a component that declares its needs and stays ignorant of the rest. That separation is what lets the app grow to dozens of features without collapsing into a tangle.

The design rests on a few deliberate choices worth carrying forward. Build in one place, so the dependency graph is readable and changeable. Inject dependencies rather than construct them, so classes stay testable and swappable. Share the one object that must be shared — the transmit-safety authority — by construction, so a safety guarantee cannot be defeated by a forgotten wire. Break startup cycles with deferred proxies and settable delegates rather than contorting the object graph. Keep building separate from running, and running separate from UI wiring, so each concern can be reasoned about and tested on its own. And tear down in reverse, so a clean shutdown is as deliberate as a clean startup.

> **For the next maintainer** — When you add a feature, add its service registration to DesktopRuntime.Create(), inject its dependencies through the constructor, and — if it needs live data on screen — add a small single-purpose Wire* method in App.axaml.cs. Follow the existing grain and the wiring diagram stays legible.


# 17. MVVM with Avalonia

*How the screens are wired: Views, ViewModels, bindings, and the map ViewModel that drives the main map page.*


## What This Is / What It Is For

Every window and panel you see in APRS-Command is built with a pattern called *MVVM* — short for Model-View-ViewModel. This chapter explains what that pattern is in everyday terms, why the project uses it, and how it plays out in the real code that drives the map page. If you have never written a line of desktop-UI code, you should finish this chapter able to read any screen in the app and know where its logic lives.

The short version: MVVM is a way of keeping the *look* of a screen (buttons, text, colors) completely separate from the *logic* behind it (what happens when you click, what the numbers mean, when things turn red). Keeping them apart means you can test the logic without opening a window, and you can restyle the window without touching the logic.


### The three parts, in plain words

*Model* — the raw facts. In this app the Models are things like a heard station's callsign, position, and age. They come from the lower layers (the packet decoder, the station database) and know nothing about screens. Think of the Model as the ingredients in the pantry: real, but not yet a meal.

*View* — the screen itself: the actual buttons, text blocks, and map control the operator looks at and clicks. In Avalonia a View is written in a markup file ending in `.axaml` (an XML-like layout language), such as **MapView.axaml**. The View is the plated dish — what gets served — but it does no cooking of its own.

*ViewModel* — the cook in between. It takes the raw Model facts, shapes them into exactly what the screen needs to show (a formatted label, a tooltip string, an on/off flag), and it holds the actions the screen can trigger (a click becomes a *command*). The ViewModel is plain C# with no mention of buttons or pixels, which is precisely what makes it testable. **MapViewModel.cs** is the ViewModel for the map page and the star of this chapter.

> **Why the ViewModel exists at all** — You could put logic directly behind the window (the "code-behind" file). The problem: code-behind can only run when a real window is open, so you cannot unit-test it on a build server with no screen. The ViewModel pulls that logic into a plain class a test can create, poke, and inspect in memory — no window required. That single benefit is the whole reason the pattern exists.


### What a binding is

A *binding* is a live wire between a spot on the screen and a property on the ViewModel. Instead of the View reaching into the ViewModel to read a value once, the binding subscribes: when the ViewModel's value changes, the screen updates itself; when the user types or clicks, the change flows back. You declare the wire in markup and never write the wiring code by hand.

Here is a real menu item from **MapView.axaml**. The `{Binding ...}` expressions are the wires:

```csharp
<MenuItem Header="Assign tactical callsign…"
          Command="{Binding AssignTacticalCommand}"
          IsEnabled="{Binding HasSelectedStation}" />
```

Two wires are declared here. `Command="{Binding AssignTacticalCommand}"` connects the menu click to the **AssignTacticalCommand** property on the ViewModel — clicking runs that command. `IsEnabled="{Binding HasSelectedStation}"` connects the item's greyed-out/clickable state to the **HasSelectedStation** property — when no station is selected, the menu item disables itself automatically. Nobody wrote "if selection changed, grey out the menu"; the binding does it.

The View also declares *which* ViewModel it is bound to, at the top of the file:

```csharp
<UserControl xmlns="https://github.com/avaloniaui"
             x:Class="Aprs.Desktop.Views.MapView"
             x:DataType="vm:MapViewModel">
```

`x:DataType="vm:MapViewModel"` tells Avalonia's compiler "every binding on this screen targets a **MapViewModel**." This turns bindings into *compiled* bindings: if you mistype a property name, the build fails instead of silently doing nothing at runtime. It is a spell-checker for your wires.


### How the app implements MVVM: hand-rolled, on purpose

For MVVM to work, two mechanical things must happen: (1) when a ViewModel property changes, it must announce the change so bindings can react; (2) a click must be turnable into a callable action. The standard machinery for these is an interface called `INotifyPropertyChanged` (the "announce a change" contract) and an interface called `ICommand` (the "a click I can run" contract).

> **This project does NOT use CommunityToolkit.Mvvm** — A popular library, CommunityToolkit.Mvvm, auto-writes this machinery for you with attributes like [ObservableProperty] and [RelayCommand] — a "source generator" fills in the boilerplate at compile time. APRS-Command deliberately does not reference that package (it is absent from Aprs.Desktop.csproj). Every ViewModel here writes the machinery by hand. Understanding the hand-rolled version is essential, because it is what the app actually ships — and it is exactly the boilerplate the toolkit would have generated, so you also learn what the toolkit does under the hood.

The "announce a change" contract shows up at the bottom of every ViewModel as one small helper. This is the real one from **MapViewModel.cs**:

```csharp
public event PropertyChangedEventHandler? PropertyChanged;

private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
{
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

`PropertyChanged` is the loudspeaker: bindings quietly subscribe to it. `OnPropertyChanged(...)` is the announcement. The clever part is `[CallerMemberName]` — a compiler feature that fills in the name of whatever property called it, so you never have to type the property's name as a string. Call it from inside the **ShowTrails** setter and it announces "ShowTrails changed" for free.

You can see the full pattern in a single property. This is **ShowTrails**, which controls whether station movement trails are drawn:

```csharp
private bool showTrails;
public bool ShowTrails
{
    get => showTrails;
    private set
    {
        if (showTrails != value)
        {
            showTrails = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TrailsButtonTooltip));
        }
    }
}

public string TrailsButtonTooltip => ShowTrails
    ? "Station trails ON — click to hide"
    : "Station trails OFF — click to show";
```

Read it line by line. `private bool showTrails;` is the actual stored value (the "backing field"). The `get` hands it out. The `set` first checks `if (showTrails != value)` — only proceed if it truly changed, so you do not fire pointless updates. It stores the new value, then announces two changes: its own (`OnPropertyChanged()` with the name filled in automatically) *and* **TrailsButtonTooltip**. That second announcement matters: the tooltip is a *computed* property with no stored value of its own — it derives its text from **ShowTrails**. Since the binding cannot know the tooltip depends on the flag, the code tells it explicitly. Miss that line and the button's on/off state would flip while its tooltip stayed stale.

> **This one-line-per-property tax is exactly what the toolkit removes** — With CommunityToolkit you would write `[ObservableProperty] private bool showTrails;` and a generator would produce the entire block above. The cost of hand-rolling is more typing and the risk of forgetting a change announcement (the classic MVVM bug: a value updates but the screen does not). The benefit is zero external dependency and total visibility — nothing is hidden inside a generator. This project chose visibility.


### Commands: turning a click into a callable action

The second contract, `ICommand`, is how a button or menu item becomes something the ViewModel can run. APRS-Command has its own tiny implementation, **DesktopCommand**, so it needs no library for this either. Here it is in full:

```csharp
public sealed class DesktopCommand : ICommand
{
    private readonly Action? execute;
    private readonly Func<bool>? canExecute;

    public DesktopCommand(Action execute, Func<bool>? canExecute = null)
    {
        this.execute = execute;
        this.canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) { /* … runs the action … */ }
}
```

A **DesktopCommand** wraps two things: `execute` (the code to run when clicked) and an optional `canExecute` (a yes/no test for whether the button should be enabled). Avalonia calls `CanExecute` to decide whether to grey the control out, and `Execute` when the user actually clicks. Its sibling **RelayCommand** is the same idea but passes the clicked item along, for commands that act on "the selected row."

The ViewModel builds its commands once, in its constructor. A few real lines from **MapViewModel**:

```csharp
ToggleTrailsCommand = new DesktopCommand(() => ShowTrails = !ShowTrails);

SetRingsHereCommand = new DesktopCommand(
    SetRingsAtSelectedStation,
    () => SelectedStation?.Latitude is not null);
```

**ToggleTrailsCommand** is the simplest possible case: run one line that flips **ShowTrails**. The XAML button bound to it needs no code-behind at all. **SetRingsHereCommand** shows the `canExecute` half in action: its second argument, `() => SelectedStation?.Latitude is not null`, means "only enable this when a station with a known position is selected." Bind a menu item's `Command` to it and the item enables and disables itself as the selection changes — the same self-managing behavior you saw with `IsEnabled` earlier, but driven by the command instead of a separate flag.


### MapViewModel: state, actions, and one-shot requests

**MapViewModel** is the largest ViewModel in the app because the map page does the most. It cleanly separates three kinds of things a screen needs, and recognizing them makes any other ViewModel easy to read.

| Kind | Example | How the View uses it |
| --- | --- | --- |
| State (property) | ShowRadar, DrawMode, SelectedStationDetails | Bound for display; the screen re-renders when it changes |
| Action (command) | ToggleRadarCommand, DrawLineCommand, HomeCommand | Bound to a button/menu; a click runs it |
| One-shot request (event) | NavigationRequested, FindStationRequested, DrawModeChanged | The code-behind subscribes and performs an imperative action on the map control |

The first two you have met. The third — *events* — solves a specific problem. Some actions cannot be expressed as "a value changed"; they are momentary requests like "jump the map to the home view *now*." You cannot bind a map's camera to a property cleanly, so the ViewModel raises an event and lets the View carry it out. Look at the **HomeCommand**:

```csharp
HomeCommand = new DesktopCommand(
    () => NavigationRequested?.Invoke(this, MapNavigationRequest.Home));
```

Clicking Home does not move the map itself — the ViewModel has no idea how to drive the Mapsui map control, and keeping it that way is the whole point. Instead it fires **NavigationRequested** with a "go home" request. The View is listening and does the actual camera work. The ViewModel stays pure C# that a test can exercise by subscribing to the event and asserting it fired.

> **Why events and not just more commands** — A command answers "what happens when clicked." An event answers "tell whoever is watching that something needs doing." The map camera, file-open dialogs, and the color picker all live in the View's world (they need real UI objects), so the ViewModel requests them by event rather than reaching across the layer boundary. This is how the app keeps the rule from CLAUDE.md — no UI code in the wrong layer — even for actions that are inherently visual.


### How the View attaches to the ViewModel

A View and its ViewModel are introduced by a single connection point called the *DataContext* — think of it as the ViewModel the whole screen is pointed at. Every `{Binding X}` on the page resolves `X` against the DataContext. The map page's code-behind, **MapView.axaml.cs**, hooks the moment that connection is (re)made:

```csharp
public MapView()
{
    InitializeComponent();
    Loaded += (_, _) => InitializeMap();
    DataContextChanged += (_, _) => AttachViewModel();
}
```

When the DataContext is set to a **MapViewModel**, `AttachViewModel()` runs. Because bindings handle all the *display* wiring automatically, this method only has to wire up the *event* side — the one-shot requests bindings cannot cover:

```csharp
currentViewModel = DataContext as MapViewModel;
if (currentViewModel is not null)
{
    currentViewModel.PropertyChanged += ViewModel_PropertyChanged;
    currentViewModel.Markers.CollectionChanged += Markers_CollectionChanged;
    currentViewModel.NavigationRequested += OnNavigationRequested;
    currentViewModel.DrawModeChanged += OnDrawModeChanged;
    // …and the rest of the one-shot requests
}
```

Each `+=` subscribes a code-behind handler to a ViewModel event. **NavigationRequested** drives the camera, **DrawModeChanged** switches the drawing tool, and so on. The matching block just above these lines detaches the *previous* ViewModel first (a mirror set of `-=` lines) so that when the DataContext is swapped, the old subscriptions are removed. Forgetting that half is a classic memory leak — the old ViewModel would be kept alive forever by the still-attached View — so the symmetry is deliberate.


### The live-data path: how new stations reach the screen

The most important collection on the ViewModel is **Markers**, the list of stations drawn on the map. Its type is an *ObservableCollection* — a list that announces its own additions and removals, the collection-level cousin of `INotifyPropertyChanged`. When something is added or removed, anyone bound to it (or subscribed to its `CollectionChanged` event) hears about it.

```csharp
public ObservableCollection<StationMarkerViewModel> Markers { get; }
```

New packets arrive off the network on background threads, but Avalonia — like every desktop UI toolkit — insists the screen be touched only from the single UI thread. The **LiveDataCoordinator** enforces this: it collects fresh data and, on the UI thread, calls one method on the ViewModel:

```csharp
map.UpdateStations(source.GetVisibleStations());
```

Inside **UpdateStations**, the ViewModel rebuilds **Markers** by wrapping each raw **StationMarker** Model in a **StationMarkerViewModel**:

```csharp
Markers.Clear();
foreach (var marker in markers)
{
    Markers.Add(new StationMarkerViewModel(marker));
}
```

**StationMarkerViewModel** is the smallest, purest example of the whole pattern: a read-only wrapper that takes a raw Model and exposes display-ready values. It turns a bare course and speed into `MovementLabel` ("Stationary" when both are missing, "045 deg / 12 kt" otherwise) and identifies the sending radio's make from its tocall. It holds no changing state and no commands — it exists purely so the screen never has to reason about raw packet fields.

Each `Markers.Add(...)` fires `CollectionChanged`, which the code-behind's **Markers_CollectionChanged** handler catches to rebuild the actual map features. That handler is careful to *coalesce* a burst of adds — a replay load can add 175 stations at once — into a single map rebuild per UI cycle, rather than redrawing the whole map 175 times. The ViewModel stays blissfully unaware of Mapsui; it just fills a list and announces the change.

> **The UI-thread rule is not optional** — ObservableCollection and INotifyPropertyChanged assume every change happens on the UI thread. That is why LiveDataCoordinator marshals network data onto the UI thread before calling UpdateStations. Touch Markers from a background thread and you get intermittent, hard-to-reproduce crashes. The single call site is the safeguard: all screen-bound state changes funnel through the one thread that is allowed to make them.


### Design-time preview: a ViewModel with no app running

Because a ViewModel is plain C#, you can hand it fake data and see the screen without launching the whole app. **MapViewModel.CreateDesignTime()** builds a ViewModel pre-loaded with three invented stations (Net Control, a mobile W1AW-9, a weather station) so the visual designer renders a realistic map page at author time. **MainWindowViewModel** does the same for the whole window. This is a direct dividend of the separation: the View has no idea whether its data is real or a rehearsal.


## Why It Matters / Design Takeaways

MVVM is the load-bearing wall of the entire UI layer, and APRS-Command applies it with a specific, deliberate stance: implement the machinery by hand rather than pull in a code-generation library. That choice trades a little repetitive typing for zero UI-framework-adjacent dependencies and complete visibility — every property change and every command is right there in the file, nothing hidden inside a generator.

The payoff is concrete. **MapViewModel** holds real logic — which tool is active, what the tooltip says, whether a menu item is enabled, how raw stations become display labels — and every bit of it can be tested in memory with no window open. The View (**MapView.axaml** plus its code-behind) is left with only two jobs: declare bindings, and carry out the visual one-shot requests the ViewModel raises as events. That is why the map control, which genuinely cannot be bound (a camera is not a property), is the one place imperative code-behind survives — and even there the ViewModel only *asks*, never *acts*.

If you take three things from this chapter: bindings are self-maintaining wires you declare once; every ViewModel property that others depend on must announce its change or the screen goes stale; and the layer boundary is kept honest by pushing anything genuinely visual out through events. Learn to spot the three kinds of member — property, command, event — and any ViewModel in the app becomes readable at a glance.


# 18. The Map: Mapsui, Layers & Drawing

*How APRS-Command renders a live world map, stacks its overlays, and lets operators draw on it — all grounded in one coordinate language, Web Mercator.*


## What This Is / What It Is For

The map is the beating heart of APRS-Command. Everything the program hears over the air — other stations, weather reports, objects someone dropped on the map, the trail a moving vehicle leaves behind — ends up as a colored dot, a line, or a symbol on a scrollable, zoomable map of the world. This chapter explains how that map is actually drawn, how it knows where north is, and how the operator can scribble their own lines, circles, and labels on top of it.

Two files do almost all of the heavy lifting. **MapView.axaml** (246 lines) is the visual shell — the map control plus the floating panels that overlay it. **MapView.axaml.cs** (2,111 lines) is the engine room: it builds the layers, converts coordinates, paints every marker, and runs the entire freehand drawing tool. A smaller helper project, **Aprs.Mapping**, holds the pieces that need no user interface — coordinate math, tile-URL building, and the offline-download planner.

> **Where to read along** — Main view: src/Aprs.Desktop/Views/MapView.axaml.cs and MapView.axaml. Supporting types: src/Aprs.Desktop/Mapping/DrawingShape.cs, ShapeMeasurements.cs, WmsRadarUrlBuilder.cs, and the Aprs.Mapping project (PlaceholderMapCoordinateConverter.cs, MapTileCalculationService.cs, TemplateMapTileProvider.cs).


### The cast of characters: which library does what

APRS-Command does not draw a map from scratch. Drawing a slippy map — one you can drag and zoom smoothly — is a genuinely hard problem, so the app stands on four well-worn open-source libraries. Understanding who does what makes the rest of the code readable.

| Library | Plain-English job | Real-world analogy |
| --- | --- | --- |
| *Mapsui* | The map engine. Holds the layers, the viewport (what you're looking at), handles pan/zoom, and asks each layer to draw itself. | The stage manager who decides what's on stage and where the audience is looking. |
| *BruTile* | Fetches and caches the little square map images ("tiles") over HTTP. | The runner who fetches numbered puzzle pieces and keeps a box of ones you've used before. |
| *NetTopologySuite (NTS)* | Pure geometry math: points, lines, polygons, circles, distances, and hit-testing. | The draftsman's compass and ruler — it knows shapes, not pixels. |
| *Skia* (via Mapsui.Rendering.Skia) | The actual pixel painter that turns styles into colored pixels on screen. | The brush that finally puts paint on the canvas. |

You will see all four named in the `using` block at the top of **MapView.axaml.cs**. The division of labor is deliberate: geometry (NTS) is kept separate from painting (Skia) which is kept separate from the map's bookkeeping (Mapsui). When the code builds a circle, it builds an abstract NTS ring of coordinates; only later does Mapsui hand it to Skia to become blue pixels.


### Web Mercator (EPSG:3857): the flat map every web map uses

*WHAT it is:* Web Mercator is a specific recipe for flattening the round Earth onto a square sheet of paper, and it is the same recipe used by Google Maps, OpenStreetMap, and essentially every web map you have ever scrolled. Its official codename is *EPSG:3857* — think of that as the map projection's model number.

*WHY it matters here:* A GPS position is given in *latitude and longitude* — degrees north and degrees east, like "35.6 N, 82.5 W." Those are angles on a globe, not positions on a flat screen. To draw a dot on a flat rectangle of pixels you must first decide how the globe was unrolled. Mapsui, the tile servers, and APRS-Command all agree to use Web Mercator, so a marker placed by one is in the exact spot expected by the others. In Web Mercator, positions are measured not in degrees but in *meters east and north of the point where the equator meets the prime meridian* (a spot in the Atlantic off West Africa).

*The catch (worth understanding):* Mercator lies about size. To keep angles and shapes correct, it stretches everything horizontally and vertically the farther you get from the equator — which is why Greenland looks as big as Africa on a wall map even though Africa is fourteen times larger. This stretch is not cosmetic; it means a "meter" in Web Mercator near the poles represents far less than a real ground meter. The app corrects for this every time it measures a drawn shape (covered below).

Here is the single most important line for converting a real coordinate into map space, from the `CreateCircle` helper that draws the operator's range rings:

```csharp
// From CreateCircle(...) — turn a real lat/lon into Web Mercator meters:
var (cx, cy) = Mapsui.Projections.SphericalMercator.FromLonLat(lonDeg, latDeg);

// ...then a circle is 64 points swept around that center, in meters:
for (int i = 0; i < segments; i++)
{
    var angle = 2 * Math.PI * i / segments;
    coords[i] = new Coordinate(
        cx + radiusMeters * Math.Cos(angle),   // east/west offset in meters
        cy + radiusMeters * Math.Sin(angle));  // north/south offset in meters
}
coords[segments] = coords[0];                    // close the ring back to the start
```

`SphericalMercator.FromLonLat` is Mapsui's built-in translator: hand it longitude and latitude, it returns the `(x, y)` pair in Web Mercator meters. Because the result is in meters, a circle of a given radius is trivial — just step a compass around the center point. The loop walks 64 steps around a full circle (`2 * Math.PI` radians), and the final line snaps the last point back onto the first so the ring is closed. The opposite translator, `SphericalMercator.ToLonLat`, appears wherever the app needs to go the other way — from a screen click back to a real coordinate.


### An oddity worth explaining: why positions travel as percentages

There is a quirk in this codebase that will confuse anyone reading it cold. Station positions do not travel through the app as latitude/longitude. Instead the view models expose two numbers — `MapLeftPercent` and `MapTopPercent` — that describe a position as a *percentage across the whole planet*: 0% left is the far west edge (−180 longitude), 100% is the far east; 0% top is the North Pole, 100% is the South. This is a holdover from an earlier, simpler map that was literally an image with markers positioned by CSS-style percentages.

Rather than rip that out, the map view treats percentages as a stable interchange format and converts at the boundary. The `PlaceholderMapCoordinateConverter` in Aprs.Mapping documents the exact formula, and `MakeFeature` in the map view reverses it and immediately projects to Web Mercator:

```csharp
private static PointFeature MakeFeature(double leftPercent, double topPercent)
{
    // Undo the whole-planet percentage encoding back to real degrees:
    var longitude = (leftPercent / 100.0 * 360.0) - 180.0;   // 0..100%  ->  -180..+180
    var latitude  = 90.0 - (topPercent  / 100.0 * 180.0);    // 0..100%  ->   +90..-90

    // Then project degrees into Web Mercator meters for Mapsui:
    var (mercatorX, mercatorY) = SphericalMercator.FromLonLat(longitude, latitude);
    return new PointFeature(new MPoint(mercatorX, mercatorY));
}
```

An `MPoint` is simply Mapsui's word for "a point on the map" (map-point). A `PointFeature` is that point plus the ability to carry styles and data. Notice the two-step dance: percentages → degrees → meters. It looks redundant, but it means the messy legacy format lives in exactly one place, and everything downstream speaks clean Web Mercator. The reverse trip appears in `OnMapInfo`, where a click's world position is turned back into percentages before being handed to the view model — the two ends of the pipe stay symmetric.

> **Design lesson** — When a legacy data format cannot be removed cheaply, quarantine it at a single conversion boundary rather than letting it leak through the whole system. Here, percentages exist only at the view-model edge; the entire rendering core is pure Web Mercator meters.


### The layer stack: order is everything

*WHAT a layer is:* A map is built from stacked transparent sheets, exactly like the acetate overlays an old-school cartographer would lay on a lightbox. The bottom sheet is the map itself; on top go trails, then rings, then drawings, then weather radar, then the station markers. Whatever is added last sits on top and can hide what's below it. Getting this order right is why markers are never buried under a rain cloud.

`InitializeMap` builds the stack once, in a fixed order, right after the control loads:

```csharp
var map = MapControl.Map;
currentBaseLayer = CreateBaseLayer(BaseMapKind.OpenStreetMap);
map.Layers.Add(currentBaseLayer);                 // 1. base map (bottom)

trailLayer = new WritableLayer { Name = "Station trails" };
map.Layers.Add(trailLayer);                       // 2. movement trails

ringsLayer = new WritableLayer { Name = "Range rings" };
map.Layers.Add(ringsLayer);                       // 3. distance rings

drawingLayer = new WritableLayer { Name = "Draw tools", Style = null };
map.Layers.Add(drawingLayer);                     // 4. operator drawings

// ... radar layer built here ...
map.Layers.Add(radarLayer);                       // 5. weather radar

markerLayer = new GenericCollectionLayer<List<IFeature>> { Name = "APRS markers" };
map.Layers.Add(markerLayer);                      // 6. station icons (top)
```

| Layer | Type | Holds | Why it sits where it does |
| --- | --- | --- | --- |
| Base map | TileLayer | The street/topo/aerial map | Bottom — everything is drawn over it |
| Station trails | WritableLayer | Faint indigo breadcrumb lines | Above the map, under everything meaningful |
| Range rings | WritableLayer | Distance circles around your station | Above trails, still a background reference |
| Draw tools | WritableLayer | Operator's lines, polygons, text | Above rings so annotations read clearly |
| Radar | TileLayer | NEXRAD weather imagery | Semi-transparent, above drawings |
| APRS markers | GenericCollectionLayer | Every station/object/weather icon | Top — the icons must always be clickable |

Two layer types appear. A *WritableLayer* is a scratchpad you clear and refill by hand — perfect for things the code redraws constantly (trails, rings, drawings). A *TileLayer* is fed by a tile source and manages its own fetching and caching — used for the base map and radar. The `Style = null` on the drawing layer is a small but important detail: it tells Mapsui "this layer has no default look; every shape carries its own style," which is exactly how the drawing engine works.


### Base maps and the quiet art of tile caching

*WHAT a tile is:* A slippy map is not one giant image; it is a mosaic of 256×256-pixel squares called *tiles*, arranged in a grid at each zoom level. Zoom in and the server hands over a finer grid of more tiles. Pan and it fetches the new squares scrolling into view. BruTile is the errand-runner that fetches them by URL.

The operator can choose among four base maps — OpenStreetMap (global streets) and three USGS layers (topographic, aerial imagery, and imagery-plus-topo, all US-only). `CreateBaseLayer` builds any of them from a table of URL templates and, crucially, gives each its own on-disk cache folder:

```csharp
var cacheDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "AprsCommand", "tile-cache", cacheFolder);      // e.g. .../tile-cache/osm
Directory.CreateDirectory(cacheDirectory);

var tileSource = new HttpTileSource(
    new GlobalSphericalMercator(0, maxZoom),        // the Web Mercator tile grid
    urlTemplate,                                    // "https://tile.openstreetmap.org/{z}/{x}/{y}.png"
    name: name,
    persistentCache: new FileCache(cacheDirectory, "png"),   // remember tiles on disk
    attribution: new Attribution(attributionText, attributionUrl),
    configureHttpRequestMessage: request => request.Headers.UserAgent.ParseAdd(
        "AprsCommand/1.0 (+https://github.com/KE4CON/CrossPlatformAPRS)"));
```

The `{z}/{x}/{y}` placeholders in the URL are the tile's zoom level and its column and row in the grid — BruTile fills them in for each square it needs. The `FileCache` is the payoff: every tile fetched while online is written to disk and reused later, even with no internet. That is what makes a map area you have already viewed work in the field, offline, with no extra step. Because each base map caches to its own folder, switching maps never mixes topo tiles with aerial ones, and switching back is instant.

> **Being a good tile citizen** — The code caches only tiles the operator actually looks at and never bulk pre-fetches, which respects each provider's usage policy. It also sends a descriptive User-Agent identifying the app — OpenStreetMap's tile policy requires this, and anonymous scrapers get blocked. These are not optional niceties; they are the terms of using free tiles.

For deliberate offline saving, a separate planner in Aprs.Mapping — `MapTileCalculationService` — does the arithmetic of "how many tiles cover this rectangle at these zoom levels." Its `ToTile` method is the canonical Web Mercator formula that turns a corner's latitude/longitude into a tile column and row, clamping latitude to ±85.05° because Mercator stretches to infinity at the true poles and simply cannot represent them. If the count tops 10,000 tiles it raises a warning so a giant download requires explicit confirmation.


### Station markers: painting APRS symbols from a sprite sheet

Every station broadcasts a two-character *APRS symbol* code — a canoe, a car, a repeater tower, a house. APRS-Command draws the real symbols, not generic dots, by cropping them out of three bundled *sprite sheets*: single PNG images that pack hundreds of 64-pixel icons into a 16-column grid. `RefreshFeatures` rebuilds the whole marker layer, and `AddStationStyles` decides how each station is painted. The core trick is turning a symbol character into a rectangle to crop:

```csharp
private static bool TryRegion(char code, out BitmapRegion region)
{
    region = null!;
    if (code < '!' || code > '~') return false;    // only printable symbol codes

    var index = code - '!';                          // 0-based position in the sheet
    region = new BitmapRegion(
        (index % 16) * CellSize,                     // column -> x pixel  (16 per row)
        (index / 16) * CellSize,                     // row    -> y pixel
        CellSize, CellSize);                         // 64 x 64 crop
    return true;
}
```

The math is pure grid bookkeeping: subtract the first printable character to get a zero-based index, then `% 16` gives the column and `/ 16` gives the row, each multiplied by the 64-pixel `CellSize` to get the pixel corner of that icon. That `BitmapRegion` is handed to an `ImageStyle`, which tells Skia "draw only this 64-pixel square of that big image at this map point." A station's symbol table character (`/` or `\`) selects which sheet — primary or secondary — and a third overlay sheet stamps an extra glyph on top for overlay symbols.

If a station has no usable symbol, `AddStationStyles` falls back to a colored dot from `DotStyle`, tinted by category (blue for home, green for vehicles, purple for digipeaters) via `StationColor`. Selection is shown by fattening the outline and scaling the icon up. Age is shown through transparency: `StationOpacity` fades a station from fully opaque when Active, to half when Stale, to a quarter when Expired — so a glance at the map tells you who is live and who was heard an hour ago.

> **A subtle loading gotcha** — Mapsui only pulls image sources into its cache during a viewport-change fetch, so marker updates alone would leave every station as a placeholder circle on first paint. PreloadSymbolSheetsAsync deliberately constructs each sheet as an Image and calls FetchAllImageDataAsync up front to force the symbols into the render cache before the first draw. This is the kind of framework-specific knowledge that is invisible until it bites.


### The drawing engine: freehand annotation on a moving map

The most involved part of the map is the *drawing engine* — the tools that let an operator sketch lines, polygons, circles, and text labels directly onto the map, for briefing routes, marking search sectors, or circling an area of interest. A drawn shape is stored as a plain data object, `DrawingShape`, whose points live in Web Mercator meters just like everything else:

```csharp
public sealed class DrawingShape
{
    public DrawShapeType ShapeType { get; init; }        // Line, Polygon, Circle, Text
    public string Color { get; set; } = "#FF0000";
    public double StrokeWidth { get; set; } = 3.0;
    public DrawFillStyle FillStyle { get; set; } = DrawFillStyle.Solid;

    // Points in world coordinates (Mapsui units = EPSG:3857 meters)
    public List<(double X, double Y)> Points { get; } = [];
    public (double X, double Y) Centre { get; set; }     // circles
    public double RadiusMetres { get; set; }             // circles
    // ...text-specific fields: Label, GroundHeightMetres, BackgroundColorHex
}
```

Storing drawings in meters (not screen pixels) is the whole reason a drawn line stays glued to the ground when you pan and zoom — the shape is anchored to the world, and only its on-screen appearance is recomputed each frame. `IsCompletable` on the same class encodes what makes a shape "real": a line needs 2+ points, a polygon 3+, a circle a radius above a minimum. That single rule prevents a stray click from leaving an empty ghost behind.

The tricky part is capturing mouse input *without* the map panning underneath the cursor. The solution is Avalonia's *tunnel* event routing, which lets the view intercept pointer events before Mapsui's own pan/zoom handling sees them:

```csharp
MapControl.AddHandler(InputElement.PointerPressedEvent, OnDrawPointerPressed,
    RoutingStrategies.Tunnel);   // fire on the way DOWN to the map, before Mapsui
// ...same for PointerMoved and PointerReleased

private void OnDrawPointerPressed(object? sender, PointerPressedEventArgs e)
{
    if (currentDrawMode == DrawMode.None || drawingLayer is null) return;
    e.Handled = true;   // <-- mark handled: Mapsui never pans while a tool is active
    var world = ScreenToWorld(e.GetPosition(MapControl));
    // ...dispatch on currentDrawMode: add a vertex, start a circle drag, etc.
}
```

*Tunnel routing* means the event travels top-down from the parent to the child — so the view gets first refusal. Setting `e.Handled = true` stops the event there, so when a drawing tool is active the map itself never moves. When no tool is active (`DrawMode.None`) the handler bows out immediately and Mapsui pans normally. `ScreenToWorld` bridges the two worlds, asking the current viewport to convert a pixel position into Web Mercator meters.

Different tools use different gestures, all coordinated through a small set of state flags. A line or polygon adds a vertex per click and finishes on a double-click, showing a live "rubber-band" segment to the cursor (`drawHoverWorld`) so you preview the next segment before committing. A circle is press-drag: the press sets the center, the drag sets the radius live. Text is placed by dragging to set its size, then a dialog asks for the words. Every gesture ends by calling `RedrawAllShapes`, which clears the drawing layer and rebuilds every shape's Skia feature from scratch — simple, and fast enough for the handful of shapes an operator draws.

> **A real Mapsui trap, documented in the code** — In BuildShapeFeature a comment warns: line-strings draw their color from VectorStyle.Line, but polygons and circles draw their border from VectorStyle.Outline. Set Line on a polygon and the border silently falls back to a thin gray default. This asymmetry is invisible until a polygon renders with the wrong outline — exactly the kind of hard-won knowledge worth preserving in a comment.


### Honest measurements: undoing Mercator's lie with a cosine

When the operator turns on measurements, each shape gets a label with its true ground length, area, or diameter. This is where Mercator's distortion must be paid back. A raw distance between two Web Mercator points is stretched by roughly one over the cosine of the latitude; area, being two-dimensional, is stretched by the square of that. `ShapeMeasurements` corrects for exactly this:

```csharp
public static double GroundLengthMetres(IReadOnlyList<(double X, double Y)> points)
{
    double total = 0;
    for (int i = 0; i < points.Count - 1; i++)
    {
        var a = points[i]; var b = points[i + 1];
        var merc = Math.Sqrt((a.X-b.X)*(a.X-b.X) + (a.Y-b.Y)*(a.Y-b.Y));  // stretched
        var (_, lat) = SphericalMercator.ToLonLat((a.X+b.X)/2, (a.Y+b.Y)/2); // segment midpoint
        total += merc * Math.Cos(lat * Math.PI / 180.0);                    // corrected
    }
    return total;
}
```

For each segment it measures the raw Mercator distance, finds the latitude at the segment's midpoint, and multiplies by the cosine of that latitude to recover the true ground meters. The area routine does the same with `cos²`, and the circle radius routine corrects at the circle's center. This is why a circle drawn in Nashville and one drawn in Anchorage report honest, comparable radii even though their Mercator meters differ. The results then flow through `FormatLength` and `FormatArea`, which pick sensible units — feet vs. miles, acres vs. square miles, meters vs. kilometers — based on the operator's imperial/metric preference and the shape's size.

> **Why a midpoint and not something fancier** — The correction uses one representative latitude (segment midpoint, polygon centroid) rather than integrating across the shape. The comment is candid that this is accurate for the small shapes an operator draws on a map — a pragmatic simplification that is correct in practice and cheap to compute, rather than a textbook geodesic that would be overkill here.


### Radar and coverage: the same layer idea, different sources

Two more overlays reuse the layer machinery. Weather radar comes from NOAA's public NEXRAD service, fetched through `WmsRadarUrlBuilder`, which formats requests to a *WMS* (Web Map Service) endpoint — a standard where you ask for a map image of a specific bounding box rather than pre-cut tiles. The builder plugs into BruTile's `HttpTileSource`, and its own comment records a nonobvious constraint: Mapsui's `TileLayer` only accepts an `HttpTileSource` or an internal local source, so a hand-rolled tile source would be silently ignored. Radar also supports an animation mode — one `TileLayer` per time frame, cycled by a timer to show weather moving. `ApplyCoverageOverlays` reuses NTS circle geometry to draw predicted station coverage rings, proving the layer-plus-geometry pattern generalizes cleanly to new features.


## Why It Matters / Design Takeaways

The map looks like one thing but is really a careful stack of separated concerns: libraries for the hard parts, a single coordinate language spoken everywhere inside, and a thin conversion skin at the edges where legacy formats and user input arrive.

- *One coordinate language wins.* Everything inside the map speaks Web Mercator meters — markers, trails, rings, drawings, radar. The messy percentage format is converted away at exactly one boundary, so the core never has to think about it. When you extend the map, work in meters.
- *Layer order is a feature, not an accident.* The fixed bottom-to-top stack (base → trails → rings → drawings → radar → markers) is what keeps station icons clickable and rain from hiding your annotations. Add new overlays with their z-order in mind.
- *Cache like a good citizen.* Per-map on-disk tile caches give free offline maps for viewed areas, while never bulk-fetching and always sending a real User-Agent — respecting the providers whose free tiles make the app possible.
- *Store drawings in world space, render in screen space.* Because DrawingShape keeps points in meters, annotations stay pinned to the ground through any pan or zoom; only their appearance is recomputed each frame.
- *Mercator's distortion is real and must be repaid.* Every ground measurement multiplies by cos(latitude) (or its square for area) so lengths and areas are honest no matter where on Earth the shape sits.
- *Tunnel routing plus e.Handled is how you steal input politely.* Intercepting pointer events before Mapsui, and only when a tool is active, lets drawing and map-navigation coexist without fighting.
- *Comments preserve hard-won framework knowledge.* The notes about Line vs. Outline, symbol-sheet preloading, and HttpTileSource requirements are load-bearing documentation — the traps that would otherwise be rediscovered painfully.


# 19. Extension Surfaces

*The four developer-facing doorways into APRS-Command — REST API, WebSocket streams, file hooks, and plugins — and the single rule that none of them can bypass transmit safety.*


## What This Is / What It Is For

APRS-Command spends most of its life talking to radios, weather stations, and the APRS-IS network. This chapter is about the *other* doorways: the deliberately built ways for **outside programs** to look inside APRS-Command and, in tightly controlled cases, hand it data. Those doorways are called *extension surfaces* — an "extension surface" is simply any point where code that Jim did not write (a dashboard, a script, a plugin, a weather bridge) is allowed to interact with the app.

There are four such surfaces, and they were built as a numbered sequence of phases so each one could rest on the last. In plain terms they are: a **local REST API** (a web-style request/response endpoint), **WebSocket event streams** (a live firehose of "something just happened" notifications), **file import/export hooks** (drop a file in a folder, or read one the app wrote), and a **plugin/driver framework** (a slot future add-ons plug into). The weather input drivers already in the app are the first real, shipping example of that last surface.

> **The one rule that governs all four** — No extension surface can make the radio transmit. A REST request, a WebSocket message, an imported file, and a plugin event are all treated as untrusted data that must pass through APRS-Command's central transmit safety before anything ever goes on the air — and today, all four are wired to refuse transmit outright. Everything else in this chapter is detail hanging off that rule.


### Where this lives in the code

The extension surfaces are physically separated from the rest of the app into their own project, **AprsCommand.Api**, and they speak a shared vocabulary defined in a second project, **AprsCommand.Contracts**. Keeping them in separate projects is not decoration: in .NET, a project can only use another project it explicitly references, so this split is *compiler-enforced*. The internal station database and messaging code cannot accidentally reach into the API layer, and the API layer can only touch the internal app through narrow, agreed-upon interfaces.

| Surface | Main source file | Phase | On by default? |
| --- | --- | --- | --- |
| Local REST API | AprsCommand.Api/LocalRestApiService.cs | 14.8 | No |
| WebSocket event streams | AprsCommand.Api/WebSocketEventStreamService.cs | 14.9 | No |
| File import/export hooks | AprsCommand.Api/FileHookService.cs | 14.10 | No |
| Plugin/driver framework | docs/architecture/PLUGIN_DRIVER_FRAMEWORK.md (foundation); Aprs.Services weather drivers are the live example | 14.11 | No |

Notice the last column: every single one is *off by default*. That is the safe-defaults model, and it is worth understanding before any of the mechanics, because it is the reason a fresh install of APRS-Command opens no network ports and watches no folders until the operator deliberately turns something on.


### The shared foundation: contracts, source tags, and the event bus

Before looking at any one surface, three shared pieces make the whole thing hang together. The architecture document, `docs/architecture/EXTENSION_ARCHITECTURE.md`, lays them out as layers, and the code follows it faithfully.

First, *DTOs* (short for "data transfer objects" — plain data containers with no behavior, meant only to be serialized to and from JSON). The app's real internal models change as the app grows; the DTOs in **AprsCommand.Contracts** are the *stable public shape* the outside world sees. A station going out over the API is a `StationUpdateDto`, not the mutable internal station object. This is the classic "don't hand strangers the keys to your house — hand them a photocopy of the floor plan" move. Every DTO carries a `schemaVersion` so a future format change can be detected instead of silently misread.

Second, *source tagging*. Every piece of data that enters or leaves carries an `ExternalSourceMetadata` record that answers: where did this come from, and how much do we trust it? The trust and origin values are fixed enums so they cannot be fudged. The `ExternalSourceType` enum alone distinguishes sixteen origins — `AprsIs`, `Rf`, `Replay`, `Simulation`, `WeatherDriver`, `FileImport`, `LocalApi`, `Plugin`, and more — so the app never confuses a real over-the-air packet with something a script pushed in through a folder.

```csharp
public enum ExternalSourceType
{
    Unknown, AprsIs, Rf, TcpKiss, SerialKiss, Direwolf, Agwpe,
    Replay, Simulation, Training, WeatherDriver, Gps,
    ManualEntry, FileImport, LocalApi, Plugin
}
```

From `AprsCommand.Contracts/ExternalSourceType.cs`. Because a REST submission gets stamped `LocalApi` and a file import gets stamped `FileImport`, the rest of the app can always tell externally-injected data apart from data it heard itself — which matters enormously when deciding what is safe to act on.

Third, the *internal event bus*. This is a notification system — think of it as a building-wide PA that announces "a station just updated," "a weather reading arrived," "an API request was rejected." The crucial design decision, stated flatly in the architecture doc, is that it is **notification-only**: "It is not a command bus and not a transmit path." Extensions can *listen* to it (that is exactly how WebSocket streams work) but nothing they hear or say through it can *command* the app to do something dangerous.


### Surface 1: the local REST API

*REST API* means a set of web-style addresses (like `/api/stations`) that a program can send an HTTP request to and get JSON back — the same style of interface most web services use. In APRS-Command it is meant for local dashboards, scripts, and future tools that want to *read* what the app knows, and in carefully fenced cases hand it local data. The service lives in `LocalRestApiService.cs`.

The heart of its safety story is the configuration record. Read the defaults slowly, because each one is a locked door:

```csharp
public sealed record LocalRestApiConfiguration
{
    public bool ApiEnabled { get; init; }                 // false
    public string BindAddress { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 8765;
    public bool LocalhostOnly { get; init; } = true;
    public bool RequireToken { get; init; } = true;
    public string? ApiTokenReference { get; init; }       // null
    public bool ReadOnlyMode { get; init; } = true;
    public bool AllowExternalDataSubmit { get; init; }    // false
    public bool AllowTransmitRequest { get; init; }       // false
    public int MaximumRequestsPerMinute { get; init; } = 60;
}
```

`ApiEnabled` is false, so `StartAsync` refuses to run and records "Local REST API is disabled." `BindAddress` is `127.0.0.1` ("localhost" — the machine talking only to itself, never the wider network) and `LocalhostOnly` enforces it. `RequireToken` is true, meaning every request must carry a secret key. `ReadOnlyMode` is true, so even if enabled, it will only read. `AllowExternalDataSubmit` and `AllowTransmitRequest` are both false. An operator has to consciously flip several of these before the API can do anything beyond serve read-only data to the same computer.

When a request does arrive, `HandleAsync` runs it through a gauntlet in a fixed order: is the API even running, then `Authorize`, then route by method and path. The `Authorize` step is where a subtle but important hardening decision lives — it *fails closed*:

```csharp
if (configuration.RequireToken)
{
    // Fail closed: if a token is required but the server has none configured,
    // there is nothing to authenticate against, so every request must be rejected.
    if (string.IsNullOrWhiteSpace(configuration.ApiTokenReference))
        return LocalRestApiResponse.ErrorResponse(
            401, "API access requires a token, but no token is configured on the server.");

    if (string.IsNullOrWhiteSpace(request.Token))
        return LocalRestApiResponse.ErrorResponse(401, "API token is required.");

    if (!FixedTimeTokenEquals(request.Token, configuration.ApiTokenReference))
        return LocalRestApiResponse.ErrorResponse(401, "API token is invalid.");
}
```

"Fail closed" means: when in doubt, deny. The comment records a real bug that was fixed — previously, if a token was required but none had been configured on the server, the check was skipped and *any* non-empty token got in. Now a missing server token rejects everyone. The final comparison uses `FixedTimeTokenEquals`, which wraps `CryptographicOperations.FixedTimeEquals` — a *constant-time comparison* that always takes the same amount of time whether the token is wrong at the first character or the last. Ordinary string comparison bails out early on the first mismatch, and an attacker can measure those tiny timing differences to guess a secret one character at a time. Constant-time comparison closes that leak.

The read endpoints (`GET /api/stations`, `/api/weather`, `/api/alerts`, and so on) simply return DTOs from a data provider. The write endpoints are where the layered permission model shows up. `CheckExternalSubmit` refuses unless external submit is enabled, read-only mode is off, and the caller holds the right permission — `CreateLocalObjects` for objects, `SubmitLocalData` for everything else. Any accepted submission is then re-stamped by `EnsureLocalApiSource` so it can never masquerade as trusted internal data.


### The transmit-queue placeholder: a door with no handle

There is one endpoint that exists purely to demonstrate the safety model: `POST /api/transmit/queue`. It is the one place an outside program might *ask* APRS-Command to put a packet on the air. Follow what `CheckTransmitRequest` actually does with such a request:

```csharp
private LocalRestApiResponse CheckTransmitRequest(LocalRestApiRequest request)
{
    if (!configuration.AllowTransmitRequest)
        return LocalRestApiResponse.ErrorResponse(403, "Transmit queue endpoint is disabled.");

    if (!HasPermission(request, ExtensionPermission.QueuePackets))
        return LocalRestApiResponse.ErrorResponse(403, "Missing required permission: QueuePackets.");

    if (!HasAnyPermission(request, ExtensionPermission.TransmitAprsIs,
            ExtensionPermission.TransmitRf, ExtensionPermission.Admin))
        return LocalRestApiResponse.ErrorResponse(403, "Missing explicit transmit permission.");

    return LocalRestApiResponse.ErrorResponse(
        501, "Transmit queue endpoint is not implemented and remains blocked by policy.");
}
```

Read the ending carefully. Even a caller who passes *every* check — the feature enabled, the `QueuePackets` permission, and an explicit transmit permission — still gets a `501` refusal. The endpoint is built as a permanently-locked door precisely so the safety wiring exists, is tested, and is visible, long before any real transmit path is ever contemplated. And notice the order: the request is rejected at the *policy* layer before it could ever reach the actual radio, so the central transmit safety is never even asked. A permission is a *ticket to be considered*, never an authorization to transmit.

> **Why permissions are a list, not a single level** — The seven permissions — ReadOnly, SubmitLocalData, CreateLocalObjects, QueuePackets, TransmitAprsIs, TransmitRf, Admin — are separate capabilities a caller either holds or doesn't, defaulting to just ReadOnly. This lets a weather bridge get SubmitLocalData without ever touching transmit, and keeps the high-risk transmit permissions as distinct, deliberately-granted items rather than a side effect of ‘more access.’


### Surface 2: WebSocket event streams

A *WebSocket* is a network connection that, once opened, stays open so the server can push messages to the client the instant they happen — unlike the REST API, where the client must keep asking. This surface (`WebSocketEventStreamService.cs`) is for live dashboards and wall displays that want to *watch* APRS-Command in real time. It carries the same conservative defaults — disabled, localhost-only, token required, read-only — plus a `MaximumConnectedClients` cap so one runaway dashboard cannot exhaust the app.

The mechanism is elegant precisely because it reuses the notification-only event bus. When the stream service starts, it subscribes to *every* internal event and, for each one, broadcasts it to connected clients:

```csharp
subscription ??= eventBus?.SubscribeAll(async (evt, token) =>
{
    await BroadcastAsync(evt, token).ConfigureAwait(false);
    return AprsEventHandlerResult.Handled;
});
```

Because it can only *subscribe* — the event bus offers no way to inject a command — the WebSocket surface is structurally incapable of doing anything but observe. That is the design paying off: the safety property comes from the shape of the plumbing, not from a check that could be forgotten.

Every outbound message is wrapped in a stable envelope built by `ToEnvelope`: a `schemaVersion`, a timestamp, the stream name, the event type and category, the source metadata, and a typed payload. The `MapPayload` method translates each internal event into the matching public DTO — a `StationUpdated` event becomes a `StationUpdateDto`, a weather event a `WeatherObservationDto`, and anything without a richer mapping falls back to a generic `DecodedEventDto` so no event is ever un-representable.

Clients can narrow what they receive. Dedicated endpoints like `/ws/stations` and `/ws/weather` apply a default category filter, and a client can send a `subscribe` or `filter` message to refine it further — by category, event type, callsign, minimum severity, or whether to include raw packets. Critically, inbound messages from clients are limited to a tiny safe vocabulary. Anything else is rejected:

```csharp
switch (command)
{
    case "ping":        return WebSocketInboundMessageResult.Accepted("pong");
    case "subscribe":
    case "filter":      client.Filter = message.Filter ?? WebSocketEventStreamClientFilter.Default;
                        return WebSocketInboundMessageResult.Accepted("subscribed");
    case "unsubscribe":
    case "close":       /* disconnect */ return WebSocketInboundMessageResult.Accepted("closed");
    default:            return WebSocketInboundMessageResult.Rejected("Unknown WebSocket command...");
}
```

The only things a client may say are `ping`, `subscribe`, `filter`, `unsubscribe`, and `close` — all of which control the client's *own view* and nothing else. There is deliberately no command that changes app state. And if a client's connection breaks mid-broadcast, `BroadcastAsync` quietly drops it (via `SafeDisconnectAsync`, which swallows the failure) so one dead dashboard can never crash the stream for everyone else.


### Surface 3: file import/export hooks

Not every integration wants to open a network socket. The file-hook surface (`FileHookService.cs`) lets external tools cooperate through plain files: APRS-Command *writes* export files describing its state, and *reads* import files that a script has dropped into a watched folder. The safe defaults follow the same pattern — hooks, import, and export are all off, invalid imports are rejected, and there are conservative file-size limits.

Exports are straightforward and offered in formats each consumer would actually want: `stations.json` and `weather.json` as DTO arrays wrapped in an envelope, `objects.geojson` as GeoJSON (the standard mapping format, so the objects drop straight onto a map tool), `messages.csv` for spreadsheets, and `raw-packets.log` as timestamped text. Each export checks its size against `MaximumExportFileSizeBytes` before writing, so a runaway dataset cannot fill the disk.

Imports are where the caution concentrates. Every imported record runs through `RequireSchema` (the `schemaVersion` must be present *and* match the current version exactly), a type-specific validator, and — for raw packets — a check that rejects embedded line breaks, a classic trick for smuggling a second packet inside one line. Accepted data is then re-stamped by `TagFileImport`:

```csharp
var metadata = dto.SourceMetadata with
{
    SourceName = ... ?? "File Import",
    SourceType = ExternalSourceType.FileImport,
    SourceId   = ... ?? "file-import",
    Origin     = ContractDataOrigin.Imported,
    TrustLevel = ExternalTrustLevel.External
};
```

Whatever the file *claimed* about its own trust, the import forces it to `FileImport` / `Imported` / `External`. A malicious file cannot label itself as trusted internal data. When the folder scanner (`ScanImportFolderAsync`) is used, accepted files are moved to a `processed/` folder and rejected ones to `rejected/` — the doc's guidance is explicit that APRS-Command "should not silently repair unsafe imports," leaving a human paper trail.

> **The transmit-requests folder is bait, not a feature** — The import layout includes an incoming/transmit-requests/ folder, and it is a deliberate dead end. In ImportAsync, the TransmitRequests kind is intercepted before any parsing: it publishes a PacketTransmitBlocked event and returns a rejection, ‘Imported transmit requests are disabled and blocked by policy.’ Just like the REST transmit endpoint, the door is framed but has no handle.


### Surface 4: the plugin / driver framework

The fourth surface is the most forward-looking. A *plugin* is add-on code that snaps into an app to extend it; a *driver* is a plugin specialized for feeding in data from one kind of device or source. The framework itself (Phase 14.11) is currently a *foundation and a set of rules* — `docs/architecture/PLUGIN_DRIVER_FRAMEWORK.md` states plainly that "runtime plugin loading remains disabled by default," and unsigned plugins are rejected, operator approval is required, and transmit permissions are denied.

But the *pattern* is already alive and shipping in the weather input drivers, which is the best way to see what a future plugin looks like. The `IWeatherInputDriver` interface is the contract every weather source obeys — Davis, Ecowitt, Ambient, Tempest, PeetBros, a manual entry, and a file-import bridge each implement it:

```csharp
public interface IWeatherInputDriver
{
    string DriverId { get; }
    string DriverName { get; }
    WeatherInputDriverType DriverType { get; }
    bool Enabled { get; }
    WeatherInputDriverStatus Status { get; }
    CommonWeatherObservation? LastObservation { get; }
    Exception? LastError { get; }
    event EventHandler<WeatherObservationReceivedEventArgs>? ObservationReceived;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
```

Every driver reports an identity, an enabled flag, a status, its last observation, its last error, and raises an `ObservationReceived` event when new data arrives — it *provides observations*, it does not command anything. That is exactly the shape the plugin framework generalizes: a plugin declares a manifest (id, publisher, capabilities, requested permissions, signature), the operator approves it, it starts in a restricted context, it reports health, and it publishes notification events through approved abstractions. The framework doc's "What Plugins Must Not Do" list is blunt: no direct transmit, no bypassing central transmit safety, no starting hidden transports, no hardcoded secrets, no touching internal models directly, no depending on the Avalonia UI.


### The security model, in one place

All four surfaces answer to a single security posture, spelled out in `docs/architecture/EXTENSION_SECURITY_MODEL.md` and `docs/architecture/EXTENSION_SAFETY_RULES.md`: extensions are untrusted until the operator configures them, default to `ReadOnly`, default to unknown/untrusted source metadata, and — the load-bearing sentence — "No extension can transmit by default."

The transmit rule is worth stating in full because it is the project's central safety claim. Every transmit-capable action — APRS-IS, RF, iGate, digipeater, beaconing, weather beaconing, object transmit, message transmit, and any *future* API-queued or plugin-requested or file-imported packet — must pass one centralized safety check. And a permission, the doc stresses, "is not enough by itself": the central policy still weighs current operator settings, station profile, port state, training/replay lockout, stale-data checks, and rate limits before anything could go on the air. The extension surfaces don't get their own private path to the radio; there is exactly one path, and it is guarded in one place.

| Concern | How every surface handles it |
| --- | --- |
| Default state | Disabled — must be explicitly enabled by the operator |
| Network exposure | Localhost-only bind address, enforced at start |
| Authentication | Token required by default; REST uses constant-time comparison and fails closed |
| Incoming data trust | Re-stamped with a source tag it cannot forge (LocalApi / FileImport / Plugin) |
| Transmit | Structurally blocked; only the one central safety gate may ever authorize it |
| Failure isolation | A failing client or import cannot crash the service or the app |

> **For a maintainer adding a fifth surface** — Follow the four existing ones exactly: put the contract in AprsCommand.Contracts, put the service in AprsCommand.Api, default every enable flag to false, bind localhost-only, require a token, re-stamp all inbound data with a fresh source tag, publish notifications through the event bus rather than reaching into internal services, and route any transmit ambition through the central safety gate — never around it.


## Why It Matters / Design Takeaways

APRS-Command is amateur-radio software, and an accidental or malicious transmission is a real-world harm, not a cosmetic bug. The extension surfaces are designed so that opening the app up to outside tools never widens that risk. They do it through a small set of repeated moves: a compiler-enforced project boundary between public contracts and internal models; safe defaults that leave everything off until an operator opts in; unforgeable source tags on every byte that crosses the line; a notification-only event bus that observers cannot turn into a command channel; and a single central transmit gate that every path — present and future — must pass through.

The recurring "door with no handle" motif — the `501` transmit endpoint, the blocked `transmit-requests` folder, the plugin framework that ships disabled — is the deepest design lesson here. The safety machinery is built, wired, and tested *before* the feature it guards exists. When a real transmit path is finally added, the fences it must pass are already standing, already exercised, and already the only way through.


# 20. Testing: the xUnit Suite and How to Add to It

*How APRS-Command stays correct: the xUnit suite, its exact-string and round-trip assertions, the framework-free fake pattern for testing radio code, and how to add a test.*


## What This Is / What It Is For

APRS-Command is software that a ham radio operator trusts to put real signals on a real antenna. When it beacons your position, it is transmitting on a public radio frequency under your callsign and license. A bug in the code is not a cosmetic glitch — it can send a malformed packet, transmit when you told it not to, or garble your coordinates so badly that other stations plot you in the wrong state. The *test suite* is the safety net that catches those mistakes before they ever reach the air.

A *test suite* is simply a large collection of small programs, each of which runs one piece of the real code and then checks that it did the right thing. If the check passes, the test is silent. If the check fails, the test shouts. Run the whole suite and you get a single verdict in seconds: is the code still doing what we promised, or did a recent change quietly break something?

This chapter explains the suite that lives in `tests/Aprs.Tests` — what it covers, the one clever trick it uses to test radio code without touching a radio, and exactly how you add a new test when you change something. Everything here is drawn from the real files in the repository.

> **Scale** — As of this writing the main test project holds 123 test classes and roughly 1,300+ individual test cases (1,186 `[Fact]` methods plus dozens of `[Theory]` methods that each run several input rows). That is the living proof that the app behaves — and the thing you must keep green.


### What tool runs the tests: xUnit

*xUnit* is the testing framework the project uses. A *testing framework* is a library that knows how to find your test methods, run every one of them, and report which passed and which failed — you write the checks, it does the bookkeeping. There are several such frameworks in the .NET world; APRS-Command standardized on xUnit, which is the modern mainstream choice.

The whole toolchain is declared in one small project file. Here is the real `tests/Aprs.Tests/Aprs.Tests.csproj`, trimmed to the parts that matter:

```csharp
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
<ProjectReference Include="..\..\src\Aprs.Core\Aprs.Core.csproj" />
<ProjectReference Include="..\..\src\Aprs.Services\Aprs.Services.csproj" />
<ProjectReference Include="..\..\src\Aprs.Transport\Aprs.Transport.csproj" />
```

Reading it top to bottom: `xunit` is the framework itself. `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` are the plumbing that lets both Visual Studio and the command line `dotnet test` discover and launch the tests. The `ProjectReference` lines are the important part — the test project references the actual production projects (`Aprs.Core`, `Aprs.Services`, `Aprs.Transport`, and more). That means the tests run the *real* code, not a copy. There is no separate 'test version' of the parser to drift out of sync.

To run everything, you type one command at the repository root:

```csharp
dotnet test tests/Aprs.Tests/Aprs.Tests.csproj
```


### The anatomy of a single test

Every test follows the same three-beat rhythm, universally called *Arrange, Act, Assert*: set up the situation, do the one thing you are testing, then check the outcome. Here is a complete real test from `AprsBeaconFormatterTests.cs`. It is the single most important kind of test in this app — proof that when we format a position beacon, the exact characters we put on the air are correct:

```csharp
[Fact]
public void FormatFixedPositionBeacon_FormatsExpectedPacket()
{
    var formatter = new AprsBeaconFormatter();                    // Arrange

    var result = formatter.FormatFixedPositionBeacon(CreateInput( // Act
        source: "N0CALL",
        latitude: 39.058333,
        longitude: -84.508333,
        comment: "Test beacon"));

    Assert.True(result.IsSuccess);                                // Assert
    Assert.Equal(
        "N0CALL>APCMD0,WIDE1-1:!3903.50N/08430.50W-Test beacon",
        result.Packet);
}
```

Line by line: `[Fact]` is xUnit's marker meaning 'this method is a test — run it.' A *Fact* is a test with no inputs; it always does the same thing. The method name, `FormatFixedPositionBeacon_FormatsExpectedPacket`, is deliberately a sentence: the method under test, an underscore, and the expected behavior. When it fails, the failure report reads like plain English.

The *Assert* lines are where the judgment happens. `Assert.True(result.IsSuccess)` demands the formatter reported success. `Assert.Equal(...)` demands the produced packet string equals the expected string *character for character*. That expected string, `!3903.50N/08430.50W`, is APRS's fixed-column position format — the `N`/`W` hemispheres and the decimal point all sit in exact positions the specification dictates. One stray digit and the assert fails. This is why the app can be confident the bytes it transmits are spec-legal.

> **The exact-string assertion is the whole point** — Radio formats are unforgiving fixed-column layouts — a receiver counts characters by position, not by looking for labels. Tests that assert the literal output string are how APRS-Command guarantees interoperability with every other APRS station on the planet. Never loosen one of these into a 'contains' check.


### Testing many inputs at once: Theory and InlineData

Sometimes one rule must hold across many inputs — northern latitudes print `N`, southern ones print `S`. Writing a separate `[Fact]` for each would be tedious and easy to under-cover. xUnit's answer is the *Theory*: a test that runs once per row of data you hand it. Each row is an `[InlineData]` line. Here is a real one, also from the formatter tests:

```csharp
[Theory]
[InlineData(39.058333, "3903.50N")]
[InlineData(-39.058333, "3903.50S")]
public void FormatLatitude_FormatsHemispheres(double latitude, string expected)
{
    Assert.Equal(expected, AprsCoordinateFormatter.FormatLatitude(latitude));
}
```

The `[Theory]` marker says 'this is a data-driven test.' Each `[InlineData]` supplies the arguments for one run: the first passes `39.058333` and expects `3903.50N`; the second passes the negative (southern) value and expects `3903.50S`. xUnit runs the body twice and reports two results. If someone breaks the sign handling, exactly the affected row lights up red while the other stays green — the failure points straight at the bug. The parser tests in `AprsParserTests.cs` use the same technique to feed whole raw packet lines through `TryParse` and check every field that comes back out.


### What the suite actually covers

The tests are not spread evenly — they cluster where mistakes are most dangerous or most likely. Three clusters dominate.

| Cluster | Representative test files | What it guards |
| --- | --- | --- |
| Packet parsing | AprsParserTests, AprsMicEParserTests, AprsSpec101ConformanceTests | That every legal APRS format is decoded correctly and malformed input is rejected, not misread |
| Formatting & round-trip | AprsBeaconFormatterTests, AprsRoundTripTests, AprsWeatherRoundTripTests | That what we transmit is spec-legal to the exact character |
| Transmit safety | TransmitSafetyAuthorityTests, TransmitInhibitGateTests, BeaconSchedulerTests | That the app never keys the radio when policy, identity, or the operator says it must not |

The *round-trip* tests deserve special mention because they catch a whole category of bug the other two miss. A round-trip test formats a packet, then immediately parses that packet back with the app's own parser, and checks the values survived the trip unchanged. From `AprsRoundTripTests.cs`:

```csharp
var result = formatter.FormatFixedPositionBeacon(formatter.CreateInputFromProfile(profile));
Assert.True(result.IsSuccess);

var parsed = new AprsParser().Parse(result.Packet!, Now);

var pos = Assert.IsType<PositionAprsPacket>(parsed);
Assert.Equal("KD8ABC", pos.SourceCallsign);
Assert.Equal(39.0583, pos.Latitude!.Value, 4);   // format carries ~60 ft resolution
```

The file's own comment says it best: it 'guards against the generate and parse sides drifting apart.' If someone changes the formatter but forgets the parser (or vice versa), a plain formatting test might still pass while real-world interoperability silently breaks. The round-trip test fails, because the two halves no longer agree. Note the fourth argument `4` on the coordinate assert — that is xUnit's precision tolerance, telling it to compare to four decimal places, because the APRS uncompressed format only carries about 60 feet of resolution and demanding exact floating-point equality would be wrong.


## The mocking pattern: testing radio code without a radio

Here is the central problem. Much of the code that most needs testing is code that talks to the outside world — it sends packets to an APRS-IS internet server, or waits for a clock to tick, or asks a port manager whether a transmitter is connected. You cannot let a test actually transmit to the internet, and you cannot make a test wait 30 real minutes for a beacon timer. So how do you test code that depends on those things?

The answer is the *mock* (also called a *fake* or *stub*): a stand-in object that looks exactly like the real thing to the code being tested, but is simple, instant, and completely under the test's control. Think of it as a crash-test dummy — the same shape and weight as a person, safe to slam into a wall a thousand times. APRS-Command builds these by hand; it uses *no mocking framework* at all (no Moq, no NSubstitute). The trick that makes hand-built fakes possible is that the real code depends on *interfaces*, not concrete classes.

> **What an interface is** — An *interface* is a contract that lists what an object can do (its methods) without saying how. `IAprsIsClient` promises 'I can connect, send a packet, and read packets' — but the promise says nothing about whether a real network is behind it. Production code plugs in the real network client; a test plugs in a fake that records what it was asked to do. Neither can tell the difference.

Here is the real fake from `BeaconSchedulerTests.cs`, trimmed to its essence. It implements `IAprsIsClient` — the same interface the real internet client implements:

```csharp
private sealed class FakeAprsIsClient : IAprsIsClient
{
    public AprsIsConnectionState State { get; set; } = AprsIsConnectionState.Disconnected;
    public int SendCallCount { get; private set; }
    public Func<string, AprsIsTransmitResult>? SendResultFactory { get; set; }

    public Task<AprsIsTransmitResult> SendRawPacketAsync(
        string rawPacketLine, bool transmitConfirmed, CancellationToken cancellationToken)
    {
        SendCallCount++;                       // record that a send was attempted
        var result = SendResultFactory?.Invoke(rawPacketLine)
            ?? AprsIsTransmitResult.Succeeded(TestNow, rawPacketLine, State);
        return Task.FromResult(result);        // hand back an instant, canned answer
    }
}
```

Two design touches make this fake powerful. First, `SendCallCount` is a *spy*: every time the code under test asks to transmit, the counter goes up. A test can then assert `Assert.Equal(0, client.SendCallCount)` to prove the code did *not* transmit — which is exactly how transmit-safety is verified. Second, `SendResultFactory` is a scripting hook: a test can hand the fake a function that makes it 'fail' with a receive-only error, so the failure-handling path can be exercised on demand without ever needing the real error to occur.

You can see both in action. This real test proves that when transmit is disabled, the scheduler blocks the beacon *without even calling the client*:

```csharp
[Fact]
public async Task BeaconNow_WhenTransmitDisabled_BlocksWithoutCallingAprsIsClient()
{
    var (profileService, client, scheduler, _) = CreateScheduler(aprsIsBeaconEnabled: true);
    profileService.UpdateProfile(CreateValidProfile(transmitEnabled: false, ...), TestNow);
    scheduler.Start();

    var result = await scheduler.BeaconNowAsync(CancellationToken.None);

    Assert.True(result.Blocked);
    Assert.Equal(0, client.SendCallCount);   // the smoking gun: zero transmit attempts
}
```

The `CreateScheduler(...)` call is a *factory helper* — a private method at the bottom of the test class that assembles a scheduler wired to fakes and returns all the pieces as a tuple. It exists so that dozens of tests share one consistent setup instead of each rebuilding the object graph by hand. Note also `IBeaconSchedulerClock`: the fake clock lets a test say 'the time is now exactly 12:00' and later 'now it is 12:31,' so a 30-minute beacon interval can be tested in microseconds. This is why the CLAUDE.md rule forbids `Thread.Sleep` — real waiting has no place in a test.


### The same pattern guards the transmit-safety authority

The most safety-critical class in the app is the *TransmitSafetyAuthority* — the single gate every transmit request must pass through. Its tests use a fake for the policy context. From `TransmitSafetyAuthorityTests.cs`:

```csharp
private sealed class FakePolicy : ITransmitPolicyContext
{
    public bool HasValidStationCallsign { get; set; } = true;
    public bool HasValidAprsIsPasscode { get; set; } = true;
}
```

By flipping those two booleans, a test can conjure any station-identity situation — valid callsign, placeholder callsign, good passcode, bad passcode — and confirm the authority reaches the right verdict. The tests then check both halves of the decision object: whether transmit is allowed, and the specific reason it was denied.

```csharp
[Fact]
public void Inhibit_BlocksEverything_EvenWhenEverythingElseIsValid()
{
    var authority = Authority(out _);

    authority.Inhibit("Exercise mode");
    var decision = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.Rf));

    Assert.False(decision.IsAllowed);
    Assert.Equal(TransmitDenyReason.GlobalInhibit, decision.Reason);
}
```

This encodes a real operational rule: during a training exercise or drill, the operator can throw a global *inhibit* that blocks all transmission regardless of how valid everything else is. Other tests in the same file prove inhibit takes *priority* over identity and port failures — that when several things are wrong at once, the authority reports them in a fixed, predictable order. Getting that priority right is subtle, and the tests pin it down permanently.


## A worked example: adding a new test

Suppose you add a feature: position beacons in the eastern hemisphere. You want to prove the formatter prints `E` for a positive longitude the same way it already prints `W` for a negative one. Here is the full, real workflow.

1. Find the right file. Formatting tests live in tests/Aprs.Tests/AprsBeaconFormatterTests.cs — the file is named after the class it tests. This naming convention (SomethingTests.cs for the Something class) holds across all 123 test classes, so the right file is always predictable.
2. Decide Fact or Theory. Because you are checking one rule across two inputs (west and east), a [Theory] is the natural fit — and one already exists: FormatLongitude_FormatsHemispheres. You are extending coverage, so you add an [InlineData] row rather than writing a whole new method.
3. Write the assertion first, in your head: FormatLongitude(84.508333) should return the string "08430.50E". That expected string is the fixed-column APRS longitude with an E hemisphere.
4. Add the row. The real file already contains both rows, which is the finished result of exactly this exercise:
5. Run just this test to confirm: dotnet test --filter FullyQualifiedName~FormatLongitude. A green result means the new input is covered and correct.
6. Run the whole suite once before committing: dotnet test. A new test must never turn an existing one red — if it does, your change had a side effect you did not expect.

```csharp
[Theory]
[InlineData(-84.508333, "08430.50W")]
[InlineData(84.508333, "08430.50E")]
public void FormatLongitude_FormatsHemispheres(double longitude, string expected)
{
    Assert.Equal(expected, AprsCoordinateFormatter.FormatLongitude(longitude));
}
```

If instead you were testing brand-new behavior with an external dependency — say a new kind of beacon that talks to the APRS-IS client — you would follow the mocking pattern above: reuse or extend `FakeAprsIsClient`, wire it through a factory helper like `CreateScheduler`, and assert on `SendCallCount` and the returned result. You would never reach for a real network connection, and you would never add a mocking-framework package, because the project deliberately does neither.

> **The one rule from the Bug-Hunting Playbook** — Every bug found gets a regression test before you move on. A *regression test* is a test that reproduces a bug you just fixed, so the same bug can never silently return. When you fix something, first write the test that fails because of the bug, then apply the fix and watch it go green. That failing-then-passing moment is your proof the fix actually works.


### A quietly clever category: testing the documentation itself

One test file, `DeveloperDocumentationExamplesTests.cs`, does something unusual and worth copying: it tests that the example files shipped in the repository's `examples/` folder are actually valid. If a developer copies the sample station-import JSON from the docs, it must work — so a test parses that very file and checks its contents:

```csharp
[Fact]
public void ExampleStationImportJsonParses()
{
    using var document = ParseExample("examples/file-hooks/station-import.example.json");
    Assert.Equal("1.0", document.RootElement.GetProperty("schemaVersion").GetString());
    Assert.Equal("SIM001", document.RootElement.GetProperty("callsign").GetString());
}
```

This closes a gap that plagues most projects: documentation that slowly rots because nothing checks it. Here, if someone changes the import schema and forgets to update the example file, this test fails and forces the fix. It is a small idea with an outsized payoff, and a good template when you add new example files of your own.


## Why It Matters / Design Takeaways

The APRS-Command test suite is the mechanism that lets one person maintain a program complex enough to transmit on licensed radio spectrum without living in fear of every change. It is worth internalizing the handful of decisions that make it work.

- Tests run the real code. The test project references the production projects directly, so there is no parallel 'test implementation' to drift out of sync — every test exercises exactly what ships.
- Exact-string assertions are the backbone. Radio formats are fixed-column and unforgiving; asserting the literal transmitted bytes is what guarantees interoperability. Never soften these into loose contains-checks.
- Round-trip tests catch drift the single-sided tests miss. Format-then-parse proves the two halves of the codec still agree, which is where subtle interop bugs hide.
- Fakes, not frameworks. The project hand-writes small fakes that implement the same interfaces as the real network client, clock, and policy — no Moq, no NSubstitute. This keeps tests instant, deterministic, and free of hidden magic.
- Spies prove the negative. A call-counter on the fake (SendCallCount == 0) is how the app proves it did NOT transmit — the single most important assertion in a transmit-safety test.
- Fake clocks replace real waiting. A controllable IBeaconSchedulerClock tests a 30-minute interval in microseconds, which is why Thread.Sleep is banned from the codebase.
- Names are sentences and files mirror classes. Method_Scenario_ExpectedResult naming and SomethingTests.cs file naming mean failures read like English and the right file is always obvious.
- Every bug gets a regression test. The playbook's one non-negotiable rule: reproduce the bug in a failing test first, then fix it. That is how a fixed bug stays fixed for the life of the project.


# 21. How This Codebase Is Meant to Grow

*The four rules a future maintainer must never break, and a worked example of adding a feature through every layer.*


## What This Is / What It Is For

This chapter is the contract between the person who wrote APRS-Command and everyone who will touch it later. Its job is to make the codebase outlive its author: to explain not just how the pieces fit, but the handful of design decisions that hold the whole thing together, so a future contributor can add a feature without accidentally tearing out a load-bearing wall.

A *codebase* is just the full collection of source files that make up the program. Most codebases rot the same way: someone in a hurry reaches across a boundary that was there for a reason — a screen talks directly to a radio, a piece of received data loses track of where it came from, a new button transmits over the air without asking permission — and each shortcut makes the next shortcut easier. APRS-Command is built to resist that. It leans on four rules, and it wires the code so that breaking them is hard, sometimes impossible, rather than merely discouraged.

> **APRS in one sentence** — APRS (Automatic Packet Reporting System) is a ham-radio system where stations broadcast short text 'packets' with their position, weather, or messages. APRS-Command receives those packets from radios and the internet, plots them on a map, and — carefully — can transmit them back out.


### The four rules, at a glance

Everything in this chapter comes back to these four. They are stated plainly in the project's own `AGENTS.md` architecture rules and enforced throughout the source. If you remember nothing else, remember these and the reason each exists.

| Rule | In plain words | What it prevents |
| --- | --- | --- |
| Layer boundaries | The code is split into stacked layers; each may only depend downward, never upward or sideways in the wrong direction. | A screen quietly reaching into a serial port; protocol logic dragging in UI code. |
| Source-tagging | Every packet or reading carries a label of where it came from — real radio, internet, replay, simulation, training. | Simulated or replayed data being mistaken for a live station, or worse, re-transmitted as real. |
| Centralized transmit safety | All eight transmit paths (RF, APRS-IS, beacon, object, message, weather, iGate, digipeater) ask the same one authority before keying up. | A new feature transmitting by a side path that forgot a safety check. |
| Receive-first | The app listens by default. Transmitting requires deliberate, explicit configuration. | An out-of-the-box install accidentally jamming a frequency or beaconing as a placeholder callsign. |


### Rule 1 — The layers, and why the compiler enforces them

*What it does:* the program is divided into separate projects stacked like floors of a building, and each floor is only allowed to look at the floors below it. *Why:* this keeps concerns from bleeding together — the part that speaks the APRS protocol never needs to know a screen exists, and the screen never needs to know whether a packet arrived over a radio or the internet. When concerns stay separate, you can change one without breaking the others.

The layers, bottom to top, each a separate `.csproj` project so the boundary is real and not just a folder convention:

| Project | Responsibility | May depend on |
| --- | --- | --- |
| Aprs.Core | Pure APRS packet models and the parser. No I/O, no UI. | nothing (framework only) |
| Aprs.Transport | Talking to radios and the internet: APRS-IS, KISS, Direwolf, serial, AGWPE. | Aprs.Core |
| Aprs.Services | Business logic: station database, beacon scheduler, messaging, transmit safety. | Aprs.Core |
| Aprs.Mapping | Map tiles and marker rendering logic. | Aprs.Core / Services |
| Aprs.Desktop | The Avalonia desktop app — views and viewmodels. | everything below |
| AprsCommand.Contracts / .Api | Versioned public data shapes for outside integrations. | kept separate on purpose |

The enforcement is the point. Because `Aprs.Core` has no *project reference* (a declared dependency from one project to another) to `Aprs.Transport` or `Aprs.Desktop`, a line of code in the parser that tried to open a serial port or touch a screen simply would not compile. The rule isn't a guideline you have to remember — the build tool rejects the violation. `AGENTS.md` spells out the same boundaries in words:

```csharp
Keep APRS protocol parsing in `Aprs.Core` only.
Keep transport-specific code in `Aprs.Transport` only.
Keep business logic in `Aprs.Services` only.
Keep UI logic in `Aprs.Desktop` only.
Do not put serial-port, TCP, file-system, or UI dependencies in `Aprs.Core`.
Prefer interfaces and dependency injection for services.
```

> **The direction is one-way** — Lower layers must never reach up. When a low layer needs something a higher layer knows (for example, the transport layer needs to ask 'are we in exercise mode?'), it declares a small interface it owns and the higher layer implements it. You will see exactly this pattern in Rule 3 with ITransmitInhibitGate — the transport defines the question, Services supplies the answer, and the dependency arrow still points downward.


### Rule 2 — Everything that arrives gets a source tag

*What it does:* every packet and reading that enters the app is stamped with a label saying where it came from. *Why:* APRS-Command doesn't just show live radio traffic. It also replays recorded logs, runs a built-in simulator, and has a training mode for exercises. If a simulated station looked identical to a real one, an operator could act on fiction during a real emergency, or the app could re-broadcast a fake packet as though it were genuine. The source tag is what keeps make-believe data visibly separated from reality — the same instinct behind the whole 'receive-first' philosophy.

The tag is not one flag but a small vocabulary of enums (an *enum* is a fixed list of named choices). The transport-facing tag `AprsPacketSource` says which pipe a packet came in on:

```csharp
public enum AprsPacketSource
{
    Unknown,
    AprsIs,        // the internet backbone
    Rf,            // over the air
    TcpKiss, SerialKiss, Direwolf, Agwpe,   // specific radio interfaces
    Replay,        // a recorded log being played back
    Simulation,    // the built-in packet generator
    External,
    LocalGenerated // something this app produced itself
}
```

For richer needs there is `SourceMetadata`, an *immutable record* (a small data object whose fields never change after creation) that pairs the source type with a trust level and an origin. Notice the two extra dimensions: `DataOrigin` (was this Received, Generated, Imported, Replayed, Simulated, Training…) and `SourceTrustLevel` (Untrusted, External, OperatorConfigured, Local, Internal). Three separate questions — what pipe, what kind of origin, how much do we trust it — because they genuinely differ. A packet can arrive over trusted local hardware but carry simulated content.

```csharp
public sealed record SourceMetadata(
    string? SourceName,
    DataSourceType SourceType,
    string? SourceId,
    DateTimeOffset TimestampUtc,
    DataOrigin Origin,
    SourceTrustLevel TrustLevel)
{
    // A safe default: unknown source, untrusted, at a known time.
    public static SourceMetadata Unknown(DateTimeOffset timestampUtc) =>
        new(null, DataSourceType.Unknown, null, timestampUtc,
            DataOrigin.Unknown, SourceTrustLevel.Untrusted);
}
```

The tag travels with the data from the moment of entry. The single receive funnel, `AprsIngestionService.IngestReceivedLine`, takes the source as a required argument and hands it forward at every step — into the raw log, into the station database, and out on the parsed-packet event:

```csharp
public void IngestReceivedLine(string rawLine, AprsPacketSource source, DateTimeOffset receivedAtUtc)
{
    if (string.IsNullOrWhiteSpace(rawLine)) return;

    rawPacketLog.AddReceivedRawPacket(rawLine, source, timestampUtc: receivedAtUtc);

    if (parser.TryParse(rawLine, receivedAtUtc, out var packet, out _) && packet is not null)
    {
        // Replayed packets go to a SEPARATE database so a replay session can be
        // shown in isolation without polluting live station state.
        var target = source == AprsPacketSource.Replay && replayStationDatabase is not null
            ? replayStationDatabase
            : stationDatabase;
        target.ProcessPacket(packet, source);
    }

    PacketParsed?.Invoke(this, new ParsedPacketEventArgs(packet, source));
}
```

That `target` switch is source-tagging earning its keep: because the line carries an honest `Replay` tag, the funnel can route replayed traffic to a dedicated database and keep the live map clean. `ProcessPacket(packet, source)` then defaults its source parameter to `Unknown` — meaning the only way a station gets a real source label is if the caller supplied one, and the compiler makes the caller think about it.

> **When you add a new way for data to enter** — A new input driver — a new radio type, a file importer, a plugin — must add its own value to the source enums and pass it through the ingestion funnel. Never let new data default to Unknown when you actually know its origin. There is a matching AprsPortSourceMapper that translates a port type to a packet source; extend it in lock-step so ports and packets agree.


### Rule 3 — Transmit safety lives in exactly one place

*What it does:* before anything is transmitted — a beacon, an object, a message, a weather bulletin, an iGate relay — the code asks one shared authority 'am I allowed to key up right now?' and obeys the answer. *Why:* transmitting is the one thing this app does that affects the outside world and other operators. Scattering the safety checks across eight features would guarantee that, eventually, one of them forgets one check. Funneling every path through a single gate means a rule added once is enforced everywhere, automatically.

The authority is `ITransmitSafetyAuthority` in the Services layer. Its `Evaluate` method runs the checks in a deliberate priority order and returns a `TransmitDecision` — allowed, or denied with a precise, human-readable reason:

```csharp
public TransmitDecision Evaluate(TransmitRequest request)
{
    // 1) Master inhibit wins over everything (exercise / training mode).
    lock (gate) { isInhibited = inhibited; reason = inhibitReason; }
    if (isInhibited)
        return TransmitDecision.Deny(TransmitDenyReason.GlobalInhibit, reason ?? "Transmit is inhibited.");

    // 2) Identity: never transmit without a real callsign (not the N0CALL placeholder).
    if (!policy.HasValidStationCallsign)
        return TransmitDecision.Deny(TransmitDenyReason.NoValidCallsign,
            "No valid station callsign is set...");

    // 3) Destination: transmitting to the internet needs a real APRS-IS passcode.
    if (request.Destination == TransmitDestination.AprsIs && !policy.HasValidAprsIsPasscode)
        return TransmitDecision.Deny(TransmitDenyReason.AprsIsPasscodeRequired, "...receive-only...");

    // 4) Per-port: is THIS port enabled, connected, transmit-enabled, not receive-only?
    var portResult = portManager.CheckTransmitSafety(request.PortId, globalTransmitSafetyEnabled: true);
    if (!portResult.IsSafe)
        return TransmitDecision.Deny(TransmitDenyReason.Port, portResult.FailureReason ?? "...");

    return TransmitDecision.Allow();
}
```

Read the order top to bottom: the global inhibit is checked first because it must beat everything — flip exercise mode on and no feature can transmit, full stop. Then identity, then destination credentials, then the specific port. Each denial names its cause, so the UI can tell the operator exactly why the radio stayed silent instead of failing mysteriously.

Now the clever part that satisfies Rule 1 at the same time. The lowest-level transmit code lives in `Aprs.Transport`, which may not depend upward on `Aprs.Services`. So the transport layer defines a tiny interface of its own — the only question it needs answered:

```csharp
// In Aprs.Transport — the minimal contract the transmit chokepoints consult.
public interface ITransmitInhibitGate
{
    bool IsTransmitInhibited { get; }
    string? InhibitReason { get; }
}
```

`TransmitSafetyAuthority` in Services implements that interface, and the *composition root* (the single startup place where the app wires its parts together) hands the very same object to every transmit client. One authority, worn two ways:

```csharp
// DesktopRuntime — the composition root.
var transmitAuthority = provider.GetRequiredService<ITransmitSafetyAuthority>();
var inhibitGate = (ITransmitInhibitGate)transmitAuthority;   // same object, transport's view of it
rfTransmitClient.InhibitGate = inhibitGate;

var beaconService = BeaconService.CreateFromSettings(..., inhibitGate: inhibitGate);
```

And the RF client actually consults it, at the last moment before a frame is encoded and sent — so even a path that never called `Evaluate` still cannot key up while inhibited:

```csharp
// KissRfBeaconTransmitClient.SendBeaconAsync
var gate = InhibitGate;
if (gate is not null && gate.IsTransmitInhibited)
    return Fail(gate.InhibitReason ?? "Transmit is globally inhibited (exercise mode).", rawPacket);
```

> **The one rule that must never be relaxed** — If you add any new way to transmit, it MUST go through the transmit authority (or at minimum consult the inhibit gate). Do not write a new SendPacket call that keys up on its own judgment. The whole guarantee — that exercise mode silences everything, that a placeholder callsign never goes on the air — collapses the instant one path decides the check doesn't apply to it.


### Rule 4 — Receive-first: silence is the default

*What it does:* a fresh install listens and plots; it does not transmit until the operator has deliberately set up a real identity and turned a specific port on. *Why:* radio spectrum is shared. An app that transmitted by default — or under a placeholder callsign like `N0CALL` — would be antisocial at best and, at a bad moment, harmful. `AGENTS.md` states it flatly: 'Do not transmit RF by default. Any RF transmit feature must require explicit user configuration.'

This principle is why the transmit checks in Rule 3 are shaped the way they are. The identity check refuses the `N0CALL` placeholder. The APRS-IS check treats the passcode value `-1` as a deliberate 'receive-only' sentinel — a magic value meaning 'I have not set up transmit credentials.' Transmit is opt-in, and the opt-in must be a real, positive act:

```csharp
// SettingsTransmitPolicyContext — 'may we transmit to the internet?'
public bool HasValidAprsIsPasscode
{
    get
    {
        foreach (var port in store.Load().Connections.Ports)
        {
            if (port.Type != ConnectionPortType.AprsIs) continue;
            var passcode = port.Configuration.AprsIs?.Passcode?.Trim();
            if (!string.IsNullOrEmpty(passcode)
                && !string.Equals(passcode, "-1", StringComparison.Ordinal)  // the receive-only sentinel
                && int.TryParse(passcode, out var value) && value >= 0)
                return true;
        }
        return false;   // default answer is NO
    }
}
```

The default return is `false`. You have to earn a `true`. That is receive-first expressed in code: the burden of proof is on transmitting, never on staying quiet. Preserve this whenever you add a feature that could emit — make the safe, silent state the one you get for free.


### A worked example — adding a new packet type end to end

Suppose a contributor wants first-class support for a new APRS packet variant the parser currently lumps into 'Unknown.' Here is the path through every layer, using the real seams the code already provides. The shape of this example generalizes to almost any feature.

1. Core type — add a new record to the AprsPacket family in Aprs.Core. Every packet is an immutable record deriving from the abstract `AprsPacket`, carrying the common header fields (source callsign, destination, path, information, received time, validity) plus its own decoded fields. Model your new type the same way, so downstream code can pattern-match on it.
2. Parser / transport — teach the parser to recognize it. `AprsParser.Parse` is an ordered chain of 'can you parse this?' checks; each delegates to a small specialized parser. Insert your check at the correct priority (order matters — weather is tried before generic position because their lead characters overlap). If the data instead arrives over a new wire, that is a new Aprs.Transport client feeding the same ingestion funnel with the correct source tag.
3. Service — decide what the app does with it. If it updates a station, `StationDatabase.ProcessPacket` already receives the packet and its source tag; extend the handling there or in the relevant peer service. This is where business meaning lives — never in the parser and never in a view.
4. Viewmodel — expose it to the screen. A *viewmodel* is the bridge object a screen binds to; it holds display-ready values and commands. Add the new fields to the appropriate row/detail viewmodel so the view has something to show, keeping the raw packet type out of the view itself.
5. View — render it. The Avalonia view binds to the viewmodel's properties. No transport, parser, or service type ever appears in a view — that is the Rule 1 boundary holding at the top of the stack.

Step 2 is worth seeing concretely, because the parser's dispatch order is a design decision, not an accident. Each guard returns the moment it matches:

```csharp
// AprsParser.Parse — ordered dispatch. Earlier wins.
if (weatherParser.CanParse(rawPacket.Information))   return weatherParser.Parse(rawPacket);
if (IsPositionInformation(rawPacket.Information))    return positionParser.Parse(rawPacket);
if (micEParser.CanParse(rawPacket.Information))      return micEParser.Parse(rawPacket);
// ...status '>', capability '<', telemetry, message, object/item, query '?'...
// <-- a new specialized parser check would slot in here, at its correct priority
if (rawPacket.Information.StartsWith('{')) { /* user-defined */ }
// Falls through to UnknownAprsPacket only when nothing above claimed it.
```

The `UnknownAprsPacket` at the very bottom is the safety net: anything unrecognized is still captured as a valid, labeled packet rather than thrown away or crashing the pipeline. That is the same defensive instinct as source-tagging — the app never silently loses data it doesn't understand.

> **The non-negotiable last step** — Every feature ships with a test. CONTRIBUTING.md and AGENTS.md both require it, and the parser especially is test-driven from sample packets. A new packet type without a parse test in tests/Aprs.Tests is an incomplete contribution. This mirrors the project's bug-hunting rule: every behavior you add or fix gets a test that pins it down, so the next person can't unknowingly break it.


### Public contracts stay separate from internal models

One more boundary a maintainer must respect. APRS-Command exposes a local API and event stream for outside integrations, and those outsiders see a *DTO* — a Data Transfer Object, a plain, stable data shape published for external use — not the app's internal models. Notice `ExternalSourceType` in `AprsCommand.Contracts` is a near-twin of the internal `DataSourceType`, deliberately duplicated rather than shared.

That looks redundant until you consider why: internal models change freely as the app evolves, but a published contract is a promise to third parties that must stay stable across versions. Keeping them as separate types with explicit mapping means you can refactor the internals without breaking someone's integration — and you can't accidentally leak an internal-only concept out through the public API. When you add an internal source, think separately about whether the public contract should learn about it too.


### The anti-patterns that quietly break the philosophy

These are the shortcuts that feel harmless in the moment and cost the project its structure. Each one is the direct negation of a rule above.

- A view or viewmodel that reaches straight into a transport client or the parser instead of going through a service abstraction — this is how the top of the stack fuses to the bottom and layers stop meaning anything.
- New data that enters the app untagged, or tagged `Unknown` when its true origin was known — this is how simulated, replayed, or training data starts masquerading as live traffic.
- A new transmit path that keys up on its own logic instead of consulting the transmit authority or the inhibit gate — this is how exercise mode stops being trustworthy.
- A feature that transmits (or beacons) by default, or that treats a blank/placeholder identity as good enough — this violates receive-first and can put bad traffic on shared spectrum.
- Putting business meaning in the parser, or protocol parsing in a service — decode belongs in Aprs.Core, meaning belongs in Aprs.Services, and mixing them makes both harder to test.
- Leaking internal model types through the public Contracts/API boundary instead of mapping to a DTO — this turns a private implementation detail into a promise you didn't mean to make.
- Shipping the change without a test — the one habit that, dropped, lets every other rule erode unnoticed.


## Why It Matters / Design Takeaways

APRS-Command is built so that the right way to extend it is also the easy way, and the wrong way is either hard or impossible. The layers are separate projects so a boundary violation won't compile. Source tags ride along with data so make-believe never impersonates reality. Every transmit path bows to one authority so a safety rule written once is enforced everywhere. And the app listens before it speaks, so an untouched install is a good radio citizen by default.

The through-line is that safety and clarity are structural, not disciplinary. The design does not ask a future contributor to remember to be careful; it arranges the code so that carefulness is the path of least resistance. When you add to this codebase, your real task is to find the seam that already exists — the ingestion funnel, the transmit authority, the parser's ordered dispatch, the source enums — and extend it in the established shape. Do that, add the test, and the program stays comprehensible and safe long after any single author has moved on. That is what it means for a codebase to be built to grow.

> **The one-line test before you commit** — Ask of any change: could this key up the radio without asking the authority? Could this data reach the map without an honest source tag? Does this make a screen depend on a wire? If any answer is yes, you're fighting the architecture — find the seam and go through it instead.


# 22. How This Book Is Maintained (Amendments Register)

*The stable-numbering, dated-amendment discipline that lets this guide grow and stay correct for years without ever being reprinted — and the living Amendments Register that records its state.*


## What This Is / What It Is For

This is the chapter about the book itself — the one that explains how the very document you are reading stays alive and accurate for years without ever being reprinted from scratch. Every other chapter explains a piece of the program. This one explains the *housekeeping rules* that let the whole book grow, get corrected, and gain new chapters over time while never once breaking a reference someone wrote in the past.

The short version: *section numbers are permanent*, changes ship as small *dated amendments* instead of silent rewrites, and a single running table — the *Amendments Register* — records exactly what state the book is in. If you are the next person to touch this guide, this chapter is your rulebook. Follow it and the book keeps its integrity; ignore it and cross-references start pointing at the wrong things.

> **Where this discipline is defined** — The rules in this chapter are the plan-of-record recorded in docs/planning/DOCUMENTATION_PLAN.md (the "Amendment / supplement model" section) and the locked outline in docs/programming-guide/OUTLINE.md. This chapter, §22, is the authoritative home of the Amendments Register; §1 introduces the idea, but the live register lives here.


### The problem this solves: books that die because they can't be updated

*WHAT it does.* The maintenance model lets the guide be improved a little at a time — fix a paragraph here, add a chapter there — without republishing the entire book and without invalidating anything already written. *Jargon check:* to *invalidate a reference* means to make an old pointer wrong; if someone wrote "see §11" in an email last year and §11 now means something different, their pointer has been invalidated. The whole model exists to make that impossible.

*WHY it was built this way.* A program under active development changes constantly. A book that must be reprinted cover-to-cover every time one sentence goes stale is a book nobody keeps current — the effort is too large, so updates never happen, and the documentation quietly rots away from the code. The author put it plainly in the plan: the goal is to improve the book "without reprinting the whole book" — in his words, "we won't kill so many trees." The rejected alternative is the obvious one: edit the master document freely and re-issue it. That feels simpler but it has a fatal flaw — every edit can silently shift section numbers, and once numbers shift, every reference ever written to this book becomes a small landmine.

This is not a new invention. It is borrowed from how technical standards bodies and legal codes stay current. A law is not rewritten from page one every time a clause changes; an *amendment* is published, dated, and appended, and the section numbers of the original stand untouched forever. This guide uses that exact discipline.


### Rule one: section numbers are permanent

*WHAT it does.* Once a section number is assigned a meaning, it keeps that meaning forever. Once §11 means the Transmit-Safety Authority, §11 means the Transmit-Safety Authority for the life of the book. New material is *added* with a fresh, never-before-used number; it is never *inserted* between existing sections in a way that pushes the later numbers up by one.

*WHY.* Permanence is the single property that makes every other convenience possible. Because numbers never move, a cross-reference written today — in the code, in another document, in someone's notes — still points at the same place a decade from now. The instant you allow renumbering, you lose that guarantee entirely, and no reader can ever again be sure the "§14" they were told about is the "§14" they are now reading. There is no partial version of this rule; it is all-or-nothing, which is why it is stated as an absolute.

> **The one rule you cannot break** — Never renumber an existing section — not to "tidy up," not to make the order read better, not for any reason. To revise a section, issue an AMENDS for its existing number. To add material, issue an ADDS with the next unused number. This single rule is the mechanism that keeps every reference anyone has ever written valid. Everything else in this chapter is bookkeeping around it.


### Rule two: changes ship as dated amendments with two tags

*WHAT it does.* When something needs to change, the change does not get buried as a silent edit inside a fresh reprint. It ships as its own short, dated, standalone *amendment* — a small document that can be printed on its own and appended to the book. *Jargon check:* an *amendment* here is exactly like a legal amendment — a self-contained, dated note that says "as of this date, this part of the book now reads differently" (or "there is now a new part").

Every amendment carries one of exactly two tags, so its nature is obvious at a single glance:

| Tag | Meaning | Effect on existing numbers |
| --- | --- | --- |
| AMENDS §X.Y | Revises existing material within section X.Y — the new text supersedes the old text for that subsection. | None. §X.Y keeps its number; only its contents are updated. |
| ADDS §Z | Adds an entirely new section, numbered §Z after the last one currently in use. | None. All existing sections keep their numbers; the book simply gains a new one. |

```csharp
AMENDS §11.3   // revises existing material within section 11.3
ADDS   §23     // adds an entirely new section, numbered after the last one

// An amendment is dated, printable on its own, and appended to the book.
// Existing section numbers are never renumbered to make room — only extended.
```

*HOW it works in practice.* Suppose the Transmit-Safety Authority (documented in §11) gains a new confirmation step and the explanation in §11.3 is now slightly wrong. You do not edit §11.3 in place and quietly re-release the book. You write an amendment tagged *`AMENDS §11.3`*, date it, state what the subsection now says, and log one row in the Amendments Register. Anyone holding an older printing keeps their book and simply appends your amendment page; anyone reading the living Markdown sees the register row telling them the change exists. Now suppose instead you want to document a brand-new subsystem that has no home yet. You write it as a new section tagged *`ADDS §23`* (assuming §22 was the last section), and again log one row. Nothing that already existed moves.


### Rule three: the Amendments Register records the book's state

*WHAT it does.* The *Amendments Register* is a single running table — kept here, in §22 — that lists every amendment ever issued, with its date, its tag, and a one-line summary. It is the book's index of its own changes. A reader can glance at it and know two things instantly: exactly what state the book is in, and which loose amendment pages, if any, belong appended to their copy.

*WHY a register at all?* Because amendments are standalone and dated, they could otherwise scatter — someone could hold three amendment pages and have no way to know a fourth exists. The register is the authoritative checklist. If it is not in the register, it did not officially happen; if it is in the register, every copy of the book should carry it. The register is what turns a pile of loose dated notes into a coherent, verifiable edition.


### The starter Amendments Register (v1.0 baseline)

At first publication the register is essentially empty — there is nothing to amend yet, because the book has just been written. It carries a single *baseline row* marking the v1.0 publication, followed here by two greyed template rows that show future contributors the exact shape their entries must take. The template rows are illustrations, not real amendments; delete-and-replace them with real ones as changes are issued.

| Date | Tag | Section | Summary |
| --- | --- | --- | --- |
| 2026-08-05 | BASELINE | — (all) | v1.0 — first publication of the APRS-Command Programming Guide Book. All sections §1–§22 established at their permanent numbers. No amendments issued. |
| (future) | AMENDS §X.Y | §X.Y | Example row — revises the named subsection; the new text supersedes the prior text. Existing numbers unchanged. |
| (future) | ADDS §Z | §Z | Example row — adds a new section at the next unused number. Existing numbers unchanged. |

> **How to add a real row** — When you issue an amendment: (1) pick the tag — AMENDS to revise, ADDS to add; (2) for ADDS, use the next section number not yet in use (never reuse or insert); (3) add one row here with today's date, the tag, the section, and a one-line summary; (4) ship the amendment as its own dated page. Keep rows in date order, newest at the bottom, so the register reads as a timeline.


### Why three formats don't complicate the model

The book is produced in three formats, and understanding which one you edit keeps the discipline clean. The *Markdown* files in the repository are the *living source of truth* — they sit beside the code, travel with it through version control, and are the only thing you hand-edit. The polished *PDF* and *Word* editions are *generated* from that Markdown for reading, printing, and sharing; they are never edited by hand. So an amendment is always, at bottom, an edit to the Markdown source plus a new register row — the PDF and Word simply get regenerated.

There is one more safeguard worth naming: the *freshness manifest*, a mapping from each chapter to the specific source files it describes. *Jargon check:* a *manifest* is just a listing — here, a table that says "chapter §11 describes TransmitSafetyAuthority.cs," and so on. Its purpose is honesty: when the code changes, a single `git diff` can flag exactly which chapters might now be stale, so the person maintaining the book knows precisely where to look instead of guessing. The manifest tells you *what* might need an amendment; this chapter's rules tell you *how* to issue one.


## Why It Matters / Design Takeaways

This book was written for the same reason the program was: so that a tool a community depends on cannot quietly be lost, and so the *reasoning* behind it — not just the code — survives its author. A book that cannot be safely updated fails that mission as surely as code with no documentation at all. The maintenance discipline in this chapter is what makes the guide durable rather than merely well-written.

Hold three ideas and you have the whole model. First, *numbers are permanent* — never renumber, because that one rule is what keeps every reference ever written valid. Second, *changes are dated amendments*, tagged AMENDS to revise or ADDS to add, shipped as small standalone pages instead of silent rewrites. Third, *the register is the truth of the book's state* — if a change is not logged there, it did not officially happen. Follow those three and the book, like the program it documents, stays alive long after the people who started it have moved on.

> **For the next maintainer** — You do not need permission to improve this book — you need only to follow the discipline. Fix a paragraph with an AMENDS; document something new with an ADDS at the next free number; log every one in the Amendments Register above with today's date. The only thing you must never do is renumber an existing section. That restraint is not bureaucracy — it is the single promise this book makes to everyone who will ever cite it.
