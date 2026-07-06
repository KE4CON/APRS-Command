using System.Collections.Concurrent;
using System.Diagnostics;
using Aprs.Core;
using Aprs.Transport;

// ── Configuration ─────────────────────────────────────────────────────────────
var callsign        = GetArg(args, "--callsign",  env: "APRS_CALLSIGN",  def: "N0CALL");
var passcode        = GetArg(args, "--passcode",  env: "APRS_PASSCODE",  def: "-1");
var filterRadius    = GetArg(args, "--radius",    env: "APRS_RADIUS",    def: "500");
var durationMinStr  = GetArg(args, "--minutes",   env: "APRS_MINUTES",   def: "10");
var slowMs          = int.Parse(GetArg(args, "--slow-ms", env: "APRS_SLOW_MS", def: "50"));

if (!int.TryParse(durationMinStr, out var durationMin) || durationMin < 1)
{
    Console.Error.WriteLine("ERROR: --minutes must be a positive integer.");
    return 1;
}

// ── Run ───────────────────────────────────────────────────────────────────────
Console.WriteLine("╔═══════════════════════════════════════════════════════╗");
Console.WriteLine("║           APRS Command — APRS-IS Fuzz Harness        ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════╝");
Console.WriteLine();
Console.WriteLine($"  Server:    rotate.aprs2.net:14580");
Console.WriteLine($"  Callsign:  {callsign}  (passcode {passcode})");
Console.WriteLine($"  Duration:  {durationMin} minute(s)");
Console.WriteLine($"  Slow threshold: {slowMs} ms");
Console.WriteLine();

var results    = new FuzzResults();
var parser     = new AprsParser();
var sw         = Stopwatch.StartNew();
var lastReport = sw.Elapsed;

// World feed: no filter = full unfiltered stream (very high volume).
// Regional: range filter centred at a useful location (Times Square, NYC).
// APRS-IS max radius is ~3000 km — larger values are silently rejected.
var radiusInt = int.Parse(filterRadius);
Console.WriteLine($"  Filter:    {(radiusInt >= 20000 ? "(none — full world feed)" : $"r/40.7580/-73.9855/{radiusInt} km radius from NYC")}");
var filter = radiusInt >= 20000
    ? null                              // no filter = full world feed
    : $"r/40.7580/-73.9855/{radiusInt}"; // NYC centre, radius in km

var config = AprsIsClientConfiguration.Default with
{
    Callsign           = callsign,
    Passcode           = passcode,
    ApplicationName    = "APRSCommand-FuzzHarness",
    ApplicationVersion = "1.0",
    Filter             = filter,
    ReceiveOnly        = true,
    TransmitEnabled    = false,
    ReconnectEnabled   = false,  // don't reconnect during a timed run
};

// Use a plain CancellationTokenSource — not linked to anything that gets
// disposed before we're done. The harness controls cancellation entirely.
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(durationMin));

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    if (!cts.IsCancellationRequested) cts.Cancel();
};

Console.WriteLine("Connecting to APRS-IS...");

var client = new AprsIsClient(config);
try
{
    await client.ConnectAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Cancelled before connection completed.");
    await client.DisposeAsync();
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Connection failed: {ex.Message}");
    await client.DisposeAsync();
    return 2;
}

Console.WriteLine($"Connected. Streaming for {durationMin} minute(s). Press Ctrl+C to stop early.");
Console.WriteLine("  (Progress updates every 30 seconds. If no update appears, check connectivity.)");
Console.WriteLine();

// ── Packet loop — use ReadPacketsAsync (the correct consumption pattern) ──────
try
{
    await foreach (var e in client.ReadPacketsAsync(cts.Token).ConfigureAwait(false))
    {
        var raw = e.RawPacketLine;

        if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith('#'))
        {
            results.ServerComments++;
            continue;
        }

        results.TotalReceived++;

        // ── Parse ──────────────────────────────────────────────────────────
        AprsPacket? packet = null;
        Exception? parseException = null;
        var parseTimer = Stopwatch.StartNew();

        try
        {
            packet = parser.Parse(raw, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            parseException = ex;
        }
        finally
        {
            parseTimer.Stop();
        }

        // ── Record findings ────────────────────────────────────────────────
        if (parseException is not null)
        {
            results.Crashes.Add(new FuzzFinding(
                raw, $"CRASH: {parseException.GetType().Name}: {parseException.Message}",
                parseTimer.ElapsedMilliseconds));
            results.CrashCount++;
            continue;
        }

        if (packet is null)
        {
            results.Crashes.Add(new FuzzFinding(raw, "CRASH: parser returned null", 0));
            results.CrashCount++;
            continue;
        }

        if (parseTimer.ElapsedMilliseconds >= slowMs)
        {
            results.SlowPackets.Add(new FuzzFinding(
                raw,
                $"SLOW: {parseTimer.ElapsedMilliseconds} ms — {packet.GetType().Name}",
                parseTimer.ElapsedMilliseconds));
        }

        // ── Sanity checks ──────────────────────────────────────────────────
        if (packet.IsValid)
        {
            results.ValidCount++;
            results.TypeCounts.AddOrUpdate(packet.GetType().Name, 1, (_, n) => n + 1);

            if (packet is PositionAprsPacket pos)
            {
                if (pos.Latitude is < -90 or > 90)
                    results.Misdecodes.Add(new FuzzFinding(
                        raw, $"MISDECODE: latitude {pos.Latitude} out of range", 0));

                if (pos.Longitude is < -180 or > 180)
                    results.Misdecodes.Add(new FuzzFinding(
                        raw, $"MISDECODE: longitude {pos.Longitude} out of range", 0));

                if (pos.SpeedKnots is < 0 or > 3000)
                    results.Misdecodes.Add(new FuzzFinding(
                        raw, $"MISDECODE: speed {pos.SpeedKnots} kn out of range", 0));

                if (pos.CourseDegrees is < 0 or > 360)
                    results.Misdecodes.Add(new FuzzFinding(
                        raw, $"MISDECODE: course {pos.CourseDegrees}° out of range", 0));

                if (pos.AltitudeFeet is < -1500 or > 250000)
                    results.Misdecodes.Add(new FuzzFinding(
                        raw, $"MISDECODE: altitude {pos.AltitudeFeet} ft out of range", 0));
            }
        }
        else
        {
            results.InvalidCount++;
        }

        // ── Progress reporting every 30 seconds ────────────────────────────
        if (sw.Elapsed - lastReport > TimeSpan.FromSeconds(30))
        {
            lastReport = sw.Elapsed;
            var elapsed = sw.Elapsed;
            var pct = elapsed.TotalMinutes / durationMin * 100;
            Console.WriteLine(
                $"  [{elapsed:mm\\:ss}] {pct:F0}% — " +
                $"{results.TotalReceived:N0} packets  " +
                $"valid={results.ValidCount:N0}  " +
                $"invalid={results.InvalidCount:N0}  " +
                $"crashes={results.CrashCount}  " +
                $"misdecodes={results.Misdecodes.Count}  " +
                $"slow={results.SlowPackets.Count}");
        }
    }
}
catch (OperationCanceledException)
{
    // Normal end — timer expired or Ctrl+C
}
finally
{
    // Dispose carefully — the CTS may or may not already be triggered.
    // DisconnectAsync inside DisposeAsync calls Cancel() on the internal
    // CancellationTokenSource; guard against ObjectDisposedException.
    try { await client.DisposeAsync(); } catch { /* best-effort cleanup */ }
}

