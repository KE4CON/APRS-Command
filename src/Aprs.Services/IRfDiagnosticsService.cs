using Aprs.Core;

namespace Aprs.Services;

/// <summary>
/// Analyzes received APRS packet activity for RF path, duplicate, and traffic-rate diagnostics.
/// </summary>
public interface IRfDiagnosticsService
{
    RfDiagnosticPacket AcceptPacket(
        AprsPacket packet,
        AprsPacketSource packetSource,
        string receivedPortOrSource,
        DateTimeOffset? receivedAtUtc = null);

    IReadOnlyList<RfDiagnosticPacket> GetRecentPackets(int? maximumCount = null);

    RfDiagnosticsSummary GetSummary();

    IReadOnlyDictionary<string, int> GetPacketRateByCallsign();

    IReadOnlyDictionary<string, int> GetPacketRateBySourcePort();

    void ClearDiagnostics();
}
