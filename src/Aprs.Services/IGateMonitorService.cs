using System.Text.RegularExpressions;
using Aprs.Core;

namespace Aprs.Services;

public sealed partial class IGateMonitorService : IIGateMonitorService
{
    private readonly IGateMonitorConfiguration configuration;
    private readonly List<IGateCandidatePacket> candidates = [];
    private readonly List<SeenAprsIsPacket> aprsIsSeenPackets = [];
    private DateTimeOffset? lastRfPacketUtc;
    private DateTimeOffset? lastAprsIsPacketUtc;
    private string? lastReason;

    public IGateMonitorService(IGateMonitorConfiguration? configuration = null)
    {
        this.configuration = configuration ?? IGateMonitorConfiguration.Default;
    }

    public IGateCandidatePacket AcceptRfPacket(
        AprsPacket packet,
        string sourcePort,
        DateTimeOffset? receivedAtUtc = null)
    {
        return AcceptPacket(packet, AprsPacketSource.Rf, sourcePort, receivedAtUtc);
    }

    public IGateCandidatePacket AcceptPacket(
        AprsPacket packet,
        AprsPacketSource packetSource,
        string sourcePort,
        DateTimeOffset? receivedAtUtc = null)
    {
        var now = receivedAtUtc ?? packet.ReceivedAtUtc;
        lastRfPacketUtc = now;
        PruneAprsIsSeen(now);

        var warnings = new List<string>();
        var errors = packet.ValidationErrors.ToList();
        var isRfSource = IsRfSource(sourcePort, packetSource);
        var packetType = FormatPacketType(packet);
        var qConstruct = GetQConstruct(packet);
        var appearsFromAprsIs = AppearsToHaveComeFromAprsIs(packet.Path, qConstruct);

        IGateCandidateState candidateState;
        IGateDuplicateState duplicateState = IGateDuplicateState.NotSeen;
        var wasAlsoSeenOnAprsIs = false;
        string reason;

        if (!configuration.MonitorEnabled)
        {
            candidateState = IGateCandidateState.Rejected;
            reason = "iGate monitor is disabled.";
        }
        else if (!configuration.RfToAprsIsCandidateDetectionEnabled)
        {
            candidateState = IGateCandidateState.Rejected;
            reason = "RF-to-APRS-IS candidate detection is disabled.";
        }
        else if (!isRfSource)
        {
            candidateState = IGateCandidateState.Rejected;
            reason = "Packet source is not an RF monitor port.";
        }
        else if (configuration.LocalRfPortsToMonitor.Count > 0
            && !configuration.LocalRfPortsToMonitor.Contains(sourcePort, StringComparer.OrdinalIgnoreCase))
        {
            candidateState = IGateCandidateState.Rejected;
            reason = "RF source port is not configured for iGate monitoring.";
        }
        else if (!packet.IsValid)
        {
            candidateState = IGateCandidateState.Invalid;
            reason = "Packet failed APRS parser validation.";
        }
        else if (configuration.RequireValidSourceCallsign && !ValidSourceCallsignRegex().IsMatch(packet.SourceCallsign))
        {
            candidateState = IGateCandidateState.Invalid;
            reason = "Packet source callsign is invalid.";
            errors.Add(reason);
        }
        else if (configuration.RequireValidPositionMessageWeatherObjectPacket && !IsSupportedCandidatePacketType(packet))
        {
            candidateState = IGateCandidateState.Rejected;
            reason = "Packet type is not enabled for iGate candidate monitoring.";
        }
        else if (appearsFromAprsIs)
        {
            candidateState = IGateCandidateState.AlreadySeenOnAprsIs;
            duplicateState = IGateDuplicateState.SeenOnAprsIs;
            wasAlsoSeenOnAprsIs = true;
            reason = "Packet path/q construct suggests it already came from APRS-IS.";
            warnings.Add(reason);
        }
        else if (configuration.AprsIsDuplicateDetectionEnabled && TryFindAprsIsDuplicate(packet, now, out var duplicate))
        {
            candidateState = IGateCandidateState.Duplicate;
            duplicateState = IGateDuplicateState.DuplicateWithinWindow;
            wasAlsoSeenOnAprsIs = true;
            reason = $"Packet was also seen on APRS-IS via {duplicate!.SourceName}.";
            warnings.Add(reason);
        }
        else
        {
            candidateState = IGateCandidateState.Candidate;
            reason = "This packet would be a candidate for gating later.";
        }

        var candidate = new IGateCandidatePacket(
            packet.RawLine,
            packetType,
            packet.SourceCallsign,
            packet.Destination,
            packet.Path,
            qConstruct,
            now,
            sourcePort,
            packetSource,
            isRfSource,
            wasAlsoSeenOnAprsIs,
            duplicateState,
            candidateState,
            reason,
            warnings,
            errors);

        candidates.Add(candidate);
        lastReason = reason;
        EnforceCandidateLimit();
        return candidate;
    }

