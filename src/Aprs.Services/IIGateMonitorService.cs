using Aprs.Core;

namespace Aprs.Services;

public interface IIGateMonitorService
{
    IGateCandidatePacket AcceptRfPacket(
        AprsPacket packet,
        string sourcePort,
        DateTimeOffset? receivedAtUtc = null);

    IGateCandidatePacket AcceptPacket(
        AprsPacket packet,
        AprsPacketSource packetSource,
        string sourcePort,
        DateTimeOffset? receivedAtUtc = null);

    void AcceptAprsIsPacket(
        AprsPacket packet,
        string sourceName,
        DateTimeOffset? receivedAtUtc = null);

    IReadOnlyList<IGateCandidatePacket> GetRecentCandidates();

    IGateMonitorSummary GetSummary();

    void ClearCandidates();

    void ExpireCandidates(DateTimeOffset now);
}
