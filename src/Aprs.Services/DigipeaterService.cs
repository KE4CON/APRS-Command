using System.Text.RegularExpressions;
using Aprs.Core;

namespace Aprs.Services;

public sealed partial class DigipeaterService : IDigipeaterService
{
    private readonly IAprsPortManager portManager;
    private readonly IRfBeaconTransmitClient rfTransmitClient;
    private readonly IBeaconSchedulerClock clock;
    private readonly List<DigipeaterDecisionRecord> decisions = [];
    private readonly DigipeaterConfiguration configuration;

    public DigipeaterService(
        IAprsPortManager portManager,
        IRfBeaconTransmitClient rfTransmitClient,
        DigipeaterConfiguration? configuration = null,
        IBeaconSchedulerClock? clock = null)
    {
        this.portManager = portManager;
        this.rfTransmitClient = rfTransmitClient;
        this.clock = clock ?? new SystemBeaconSchedulerClock();
        this.configuration = StampDefaults(configuration ?? DigipeaterConfiguration.Default, DateTimeOffset.UtcNow);
    }

    public async Task<DigipeaterDecisionRecord> EvaluateAndDigipeatAsync(
        AprsPacket packet,
        AprsPacketSource packetSource,
        string receivedRfPort,
        CancellationToken cancellationToken = default)
    {
        var pathResult = EvaluatePath(packet);
        var blocked = EvaluateBlocked(packet, packetSource, receivedRfPort, pathResult);
        if (blocked is not null)
        {
            return LogDecision(packet, receivedRfPort, pathResult, blocked.Value.Decision, blocked.Value.Reason, false, null, blocked.Value.Errors);
        }

        var modifiedPacket = pathResult.ModifiedPacket!;
        var transmitResult = await rfTransmitClient.SendBeaconAsync(modifiedPacket, cancellationToken).ConfigureAwait(false);
        if (!transmitResult.Transmitted)
        {
            return LogDecision(
                packet,
                receivedRfPort,
                pathResult,
                DigipeaterDecision.Error,
                transmitResult.Message ?? "RF transmit service rejected the digipeated packet.",
                true,
                transmitResult,
                [transmitResult.Message ?? "RF transmit failed."]);
        }

        if (!string.IsNullOrWhiteSpace(configuration.RfTransmitPort))
        {
            portManager.RecordPacketTransmitted(configuration.RfTransmitPort, clock.UtcNow);
        }

        return LogDecision(packet, receivedRfPort, pathResult, DigipeaterDecision.Allowed, "RF packet digipeated by safe digipeater service.", true, transmitResult, []);
    }

    public IReadOnlyList<DigipeaterDecisionRecord> GetRecentDecisions()
    {
        return decisions.ToArray();
    }

    public DigipeaterStatusSummary GetStatusSummary()
    {
        var last = decisions.LastOrDefault();
        return new DigipeaterStatusSummary(
            configuration.DigipeaterEnabled,
            configuration.RfTransmitEnabled,
            configuration.RfTransmitPort,
            configuration.DigipeaterCallsign,
            configuration.SupportedAliases,
            configuration.FillInDigipeaterMode,
            configuration.FullDigipeaterMode,
            decisions.Count(decision => decision.Decision == DigipeaterDecision.Allowed),
            decisions.Count(decision => decision.Decision is DigipeaterDecision.Blocked or DigipeaterDecision.TransmitDisabled or DigipeaterDecision.NoMatchingAlias),
            decisions.Count(decision => decision.Decision == DigipeaterDecision.Duplicate),
            decisions.Count(decision => decision.Decision == DigipeaterDecision.RateLimited),
            decisions.Count(decision => decision.Decision == DigipeaterDecision.Invalid),
            decisions.Count(decision => decision.Decision == DigipeaterDecision.Error),
            decisions.LastOrDefault(decision => decision.Decision == DigipeaterDecision.Allowed)?.ModifiedPacket,
            decisions.LastOrDefault(decision => decision.Decision != DigipeaterDecision.Allowed)?.Reason,
            last?.ReceivedTimestampUtc);
    }

    public void ClearDecisionHistory()
    {
        decisions.Clear();
    }