    public void AcceptAprsIsPacket(
        AprsPacket packet,
        string sourceName,
        DateTimeOffset? receivedAtUtc = null)
    {
        var now = receivedAtUtc ?? packet.ReceivedAtUtc;
        lastAprsIsPacketUtc = now;
        PruneAprsIsSeen(now);

        if (!configuration.MonitorEnabled || !configuration.AprsIsDuplicateDetectionEnabled)
        {
            return;
        }

        aprsIsSeenPackets.Add(new SeenAprsIsPacket(packet.RawLine, BuildFingerprint(packet), sourceName, now));
        MarkMatchingCandidatesAsSeenOnAprsIs(packet, sourceName, now);
    }

    public IReadOnlyList<IGateCandidatePacket> GetRecentCandidates()
    {
        return candidates.ToArray();
    }

    public IGateMonitorSummary GetSummary()
    {
        return new IGateMonitorSummary(
            configuration.MonitorEnabled,
            candidates.Count,
            candidates.Count(candidate => candidate.CandidateState == IGateCandidateState.Candidate),
            candidates.Count(candidate => candidate.CandidateState == IGateCandidateState.Rejected),
            candidates.Count(candidate => candidate.CandidateState == IGateCandidateState.Duplicate),
            candidates.Count(candidate => candidate.CandidateState == IGateCandidateState.AlreadySeenOnAprsIs),
            candidates.Count(candidate => candidate.CandidateState == IGateCandidateState.Invalid),
            candidates.Count(candidate => candidate.CandidateState == IGateCandidateState.Expired),
            lastRfPacketUtc,
            lastAprsIsPacketUtc,
            lastReason);
    }

    public void ClearCandidates()
    {
        candidates.Clear();
        lastReason = null;
    }

