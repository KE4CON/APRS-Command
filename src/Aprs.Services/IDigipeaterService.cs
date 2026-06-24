using Aprs.Core;

namespace Aprs.Services;

public interface IDigipeaterService
{
    Task<DigipeaterDecisionRecord> EvaluateAndDigipeatAsync(
        AprsPacket packet,
        AprsPacketSource packetSource,
        string receivedRfPort,
        CancellationToken cancellationToken = default);

    IReadOnlyList<DigipeaterDecisionRecord> GetRecentDecisions();

    DigipeaterStatusSummary GetStatusSummary();

    void ClearDecisionHistory();
}
