namespace Aprs.Services;

public interface ISmartBeaconingDecisionService
{
    /// <summary>
    /// Evaluates a mobile position and decides whether SmartBeaconing recommends a beacon.
    /// </summary>
    SmartBeaconingDecision Evaluate(MobilePositionInput currentPosition);

    /// <summary>
    /// Records that a beacon was sent or accepted for the supplied mobile position.
    /// </summary>
    void RecordBeacon(MobilePositionInput beaconedPosition);

    /// <summary>
    /// Clears previous mobile position and beacon history.
    /// </summary>
    void Reset();
}