// ── Final report ──────────────────────────────────────────────────────────────
sw.Stop();
Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("  RESULTS");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine($"  Duration:       {sw.Elapsed:mm\\:ss}");
Console.WriteLine($"  Total received: {results.TotalReceived:N0}");
Console.WriteLine($"  Valid:          {results.ValidCount:N0} ({results.ValidCount * 100.0 / Math.Max(1, results.TotalReceived):F1}%)");
Console.WriteLine($"  Invalid:        {results.InvalidCount:N0} ({results.InvalidCount * 100.0 / Math.Max(1, results.TotalReceived):F1}%)");
Console.WriteLine($"  Crashes:        {results.CrashCount}");
Console.WriteLine($"  Misdecodes:     {results.Misdecodes.Count}");
Console.WriteLine($"  Slow (≥{slowMs}ms): {results.SlowPackets.Count}");
Console.WriteLine($"  Rate:           {results.TotalReceived / Math.Max(1, sw.Elapsed.TotalSeconds):F1} packets/sec");
Console.WriteLine($"  Server comments:{results.ServerComments:N0} (# lines — if >0, connection is working)");

Console.WriteLine();
Console.WriteLine("  Packet types:");
foreach (var (type, count) in results.TypeCounts.OrderByDescending(x => x.Value))
    Console.WriteLine($"    {count,8:N0}  {type}");

if (results.Crashes.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"  ⚠️  CRASHES ({results.Crashes.Count}):");
    foreach (var f in results.Crashes.Take(20))
    {
        Console.WriteLine($"    [{f.ElapsedMs}ms] {f.Finding}");
        Console.WriteLine($"           raw: {Truncate(f.RawLine, 120)}");
    }
    if (results.Crashes.Count > 20)
        Console.WriteLine($"    ... and {results.Crashes.Count - 20} more");
}

if (results.Misdecodes.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"  ⚠️  MISDECODES ({results.Misdecodes.Count}):");
    foreach (var f in results.Misdecodes.Take(20))
    {
        Console.WriteLine($"    {f.Finding}");
        Console.WriteLine($"           raw: {Truncate(f.RawLine, 120)}");
    }
    if (results.Misdecodes.Count > 20)
        Console.WriteLine($"    ... and {results.Misdecodes.Count - 20} more");
}

if (results.SlowPackets.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"  ⚠️  SLOW PACKETS ({results.SlowPackets.Count}, ≥{slowMs}ms):");
    foreach (var f in results.SlowPackets.OrderByDescending(x => x.ElapsedMs).Take(10))
        Console.WriteLine($"    [{f.ElapsedMs}ms] {Truncate(f.RawLine, 120)}");
}

if (results.CrashCount == 0 && results.Misdecodes.Count == 0)
{
    Console.WriteLine();
    Console.WriteLine("  ✅  No crashes or misdecodes found.");
}

Console.WriteLine();
return results.CrashCount > 0 || results.Misdecodes.Count > 0 ? 3 : 0;

// ── Helpers ───────────────────────────────────────────────────────────────────
static string GetArg(string[] args, string flag, string env, string def)
{
    var idx = Array.IndexOf(args, flag);
    if (idx >= 0 && idx + 1 < args.Length) return args[idx + 1];
    return Environment.GetEnvironmentVariable(env) ?? def;
}

static string Truncate(string s, int max) =>
    s.Length > max ? s[..max] + "…" : s;

// ── Data structures ───────────────────────────────────────────────────────────
sealed class FuzzResults
{
    public int  TotalReceived;
    public int  ValidCount;
    public int  InvalidCount;
    public int  CrashCount;
    public int  ServerComments;

    public ConcurrentBag<FuzzFinding>        Crashes     = [];
    public ConcurrentBag<FuzzFinding>        Misdecodes  = [];
    public ConcurrentBag<FuzzFinding>        SlowPackets = [];
    public ConcurrentDictionary<string, int> TypeCounts  = [];
}

record FuzzFinding(string RawLine, string Finding, long ElapsedMs);
