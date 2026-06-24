using AprsCommand.Contracts;

namespace AprsCommand.Api;

public interface ILocalRestApiDataProvider
{
    IReadOnlyList<StationUpdateDto> GetStations();
    StationUpdateDto? GetStation(string callsign);
    IReadOnlyList<AprsObjectDto> GetObjects();
    IReadOnlyList<WeatherObservationDto> GetWeather();
    IReadOnlyList<MessageDto> GetMessages();
    GpsPositionDto? GetGps();
    IReadOnlyList<PortStatusDto> GetPorts();
    IReadOnlyList<AlertDto> GetAlerts();
    IReadOnlyList<RawPacketDto> GetRawPackets();
    IReadOnlyList<DecodedEventDto> GetEvents();
    IReadOnlyList<RfDiagnosticDto> GetRfDiagnostics();
    ReplayStatusDto GetReplayStatus();
    SimulationStatusDto GetSimulationStatus();
    TrainingScenarioDto? GetTrainingStatus();
    void SubmitStation(StationUpdateDto station);
    void SubmitWeather(WeatherObservationDto weather);
    void SubmitObject(AprsObjectDto aprsObject);
    void SubmitGps(GpsPositionDto gps);
    void SubmitRawPacket(RawPacketDto packet);
}