    private (DigipeaterDecision Decision, string Reason, IReadOnlyList<string> Errors)? EvaluateBlocked(
        AprsPacket packet,
        AprsPacketSource packetSource,
        string receivedRfPort,
        DigipeaterPathResult pathResult)
    {
        if (!configuration.DigipeaterEnabled)
        {
            return (DigipeaterDecision.TransmitDisabled, "Digipeater mode is disabled.", ["Digipeater mode is disabled."]);
        }

        if (!configuration.RfTransmitEnabled)
        {
            return (DigipeaterDecision.TransmitDisabled, "RF transmit is disabled for digipeater mode.", ["RF transmit is disabled for digipeater mode."]);
        }

        if (string.IsNullOrWhiteSpace(configuration.RfTransmitPort))
        {
            return (DigipeaterDecision.TransmitDisabled, "No RF transmit port is selected.", ["No RF transmit port is selected."]);
        }

        var portSafety = portManager.CheckTransmitSafety(configuration.RfTransmitPort, globalTransmitSafetyEnabled: configuration.RfTransmitEnabled);
        if (!portSafety.IsSafe)
        {
            return (DigipeaterDecision.TransmitDisabled, portSafety.FailureReason ?? "RF transmit port is not safe.", [portSafety.FailureReason ?? "RF transmit port is not safe."]);
        }

        if (!IsRfSource(packetSource))
        {
            return (DigipeaterDecision.Blocked, "Packet did not come from an RF source.", ["Packet did not come from an RF source."]);
        }

        if (configuration.AllowedRfReceivePorts.Count > 0
            && !configuration.AllowedRfReceivePorts.Contains(receivedRfPort, StringComparer.OrdinalIgnoreCase))
        {
            return (DigipeaterDecision.Blocked, "RF receive port is not allowed for digipeater mode.", ["RF receive port is not allowed for digipeater mode."]);
        }

        if (!packet.IsValid || packet.ValidationErrors.Count > 0)
        {
            return (DigipeaterDecision.Invalid, "Packet is malformed.", packet.ValidationErrors);
        }

        if (configuration.RequireValidSourceCallsign && !ValidCallsignRegex().IsMatch(packet.SourceCallsign))
        {
            return (DigipeaterDecision.Invalid, "Packet source callsign is invalid.", ["Packet source callsign is invalid."]);
        }

        if (configuration.RequireValidPath && packet.Path.Count == 0)
        {
            return (DigipeaterDecision.Invalid, "Packet path is required for digipeater mode.", ["Packet path is required for digipeater mode."]);
        }

        if (configuration.BlockedCallsigns.Contains(packet.SourceCallsign, StringComparer.OrdinalIgnoreCase))
        {
            return (DigipeaterDecision.Blocked, "Packet source callsign is blocked.", ["Packet source callsign is blocked."]);
        }

        if (MatchesAnyPattern(packet.Path, configuration.BlockedPathPatterns))
        {
            return (DigipeaterDecision.Blocked, "Packet path matches a blocked path pattern.", ["Packet path matches a blocked path pattern."]);
        }

        if (configuration.AllowedPathPatterns.Count > 0 && !MatchesAnyPattern(packet.Path, configuration.AllowedPathPatterns))
        {
            return (DigipeaterDecision.Blocked, "Packet path does not match an allowed path pattern.", ["Packet path does not match an allowed path pattern."]);
        }

        if (!pathResult.Matched)
        {
            return (DigipeaterDecision.NoMatchingAlias, pathResult.Reason, pathResult.ValidationErrors);
        }

        if (configuration.DuplicateSuppressionEnabled && IsDuplicate(packet))
        {
            return (DigipeaterDecision.Duplicate, "Packet was already digipeated within the duplicate window.", ["Packet was already digipeated within the duplicate window."]);
        }

        if (IsRateLimited(packet))
        {
            return (DigipeaterDecision.RateLimited, "Digipeater rate limit would be exceeded.", ["Digipeater rate limit would be exceeded."]);
        }

        return null;
    }

