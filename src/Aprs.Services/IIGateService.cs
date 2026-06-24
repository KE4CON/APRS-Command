namespace Aprs.Services;

public interface IIGateService
{
    Task<IGateGatingDecisionRecord> EvaluateAndGateAsync(
        IGateCandidatePacket candidate,
        CancellationToken cancellationToken = default);

    IReadOnlyList<IGateGatingDecisionRecord> GetRecentDecisions();

    IGateStatusSummary GetStatusSummary();

    void ClearDecisionHistory();
}
