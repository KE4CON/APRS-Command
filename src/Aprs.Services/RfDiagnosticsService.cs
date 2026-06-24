using Aprs.Core;

namespace Aprs.Services;

public sealed class RfDiagnosticsService : IRfDiagnosticsService
{
    private readonly RfDiagnosticsConfiguration configuration;
    private readonly List<RfDiagnosticPacket> packets = [];
    private readonly List<string> excessiveBeaconWarnings = [];
    private readonly List<string> pathWarnings = [];
    private DateTimeOffset? lastUpdatedUtc;

    public RfDiagnosticsService(RfDiagnosticsConfiguration? configuration = null)
    {
        this.configuration = configuration ?? RfDiagnosticsConfiguration.Default;
    }

    public RfDiagnosticPacket AcceptPacket(
        AprsPacket packet,
        AprsPacketSource packetSource,
        string receivedPortOrSource,
        DateTimeOffset? receivedAtUtc = null)
    {
        var timestamp = receivedAtUtc ?? packet.ReceivedAtUtc;
        var isRf = IsRfSource(packetSource, receivedPortOrSource);
        var qConstruct = GetQConstruct(packet);
        var warnings = AnalyzePath(packet.Path, qConstruct).ToList();
        var heardVia = packet.Path
            .Where(component => component.Contains('*', StringComparison.Ordinal))
            .Select(component => component.Trim())
            .Where(component => component.Length > 0)
            .ToArray();

        var fingerprint = BuildFingerprint(packet);
        var matchingWithinWindow = packets
            .Where(existing => timestamp - existing.LastSeenTimestampUtc <= configuration.DuplicateDetectionWindow
                && (string.Equals(existing.RawPacket, packet.RawLine, StringComparison.Ordinal)
                    || string.Equals(BuildFingerprint(existing), fingerprint, StringComparison.Ordinal)))
            .ToArray();
        var exactDuplicateCount = matchingWithinWindow.Length;
        var alsoSeenOnAprsIs = packetSource == AprsPacketSource.AprsIs
            || matchingWithinWindow.Any(existing => existing.PacketSource == AprsPacketSource.AprsIs);
        var linkState = GetInitialLinkState(packetSource, isRf);

        if (matchingWithinWindow.Any(existing => existing.IsReceivedFromRf != isRf || existing.PacketSource == AprsPacketSource.AprsIs || packetSource == AprsPacketSource.AprsIs))
        {
            linkState = RfDiagnosticLinkState.SeenOnBothRfAndAprsIs;
            alsoSeenOnAprsIs = true;
            MarkMatchingPacketsAsSeenOnBoth(matchingWithinWindow, timestamp);
        }

        var duplicateState = matchingWithinWindow.Length == 0
            ? RfDiagnosticDuplicateState.NotDuplicate
            : matchingWithinWindow.Any(existing => string.Equals(existing.RawPacket, packet.RawLine, StringComparison.Ordinal))
                ? RfDiagnosticDuplicateState.ConfirmedDuplicate
                : RfDiagnosticDuplicateState.PossibleDuplicate;

        if (duplicateState != RfDiagnosticDuplicateState.NotDuplicate)
        {
            warnings.Add($"Duplicate candidate seen {exactDuplicateCount + 1} times within {configuration.DuplicateDetectionWindow.TotalMinutes:0} minutes.");
        }

        warnings.AddRange(AnalyzePacketRate(packet.SourceCallsign, receivedPortOrSource, timestamp));

        var diagnostic = new RfDiagnosticPacket(
            Guid.NewGuid(),
            packet.RawLine,
            FormatPacketType(packet),
            packet.SourceCallsign,
            packet.Destination,
            packet.Path,
            packetSource,
            timestamp,
            string.IsNullOrWhiteSpace(receivedPortOrSource) ? "Unknown" : receivedPortOrSource.Trim(),
            isRf,
            alsoSeenOnAprsIs,
            duplicateState,
            exactDuplicateCount + 1,
            matchingWithinWindow.Select(existing => existing.FirstSeenTimestampUtc).DefaultIfEmpty(timestamp).Min(),
            timestamp,
            heardVia,
            qConstruct,
            linkState,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            packet.ValidationErrors);

        packets.Add(diagnostic);
        lastUpdatedUtc = timestamp;
        EnforceHistoryLimit();
        CaptureWarnings(diagnostic);
        return diagnostic;
    }