    private DigipeaterPathResult EvaluatePath(AprsPacket packet)
    {
        var warnings = new List<string>();
        var errors = new List<string>();
        if (packet.Path.Count == 0)
        {
            errors.Add("Packet does not contain a digipeater path.");
            return new DigipeaterPathResult(false, packet.Path, null, "Packet does not request a digipeater path.", warnings, errors);
        }

        var aliases = BuildAliasSet();
        var modifiedPath = packet.Path.ToList();
        for (var index = 0; index < modifiedPath.Count; index++)
        {
            var component = modifiedPath[index];
            if (IsUsed(component))
            {
                continue;
            }

            var clean = StripUsedMarker(component);
            if (aliases.Contains(clean))
            {
                modifiedPath[index] = MarkUsed(clean);
                return PathMatched(packet, modifiedPath, $"Path component {clean} matched configured digipeater alias.");
            }

            var wide = WideAliasRegex().Match(clean);
            if (!wide.Success || !IsSupportedWideBase(clean, wide))
            {
                continue;
            }

            var total = int.Parse(wide.Groups["total"].Value);
            var remaining = int.Parse(wide.Groups["remaining"].Value);
            if (remaining <= 0)
            {
                continue;
            }

            var digi = MarkUsed(NormalizeCallsign(configuration.DigipeaterCallsign));
            if (remaining == 1)
            {
                modifiedPath[index] = digi;
                return PathMatched(packet, modifiedPath, $"Path component {clean} was consumed by this digipeater.");
            }

            modifiedPath[index] = digi;
            modifiedPath.Insert(index + 1, $"WIDE{total}-{remaining - 1}");
            warnings.Add($"Path component {clean} was decremented to WIDE{total}-{remaining - 1}.");
            return PathMatched(packet, modifiedPath, $"Path component {clean} was decremented by this digipeater.", warnings);
        }

        errors.Add("No configured digipeater callsign or alias was requested.");
        return new DigipeaterPathResult(false, packet.Path, null, "Packet does not request this digipeater or a configured alias.", warnings, errors);
    }

    private DigipeaterPathResult PathMatched(AprsPacket packet, IReadOnlyList<string> modifiedPath, string reason, IReadOnlyList<string>? warnings = null)
    {
        var modifiedPacket = BuildPacketWithPath(packet, modifiedPath);
        return new DigipeaterPathResult(true, modifiedPath, modifiedPacket, reason, warnings ?? [], []);
    }

