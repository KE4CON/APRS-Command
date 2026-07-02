using Aprs.Core;
using System.Collections.Concurrent;

namespace Aprs.Services;

/// <summary>
/// Accumulates live packet statistics for the current session.
/// Thread-safe — updated from the packet-receive thread, read from the UI thread.
/// </summary>
public sealed class PacketStatisticsService
{
    // ── Session totals ──────────────────────────────────────────────────────
    private int totalPackets;
    private int positionPackets;
    private int messagePackets;
    private int weatherPackets;
    private int objectPackets;
    private int statusPackets;
    private int otherPackets;
    private int invalidPackets;

    // ── Station tracking ────────────────────────────────────────────────────
    private readonly ConcurrentDictionary<string, int> packetsByStation = new();

    // ── Hourly buckets (last 24 hours, one int per hour) ───────────────────
    private readonly int[] hourlyBuckets = new int[24];
    private int currentHour = DateTimeOffset.UtcNow.Hour;

    // ── Session metadata ────────────────────────────────────────────────────
    public DateTimeOffset SessionStartUtc { get; } = DateTimeOffset.UtcNow;

    // ── Public read properties ──────────────────────────────────────────────
    public int TotalPackets        => totalPackets;
    public int PositionPackets     => positionPackets;
    public int MessagePackets      => messagePackets;
    public int WeatherPackets      => weatherPackets;
    public int ObjectPackets       => objectPackets;
    public int StatusPackets       => statusPackets;
    public int OtherPackets        => otherPackets;
    public int InvalidPackets      => invalidPackets;
    public int UniqueStations      => packetsByStation.Count;

    public TimeSpan SessionDuration => DateTimeOffset.UtcNow - SessionStartUtc;

    public double PacketsPerHour
    {
        get
        {
            var hours = SessionDuration.TotalHours;
            return hours < 0.01 ? 0 : totalPackets / hours;
        }
    }

    public double PacketsPerMinute
    {
        get
        {
            var minutes = SessionDuration.TotalMinutes;
            return minutes < 0.01 ? 0 : totalPackets / minutes;
        }
    }

    /// <summary>Returns the top N stations by packet count.</summary>
    public IReadOnlyList<(string Callsign, int Count)> GetTopStations(int n = 10)
        => packetsByStation
            .OrderByDescending(kv => kv.Value)
            .Take(n)
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

    /// <summary>Returns the 24 hourly packet counts, index 0 = oldest hour.</summary>
    public IReadOnlyList<int> GetHourlyBuckets()
    {
        var now = DateTimeOffset.UtcNow;
        AdvanceHourIfNeeded(now);

        // Return buckets rotated so index 0 = (currentHour+1) % 24 (oldest)
        var result = new int[24];
        for (int i = 0; i < 24; i++)
            result[i] = hourlyBuckets[(currentHour + 1 + i) % 24];
        return result;
    }

    /// <summary>
    /// Records one parsed packet. Call this from the packet-received event handler.
    /// </summary>
    public void RecordPacket(AprsPacket packet)
    {
        var now = DateTimeOffset.UtcNow;
        AdvanceHourIfNeeded(now);

        Interlocked.Increment(ref totalPackets);
        Interlocked.Increment(ref hourlyBuckets[currentHour]);

        if (!packet.IsValid)
        {
            Interlocked.Increment(ref invalidPackets);
            return;
        }

        // Count by type
        switch (packet)
        {
            case PositionAprsPacket:    Interlocked.Increment(ref positionPackets); break;
            case MessageAprsPacket:     Interlocked.Increment(ref messagePackets);  break;
            case WeatherAprsPacket:     Interlocked.Increment(ref weatherPackets);  break;
            case ObjectAprsPacket:      Interlocked.Increment(ref objectPackets);   break;
            case StatusAprsPacket:      Interlocked.Increment(ref statusPackets);   break;
            default:                    Interlocked.Increment(ref otherPackets);    break;
        }

        // Track per-station counts
        var callsign = string.IsNullOrWhiteSpace(packet.SourceCallsign)
            ? "UNKNOWN"
            : packet.SourceCallsign.ToUpperInvariant();
        packetsByStation.AddOrUpdate(callsign, 1, (_, c) => c + 1);
    }

    /// <summary>Resets all statistics for a new session.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref totalPackets, 0);
        Interlocked.Exchange(ref positionPackets, 0);
        Interlocked.Exchange(ref messagePackets, 0);
        Interlocked.Exchange(ref weatherPackets, 0);
        Interlocked.Exchange(ref objectPackets, 0);
        Interlocked.Exchange(ref statusPackets, 0);
        Interlocked.Exchange(ref otherPackets, 0);
        Interlocked.Exchange(ref invalidPackets, 0);
        packetsByStation.Clear();
        Array.Clear(hourlyBuckets, 0, 24);
        currentHour = DateTimeOffset.UtcNow.Hour;
    }

    private void AdvanceHourIfNeeded(DateTimeOffset now)
    {
        var hour = now.Hour;
        if (hour != currentHour)
        {
            // Zero out any skipped hours
            var steps = (hour - currentHour + 24) % 24;
            for (int i = 1; i <= steps; i++)
                Interlocked.Exchange(ref hourlyBuckets[(currentHour + i) % 24], 0);
            currentHour = hour;
        }
    }
}
