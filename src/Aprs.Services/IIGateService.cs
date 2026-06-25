namespace Aprs.Services;

public interface IIGateService
{
    /// <summary>Whether iGating is currently enabled at runtime.</summary>
    bool IsEnabled { get; }

    /// <summary>Enable or disable iGating at runtime without restarting.</summary>
    void SetEnabled(bool enabled);

    Task<IGateGatingDecisionRecord> EvaluateAndGateAsync(
        IGateCandidatePacket candidate,
        CancellationToken cancellationToken = default);

    IReadOnlyList<IGateGatingDecisionRecord> GetRecentDecisions();

    IGateStatusSummary GetStatusSummary();

    void ClearDecisionHistory();
}