    public void ExpireCandidates(DateTimeOffset now)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (candidate.CandidateState != IGateCandidateState.Expired
                && now - candidate.ReceivedTimestampUtc > configuration.CandidateRetentionTime)
            {
                candidates[index] = candidate with
                {
                    CandidateState = IGateCandidateState.Expired,
                    DuplicateState = candidate.DuplicateState == IGateDuplicateState.NotSeen
                        ? IGateDuplicateState.Expired
                        : candidate.DuplicateState,
                    Reason = "iGate monitor candidate expired."
                };
            }
        }

        PruneAprsIsSeen(now);
    }

    private void MarkMatchingCandidatesAsSeenOnAprsIs(AprsPacket aprsIsPacket, string sourceName, DateTimeOffset now)
    {
        var fingerprint = BuildFingerprint(aprsIsPacket);
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (now - candidate.ReceivedTimestampUtc > configuration.DuplicateDetectionWindow)
            {
                continue;
            }

            if (!string.Equals(candidate.RawPacket, aprsIsPacket.RawLine, StringComparison.Ordinal)
                && !string.Equals(BuildCandidateFingerprint(candidate), fingerprint, StringComparison.Ordinal))
            {
                continue;
            }

            candidates[index] = candidate with
            {
                WasAlsoSeenOnAprsIs = true,
                DuplicateState = IGateDuplicateState.DuplicateWithinWindow,
                CandidateState = candidate.CandidateState == IGateCandidateState.Candidate
                    ? IGateCandidateState.AlreadySeenOnAprsIs
                    : candidate.CandidateState,
                Reason = $"Packet was later seen on APRS-IS via {sourceName}.",
                ValidationWarnings = candidate.ValidationWarnings.Concat([$"Packet was later seen on APRS-IS via {sourceName}."]).Distinct().ToArray()
            };
            lastReason = candidates[index].Reason;
        }
    }

    private bool TryFindAprsIsDuplicate(AprsPacket packet, DateTimeOffset now, out SeenAprsIsPacket? duplicate)
    {
        var fingerprint = BuildFingerprint(packet);
        duplicate = aprsIsSeenPackets.FirstOrDefault(seen =>
            now - seen.TimestampUtc <= configuration.DuplicateDetectionWindow
            && (string.Equals(seen.RawPacket, packet.RawLine, StringComparison.Ordinal)
                || string.Equals(seen.Fingerprint, fingerprint, StringComparison.Ordinal)));

        return duplicate is not null;
    }

    private void PruneAprsIsSeen(DateTimeOffset now)
    {
        aprsIsSeenPackets.RemoveAll(seen => now - seen.TimestampUtc > configuration.DuplicateDetectionWindow);
    }

    private void EnforceCandidateLimit()
    {
        var maximum = Math.Max(1, configuration.MaximumCandidateListSize);
        while (candidates.Count > maximum)
        {
            candidates.RemoveAt(0);
        }
    }

    private static bool IsRfSource(string sourcePort, AprsPacketSource packetSource)
    {
        if (packetSource is AprsPacketSource.Rf or AprsPacketSource.TcpKiss or AprsPacketSource.SerialKiss or AprsPacketSource.Direwolf or AprsPacketSource.Agwpe)
        {
            return true;
        }

        return sourcePort.Contains("kiss", StringComparison.OrdinalIgnoreCase)
            || sourcePort.Contains("rf", StringComparison.OrdinalIgnoreCase)
            || sourcePort.Contains("direwolf", StringComparison.OrdinalIgnoreCase)
            || sourcePort.Contains("agwpe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool AppearsToHaveComeFromAprsIs(IReadOnlyList<string> path, string? qConstruct)
    {
        return qConstruct is not null
            || path.Any(component =>
                component.StartsWith("TCPIP", StringComparison.OrdinalIgnoreCase)
                || component.StartsWith("TCPXX", StringComparison.OrdinalIgnoreCase)
                || component.StartsWith("q", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSupportedCandidatePacketType(AprsPacket packet)
    {
        return packet is PositionAprsPacket or MessageAprsPacket or QueryAprsPacket or WeatherAprsPacket or ObjectAprsPacket or ItemAprsPacket;
    }

    private static string FormatPacketType(AprsPacket packet)
    {
        var typeName = packet.GetType().Name;
        return typeName.EndsWith("AprsPacket", StringComparison.Ordinal)
            ? typeName[..^"AprsPacket".Length]
            : typeName;
    }

    private static string BuildFingerprint(AprsPacket packet)
    {
        return $"{packet.SourceCallsign}>{packet.Destination}:{packet.Information}";
    }

    private static string BuildCandidateFingerprint(IGateCandidatePacket candidate)
    {
        var separatorIndex = candidate.RawPacket.IndexOf(':');
        var information = separatorIndex >= 0 ? candidate.RawPacket[(separatorIndex + 1)..] : string.Empty;
        return $"{candidate.SourceCallsign}>{candidate.Destination}:{information}";
    }

    private static string? GetQConstruct(AprsPacket packet)
    {
        return packet switch
        {
            RawAprsPacket raw => raw.QConstruct,
            PositionAprsPacket position => position.QConstruct,
            StatusAprsPacket status => status.QConstruct,
            TelemetryAprsPacket telemetry => telemetry.QConstruct,
            TelemetryMetadataAprsPacket telemetryMetadata => telemetryMetadata.QConstruct,
            CapabilityAprsPacket capability => capability.QConstruct,
            UnknownAprsPacket unknown => unknown.QConstruct,
            MessageAprsPacket message => message.QConstruct,
            QueryAprsPacket query => query.QConstruct,
            ObjectAprsPacket objectPacket => objectPacket.QConstruct,
            ItemAprsPacket item => item.QConstruct,
            WeatherAprsPacket weather => weather.QConstruct,
            _ => null
        };
    }

    [GeneratedRegex("^[A-Z0-9]{1,9}$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ValidSourceCallsignRegex();

    private sealed record SeenAprsIsPacket(
        string RawPacket,
        string Fingerprint,
        string SourceName,
        DateTimeOffset TimestampUtc);
}