    private HashSet<string> BuildAliasSet()
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(configuration.DigipeaterCallsign))
        {
            aliases.Add(NormalizeCallsign(configuration.DigipeaterCallsign));
        }

        foreach (var alias in configuration.SupportedAliases)
        {
            if (!string.IsNullOrWhiteSpace(alias))
            {
                aliases.Add(StripUsedMarker(alias.Trim()));
            }
        }

        return aliases;
    }

    private bool IsSupportedWideBase(string cleanComponent, Match wide)
    {
        if (configuration.FullDigipeaterMode)
        {
            return true;
        }

        if (configuration.FillInDigipeaterMode && cleanComponent.Equals("WIDE1-1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return configuration.SupportedAliases.Any(alias =>
            string.Equals(StripUsedMarker(alias), cleanComponent, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsDuplicate(AprsPacket packet)
    {
        var fingerprint = BuildFingerprint(packet);
        var cutoff = packet.ReceivedAtUtc - configuration.DuplicateDetectionWindow;
        return decisions.Any(decision =>
            decision.Decision == DigipeaterDecision.Allowed
            && decision.ReceivedTimestampUtc >= cutoff
            && string.Equals(BuildFingerprint(decision.SourceCallsign, decision.Destination, decision.OriginalPath, ExtractInformation(decision.RawPacket)), fingerprint, StringComparison.Ordinal));
    }

    private bool IsRateLimited(AprsPacket packet)
    {
        var cutoff = packet.ReceivedAtUtc.AddMinutes(-1);
        var recentAllowed = decisions.Where(decision =>
            decision.Decision == DigipeaterDecision.Allowed
            && decision.ReceivedTimestampUtc >= cutoff).ToArray();

        if (recentAllowed.Length >= configuration.MaximumDigipeatsPerMinute)
        {
            return true;
        }

        return recentAllowed.Count(decision =>
            string.Equals(decision.SourceCallsign, packet.SourceCallsign, StringComparison.OrdinalIgnoreCase))
            >= configuration.MaximumDigipeatsPerStationPerMinute;
    }

    private DigipeaterDecisionRecord LogDecision(
        AprsPacket packet,
        string receivedRfPort,
        DigipeaterPathResult pathResult,
        DigipeaterDecision decision,
        string reason,
        bool transmitAttempted,
        BeaconNowResult? transmitResult,
        IReadOnlyList<string> errors)
    {
        var record = new DigipeaterDecisionRecord(
            packet.RawLine,
            GetPacketType(packet),
            packet.SourceCallsign,
            packet.Destination,
            packet.Path,
            pathResult.ModifiedPath,
            pathResult.ModifiedPacket,
            packet.ReceivedAtUtc,
            receivedRfPort,
            configuration.RfTransmitPort,
            decision,
            reason,
            pathResult.ValidationWarnings,
            packet.ValidationErrors.Concat(pathResult.ValidationErrors).Concat(errors).Distinct().ToArray(),
            transmitAttempted,
            transmitResult);
        decisions.Add(record);
        return record;
    }

    private static string BuildPacketWithPath(AprsPacket packet, IReadOnlyList<string> modifiedPath)
    {
        var source = packet.RawLine.Split('>', 2)[0];
        var pathText = modifiedPath.Count > 0 ? "," + string.Join(",", modifiedPath) : string.Empty;
        return $"{source}>{packet.Destination}{pathText}:{packet.Information}";
    }

    private static string BuildFingerprint(AprsPacket packet)
    {
        return BuildFingerprint(packet.SourceCallsign, packet.Destination, packet.Path, packet.Information);
    }

    private static string BuildFingerprint(string source, string destination, IReadOnlyList<string> path, string information)
    {
        return string.Join("|", source.ToUpperInvariant(), destination.ToUpperInvariant(), string.Join(",", path).ToUpperInvariant(), information);
    }

    private static string ExtractInformation(string rawPacket)
    {
        var separator = rawPacket.IndexOf(':', StringComparison.Ordinal);
        return separator >= 0 && separator + 1 < rawPacket.Length ? rawPacket[(separator + 1)..] : string.Empty;
    }

    private static bool IsRfSource(AprsPacketSource packetSource)
    {
        return packetSource is AprsPacketSource.Rf or AprsPacketSource.TcpKiss or AprsPacketSource.SerialKiss or AprsPacketSource.Direwolf or AprsPacketSource.Agwpe;
    }

    private static bool IsUsed(string pathComponent)
    {
        return pathComponent.Trim().EndsWith('*');
    }

    private static string StripUsedMarker(string pathComponent)
    {
        return pathComponent.Trim().TrimEnd('*');
    }

    private static string MarkUsed(string pathComponent)
    {
        return StripUsedMarker(pathComponent) + "*";
    }

    private static string NormalizeCallsign(string callsign)
    {
        return StripUsedMarker(callsign).ToUpperInvariant();
    }

    private static bool MatchesAnyPattern(IReadOnlyList<string> path, IReadOnlyList<string> patterns)
    {
        return patterns.Count > 0
            && path.Any(component => patterns.Any(pattern => IsPatternMatch(component, pattern)));
    }

    private static bool IsPatternMatch(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var regex = "^" + Regex.Escape(pattern.Trim()).Replace("\\*", ".*", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(value, regex, RegexOptions.IgnoreCase);
    }

    private static string GetPacketType(AprsPacket packet)
    {
        var typeName = packet.GetType().Name;
        return typeName.EndsWith("AprsPacket", StringComparison.Ordinal)
            ? typeName[..^"AprsPacket".Length]
            : typeName;
    }

    private static DigipeaterConfiguration StampDefaults(DigipeaterConfiguration configuration, DateTimeOffset now)
    {
        return configuration with
        {
            CreatedTimestampUtc = configuration.CreatedTimestampUtc == DateTimeOffset.MinValue ? now : configuration.CreatedTimestampUtc,
            UpdatedTimestampUtc = configuration.UpdatedTimestampUtc == DateTimeOffset.MinValue ? now : configuration.UpdatedTimestampUtc
        };
    }

    [GeneratedRegex("^[A-Z0-9]{1,9}$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ValidCallsignRegex();

    [GeneratedRegex("^WIDE(?<total>[1-7])-(?<remaining>[1-7])$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex WideAliasRegex();
}
