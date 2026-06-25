using Aprs.Core;

namespace Aprs.Services;

public interface IDigipeaterService
{
    /// <summary>Whether digipeating is currently enabled at runtime.</summary>
    bool IsEnabled { get; }

    /// <summary>Enable or disable digipeating at runtime without restarting.</summary>
    void SetEnabled(bool enabled);

    Task<DigipeaterDecisionRecord> EvaluateAndDigipeatAsync(
        AprsPacket packet,
        AprsPacketSource packetSource,
        string receivedRfPort,
        CancellationToken cancellationToken = default);

    IReadOnlyList<DigipeaterDecisionRecord> GetRecentDecisions();

    DigipeaterStatusSummary GetStatusSummary();

    void ClearDecisionHistory();
}
