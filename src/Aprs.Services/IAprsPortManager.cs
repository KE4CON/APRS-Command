namespace Aprs.Services;

public interface IAprsPortManager
{
    AprsPortSnapshot RegisterPort(AprsPortSnapshot port);

    bool RemovePort(string portId);

    bool SetPortEnabled(string portId, bool enabled, DateTimeOffset timestampUtc);

    IReadOnlyCollection<AprsPortSnapshot> GetAllPorts();

    IReadOnlyCollection<AprsPortSnapshot> GetReceiveEnabledPorts();

    IReadOnlyCollection<AprsPortSnapshot> GetTransmitEnabledPorts();

    AprsPortSnapshot? GetPort(string portId);

    bool UpdateConnectionState(string portId, AprsPortConnectionState connectionState, DateTimeOffset timestampUtc);

    bool RecordPacketReceived(string portId, DateTimeOffset timestampUtc);

    bool RecordPacketTransmitted(string portId, DateTimeOffset timestampUtc);

    bool RecordError(string portId, string error, DateTimeOffset timestampUtc);

    bool ClearCounters(string portId);

    void ClearAllCounters();

    AprsPortHealthSummary GetHealthSummary();

    AprsPortTransmitSafetyResult CheckTransmitSafety(string portId, bool globalTransmitSafetyEnabled);
}
