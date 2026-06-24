namespace Aprs.Services;

public enum ApplicationEventType
{
    RawPacketReceived,
    AprsPacketParsed,
    StationUpdated,
    ObjectUpdated,
    WeatherUpdated,
    MessageReceived,
    GpsUpdated,
    PortStatusChanged,
    AlertTriggered,
    PacketTransmitted,
    TransmitBlocked,
    IGateDecisionMade,
    DigipeaterDecisionMade,
    TrainingStateChanged,
    SimulationStateChanged,
    ReplayStateChanged
}