    public IReadOnlyList<RfDiagnosticPacket> GetRecentPackets(int? maximumCount = null)
    {
        var query = packets.OrderByDescending(packet => packet.ReceivedTimestampUtc).ThenByDescending(packet => packet.DiagnosticId);
        return maximumCount is > 0 ? query.Take(maximumCount.Value).ToArray() : query.ToArray();
    }

    public RfDiagnosticsSummary GetSummary()
    {
        var recent = GetRecentPackets();
        return new RfDiagnosticsSummary(
            recent.Count,
            recent.Count(packet => packet.IsReceivedFromRf),
            recent.Count(packet => packet.PacketSource == AprsPacketSource.AprsIs),
            recent.Count(packet => packet.DuplicateState != RfDiagnosticDuplicateState.NotDuplicate),
            recent.Select(packet => packet.SourceCallsign).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            recent.GroupBy(packet => packet.ReceivedPortOrSource, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(group => new KeyValuePair<string, int>(group.Key, group.Count()))
                .ToArray(),
            recent.GroupBy(packet => packet.SourceCallsign, StringComparer.OrdinalIgnoreCase)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .Select(group => new KeyValuePair<string, int>(group.Key, group.Count()))
                .ToArray(),
            excessiveBeaconWarnings.Distinct(StringComparer.OrdinalIgnoreCase).TakeLast(10).ToArray(),
            pathWarnings.Distinct(StringComparer.OrdinalIgnoreCase).TakeLast(10).ToArray(),
            recent.Count(packet => packet.LinkState == RfDiagnosticLinkState.RfOnly),
            recent.Count(packet => packet.LinkState == RfDiagnosticLinkState.AprsIsOnly),
            recent.Count(packet => packet.LinkState == RfDiagnosticLinkState.SeenOnBothRfAndAprsIs),
            lastUpdatedUtc);
    }

    public IReadOnlyDictionary<string, int> GetPacketRateByCallsign()
    {
        var cutoff = lastUpdatedUtc is null ? DateTimeOffset.MinValue : lastUpdatedUtc.Value - configuration.PacketRateWindow;
        return packets
            .Where(packet => packet.ReceivedTimestampUtc >= cutoff && !string.IsNullOrWhiteSpace(packet.SourceCallsign))
            .GroupBy(packet => packet.SourceCallsign, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, int> GetPacketRateBySourcePort()
    {
        var cutoff = lastUpdatedUtc is null ? DateTimeOffset.MinValue : lastUpdatedUtc.Value - configuration.PacketRateWindow;
        return packets
            .Where(packet => packet.ReceivedTimestampUtc >= cutoff)
            .GroupBy(packet => packet.ReceivedPortOrSource, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public void ClearDiagnostics()
    {
        packets.Clear();
        excessiveBeaconWarnings.Clear();
        pathWarnings.Clear();
        lastUpdatedUtc = null;
    }

    private IEnumerable<string> AnalyzePath(IReadOnlyList<string> path, string? qConstruct)
    {
        if (path.Count > configuration.MaximumRecommendedPathComponents)
        {
            yield return $"Path has {path.Count} components, which is longer than the recommended {configuration.MaximumRecommendedPathComponents}.";
        }

        if (path.Any(component => component.Contains('*', StringComparison.Ordinal)))
        {
            yield return "Path contains used digipeater components.";
        }

        if (path.Any(component => component.StartsWith("TCPIP", StringComparison.OrdinalIgnoreCase)
            || component.StartsWith("TCPXX", StringComparison.OrdinalIgnoreCase)))
        {
            yield return "Path contains TCPIP/TCPXX APRS-IS markers.";
        }

        if (qConstruct is not null)
        {
            yield return $"Path contains APRS-IS q construct {qConstruct}.";
        }

        if (path.Count(component => component.StartsWith("WIDE", StringComparison.OrdinalIgnoreCase)) > 2)
        {
            yield return "Path contains multiple WIDE components; check for excessive RF pathing.";
        }
    }

    private IEnumerable<string> AnalyzePacketRate(string sourceCallsign, string sourcePort, DateTimeOffset timestamp)
    {
        var cutoff = timestamp - configuration.PacketRateWindow;
        var stationCount = packets.Count(packet =>
            packet.ReceivedTimestampUtc >= cutoff
            && string.Equals(packet.SourceCallsign, sourceCallsign, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(sourceCallsign) && stationCount + 1 > configuration.ExcessiveBeaconCountThreshold)
        {
            yield return $"{sourceCallsign} exceeded {configuration.ExcessiveBeaconCountThreshold} packets in {configuration.PacketRateWindow.TotalMinutes:0} minutes.";
        }

        var portName = string.IsNullOrWhiteSpace(sourcePort) ? "Unknown" : sourcePort.Trim();
        var portCount = packets.Count(packet =>
            packet.ReceivedTimestampUtc >= cutoff
            && string.Equals(packet.ReceivedPortOrSource, portName, StringComparison.OrdinalIgnoreCase));
        if (portCount + 1 > configuration.ExcessivePortPacketCountThreshold)
        {
            yield return $"{portName} exceeded {configuration.ExcessivePortPacketCountThreshold} packets in {configuration.PacketRateWindow.TotalMinutes:0} minutes.";
        }

        var previousFromStation = packets
            .Where(packet => string.Equals(packet.SourceCallsign, sourceCallsign, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(packet => packet.ReceivedTimestampUtc)
            .FirstOrDefault();
        if (previousFromStation is not null && timestamp - previousFromStation.ReceivedTimestampUtc < configuration.MinimumBeaconInterval)
        {
            yield return $"{sourceCallsign} sent packets less than {configuration.MinimumBeaconInterval.TotalSeconds:0} seconds apart.";
        }
    }

    private void MarkMatchingPacketsAsSeenOnBoth(IReadOnlyList<RfDiagnosticPacket> matchingPackets, DateTimeOffset timestamp)
    {
        foreach (var match in matchingPackets)
        {
            var index = packets.FindIndex(packet => packet.DiagnosticId == match.DiagnosticId);
            if (index < 0)
            {
                continue;
            }

            var existing = packets[index];
            packets[index] = existing with
            {
                WasAlsoSeenOnAprsIs = true,
                DuplicateState = existing.DuplicateState == RfDiagnosticDuplicateState.NotDuplicate
                    ? RfDiagnosticDuplicateState.DuplicateOfEarlierPacket
                    : existing.DuplicateState,
                DuplicateCount = existing.DuplicateCount + 1,
                LastSeenTimestampUtc = timestamp,
                LinkState = RfDiagnosticLinkState.SeenOnBothRfAndAprsIs,
                ValidationWarnings = existing.ValidationWarnings
                    .Concat(["Packet was seen on both RF and APRS-IS."])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            };
        }
    }

    private void CaptureWarnings(RfDiagnosticPacket diagnostic)
    {
        foreach (var warning in diagnostic.ValidationWarnings)
        {
            if (warning.Contains("Path", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("TCPIP", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("q construct", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("WIDE", StringComparison.OrdinalIgnoreCase))
            {
                pathWarnings.Add($"{diagnostic.SourceCallsign}: {warning}");
            }

            if (warning.Contains("exceeded", StringComparison.OrdinalIgnoreCase)
                || warning.Contains("less than", StringComparison.OrdinalIgnoreCase))
            {
                excessiveBeaconWarnings.Add($"{diagnostic.SourceCallsign}: {warning}");
            }
        }
    }

    private void EnforceHistoryLimit()
    {
        var maximum = Math.Max(1, configuration.MaximumRecentPackets);
        while (packets.Count > maximum)
        {
            packets.RemoveAt(0);
        }
    }

    private static RfDiagnosticLinkState GetInitialLinkState(AprsPacketSource source, bool isRf)
    {
        if (isRf)
        {
            return RfDiagnosticLinkState.RfOnly;
        }

        return source == AprsPacketSource.AprsIs ? RfDiagnosticLinkState.AprsIsOnly : RfDiagnosticLinkState.Unknown;
    }

    private static bool IsRfSource(AprsPacketSource packetSource, string sourcePort)
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

    private static string BuildFingerprint(AprsPacket packet)
    {
        return $"{packet.SourceCallsign}>{packet.Destination}:{packet.Information}";
    }

    private static string BuildFingerprint(RfDiagnosticPacket packet)
    {
        var separatorIndex = packet.RawPacket.IndexOf(':');
        var information = separatorIndex >= 0 ? packet.RawPacket[(separatorIndex + 1)..] : string.Empty;
        return $"{packet.SourceCallsign}>{packet.Destination}:{information}";
    }

    private static string FormatPacketType(AprsPacket packet)
    {
        var typeName = packet.GetType().Name;
        return typeName.EndsWith("AprsPacket", StringComparison.Ordinal)
            ? typeName[..^"AprsPacket".Length]
            : typeName;
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
}
