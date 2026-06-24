using Aprs.Core;

namespace Aprs.Services;

public interface IAprsObjectManager
{
    /// <summary>
    /// Accepts a parsed APRS object packet and creates or updates managed object state.
    /// </summary>
    AprsObjectState? AcceptObject(ObjectAprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown);

    /// <summary>
    /// Accepts a parsed APRS item packet and creates or updates managed item state.
    /// </summary>
    AprsObjectState? AcceptItem(ItemAprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown);

    /// <summary>
    /// Accepts a parsed packet and stores it if it is an APRS object or item.
    /// </summary>
    AprsObjectState? AcceptPacket(AprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown);

    AprsObjectState? GetObject(string name);

    IReadOnlyList<AprsObjectState> GetAllObjects();

    IReadOnlyList<AprsObjectState> GetActiveObjects(DateTimeOffset now);

    IReadOnlyList<AprsObjectState> GetKilledObjects();

    IReadOnlyList<AprsObjectState> GetInactiveObjects(DateTimeOffset now);

    void UpdateLifecycleStates(DateTimeOffset now);

    AprsObjectState? MarkLocallyCreated(string name, string localStationCallsign, DateTimeOffset now);

    AprsObjectState? AdoptObject(string name, string localStationCallsign, DateTimeOffset now);

    bool RemoveObject(string name);

    void Clear();
}
