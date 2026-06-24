using System.Text.RegularExpressions;
using Aprs.Transport;

namespace Aprs.Services;

public sealed partial class IGateService : IIGateService
{
    private readonly IAprsIsClient aprsIsClient;
    private readonly IBeaconSchedulerClock clock;
    private readonly List<IGateGatingDecisionRecord> decisions = [];
    private readonly IGateConfiguration configuration;

    public IGateService(
        IAprsIsClient aprsIsClient,
        IGateConfiguration? configuration = null,
        IBeaconSchedulerClock? clock = null)
    {
        this.aprsIsClient = aprsIsClient;
        this.clock = clock ?? new SystemBeaconSchedulerClock();
        this.configuration = StampDefaults(configuration ?? IGateConfiguration.Default, DateTimeOffset.UtcNow);
    }

    public async Task<IGateGatingDecisionRecord> EvaluateAndGateAsync(
        IGateCandidatePacket candidate,
        CancellationToken cancellationToken = default)
    {
        var blocked = EvaluateBlocked(candidate);
        if (blocked is not null)
        {
            return LogDecision(candidate, blocked.Value.Decision, blocked.Value.Reason, false, null, blocked.Value.Errors);
        }

        var transmitResult = await aprsIsClient.SendRawPacketAsync(
            candidate.RawPacket,
            configuration.RequireExplicitConfirmationBeforeEnabling,
            cancellationToken).ConfigureAwait(false);

        if (!transmitResult.IsSuccess)
        {
            return LogDecision(
                candidate,
                IGateDecision.Error,
                transmitResult.FailureReason ?? "APRS-IS transmit service rejected the iGate packet.",
                true,
                transmitResult,
                [transmitResult.FailureReason ?? "APRS-IS transmit failed."]);
        }

        return LogDecision(candidate, IGateDecision.Allowed, "RF packet gated to APRS-IS by safe iGate service.", true, transmitResult, []);
    }

    public IReadOnlyList<IGateGatingDecisionRecord> GetRecentDecisions()
    {
        return decisions.ToArray();
    }

    public IGateStatusSummary GetStatusSummary()
    {
        var last = decisions.LastOrDefault();
        return new IGateStatusSummary(
            configuration.IGateEnabled,
            configuration.RfToAprsIsGatingEnabled,
            configuration.AprsIsTransmitEnabled,
            configuration.AprsIsTransmitRequired,
            decisions.Count(decision => decision.Decision == IGateDecision.Allowed),
            decisions.Count(decision => decision.Decision is IGateDecision.Blocked or IGateDecision.TransmitDisabled or IGateDecision.AprsIsDisconnected),
            decisions.Count(decision => decision.Decision == IGateDecision.Duplicate),
            decisions.Count(decision => decision.Decision == IGateDecision.RateLimited),
            decisions.Count(decision => decision.Decision == IGateDecision.Invalid),
            decisions.Count(decision => decision.Decision == IGateDecision.Error),
            decisions.LastOrDefault(decision => decision.Decision == IGateDecision.Allowed)?.RawPacket,
            decisions.LastOrDefault(decision => decision.Decision != IGateDecision.Allowed)?.Reason,
            last?.ReceivedTimestampUtc);
    }

    public void ClearDecisionHistory()
    {
        decisions.Clear();
    }

    private (IGateDecision Decision, string Reason, IReadOnlyList<string> Errors)? EvaluateBlocked(IGateCandidatePacket candidate)
    {
        if (!configuration.IGateEnabled)
        {
            return (IGateDecision.TransmitDisabled, "iGate is disabled.", ["iGate is disabled."]);
        }

        if (!configuration.RfToAprsIsGatingEnabled)
        {
            return (IGateDecision.TransmitDisabled, "RF-to-APRS-IS gating is disabled.", ["RF-to-APRS-IS gating is disabled."]);
        }

        if (!configuration.AprsIsTransmitEnabled)
        {
            return (IGateDecision.TransmitDisabled, "APRS-IS transmit is disabled for iGate.", ["APRS-IS transmit is disabled for iGate."]);
        }

        if (configuration.AprsIsTransmitRequired && aprsIsClient.State != AprsIsConnectionState.Connected)
        {
            return (IGateDecision.AprsIsDisconnected, "APRS-IS client is disconnected.", ["APRS-IS client is disconnected."]);
        }

        if (!candidate.IsRfSource)
        {
            return (IGateDecision.Blocked, "Packet did not come from an RF source.", ["Packet did not come from an RF source."]);
        }

        if (configuration.AllowedRfSourcePorts.Count > 0
            && !configuration.AllowedRfSourcePorts.Contains(candidate.ReceivedSourcePort, StringComparer.OrdinalIgnoreCase))
        {
            return (IGateDecision.Blocked, "RF source port is not allowed for iGate.", ["RF source port is not allowed for iGate."]);
        }

        if (configuration.RequireValidPacket && candidate.ValidationErrors.Count > 0)
        {
            return (IGateDecision.Invalid, "Candidate packet is malformed.", candidate.ValidationErrors);
        }

        if (configuration.RequireValidCallsign && !ValidCallsignRegex().IsMatch(candidate.SourceCallsign))
        {
            return (IGateDecision.Invalid, "Candidate source callsign is invalid.", ["Candidate source callsign is invalid."]);
        }

        if (candidate.CandidateState is IGateCandidateState.Invalid)
        {
            return (IGateDecision.Invalid, candidate.Reason, candidate.ValidationErrors);
        }

        if (configuration.DuplicateSuppressionEnabled
            && (candidate.CandidateState is IGateCandidateState.Duplicate or IGateCandidateState.AlreadySeenOnAprsIs
                || candidate.WasAlsoSeenOnAprsIs
                || IsRepeatedRawPacket(candidate)))
        {
            return (IGateDecision.Duplicate, "Candidate is a duplicate or already seen on APRS-IS.", candidate.ValidationWarnings);
        }

        if (!IsPacketTypeAllowed(candidate))
        {
            return (IGateDecision.Blocked, "Candidate packet type is not enabled for iGate.", ["Candidate packet type is not enabled for iGate."]);
        }

        if (!configuration.GateThirdPartyPackets && candidate.RawPacket.Contains(':') && candidate.RawPacket.Split(':', 2)[1].StartsWith('}'))
        {
            return (IGateDecision.Blocked, "Third-party packet gating is disabled.", ["Third-party packet gating is disabled."]);
        }

        if (MatchesAnyPattern(candidate.Path, configuration.BlockedPathPatterns))
        {
            return (IGateDecision.Blocked, "Candidate path matches a blocked path pattern.", ["Candidate path matches a blocked path pattern."]);
        }

        if (configuration.AllowedPathPatterns.Count > 0 && !MatchesAnyPattern(candidate.Path, configuration.AllowedPathPatterns))
        {
            return (IGateDecision.Blocked, "Candidate path does not match an allowed path pattern.", ["Candidate path does not match an allowed path pattern."]);
        }

        if (IsRateLimited(candidate))
        {
            return (IGateDecision.RateLimited, "iGate rate limit would be exceeded.", ["iGate rate limit would be exceeded."]);
        }

        return null;
    }

    private bool IsPacketTypeAllowed(IGateCandidatePacket candidate)
    {
        return candidate.ParsedPacketType switch
        {
            "Position" => configuration.GatePositionPackets,
            "Weather" => configuration.GateWeatherPackets,
            "Object" or "Item" => configuration.GateObjectItemPackets,
            "Message" or "Query" => configuration.GateMessages,
            "Telemetry" or "TelemetryMetadata" => configuration.GateTelemetry,
            _ => false
        };
    }

    private bool IsRateLimited(IGateCandidatePacket candidate)
    {
        var cutoff = candidate.ReceivedTimestampUtc.AddMinutes(-1);
        var recentAllowed = decisions.Where(decision =>
            decision.Decision == IGateDecision.Allowed
            && decision.ReceivedTimestampUtc >= cutoff).ToArray();

        if (recentAllowed.Length >= configuration.MaximumGateRatePerMinute)
        {
            return true;
        }

        return recentAllowed.Count(decision =>
            string.Equals(decision.SourceCallsign, candidate.SourceCallsign, StringComparison.OrdinalIgnoreCase))
            >= configuration.MaximumGateRatePerStationPerMinute;
    }

    private bool IsRepeatedRawPacket(IGateCandidatePacket candidate)
    {
        var cutoff = candidate.ReceivedTimestampUtc - configuration.DuplicateDetectionWindow;
        return decisions.Any(decision =>
            decision.ReceivedTimestampUtc >= cutoff
            && string.Equals(decision.RawPacket, candidate.RawPacket, StringComparison.Ordinal)
            && decision.Decision == IGateDecision.Allowed);
    }

    private IGateGatingDecisionRecord LogDecision(
        IGateCandidatePacket candidate,
        IGateDecision decision,
        string reason,
        bool transmitAttempted,
        AprsIsTransmitResult? transmitResult,
        IReadOnlyList<string> errors)
    {
        var record = new IGateGatingDecisionRecord(
            candidate.RawPacket,
            candidate.ParsedPacketType,
            candidate.SourceCallsign,
            candidate.Destination,
            candidate.Path,
            candidate.ReceivedTimestampUtc,
            candidate.ReceivedSourcePort,
            candidate.CandidateState,
            decision,
            reason,
            candidate.ValidationWarnings,
            candidate.ValidationErrors.Concat(errors).Distinct().ToArray(),
            transmitAttempted,
            transmitResult);
        decisions.Add(record);
        return record;
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

    private static IGateConfiguration StampDefaults(IGateConfiguration configuration, DateTimeOffset now)
    {
        return configuration with
        {
            CreatedTimestampUtc = configuration.CreatedTimestampUtc == DateTimeOffset.MinValue ? now : configuration.CreatedTimestampUtc,
            UpdatedTimestampUtc = configuration.UpdatedTimestampUtc == DateTimeOffset.MinValue ? now : configuration.UpdatedTimestampUtc
        };
    }

    [GeneratedRegex("^[A-Z0-9]{1,9}$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ValidCallsignRegex();
}
