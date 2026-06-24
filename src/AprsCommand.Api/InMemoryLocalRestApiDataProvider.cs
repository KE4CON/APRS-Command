using AprsCommand.Contracts;

namespace AprsCommand.Api;

public sealed class InMemoryLocalRestApiDataProvider : ILocalRestApiDataProvider
{
    private readonly List<StationUpdateDto> stations = [];
    private readonly List<AprsObjectDto> objects = [];
    private readonly List<WeatherObservationDto> weather = [];
    private readonly List<MessageDto> messages = [];
    private readonly List<PortStatusDto> ports = [];
    private readonly List<AlertDto> alerts = [];
    private readonly List<RawPacketDto> rawPackets = [];
    private readonly List<DecodedEventDto> events = [];
    private readonly List<RfDiagnosticDto> rfDiagnostics = [];
    private GpsPositionDto? gps;
    private ReplayStatusDto replayStatus = new();
    private SimulationStatusDto simulationStatus = new();
    private TrainingScenarioDto? trainingStatus;

    public IReadOnlyList<StationUpdateDto> GetStations() => stations.ToArray();

    public StationUpdateDto? GetStation(string callsign)
    {
        return stations.LastOrDefault(station => string.Equals(station.Callsign, callsign, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<AprsObjectDto> GetObjects() => objects.ToArray();
    public IReadOnlyList<WeatherObservationDto> GetWeather() => weather.ToArray();
    public IReadOnlyList<MessageDto> GetMessages() => messages.ToArray();
    public GpsPositionDto? GetGps() => gps;
    public IReadOnlyList<PortStatusDto> GetPorts() => ports.ToArray();
    public IReadOnlyList<AlertDto> GetAlerts() => alerts.ToArray();
    public IReadOnlyList<RawPacketDto> GetRawPackets() => rawPackets.ToArray();
    public IReadOnlyList<DecodedEventDto> GetEvents() => events.ToArray();
    public IReadOnlyList<RfDiagnosticDto> GetRfDiagnostics() => rfDiagnostics.ToArray();
    public ReplayStatusDto GetReplayStatus() => replayStatus;
    public SimulationStatusDto GetSimulationStatus() => simulationStatus;
    public TrainingScenarioDto? GetTrainingStatus() => trainingStatus;

    public void SeedStations(params StationUpdateDto[] values) => stations.AddRange(values);
    public void SeedWeather(params WeatherObservationDto[] values) => weather.AddRange(values);
    public void SeedEvents(params DecodedEventDto[] values) => events.AddRange(values);
    public void SetReplayStatus(ReplayStatusDto value) => replayStatus = value;
    public void SetSimulationStatus(SimulationStatusDto value) => simulationStatus = value;
    public void SetTrainingStatus(TrainingScenarioDto value) => trainingStatus = value;

    public void SubmitStation(StationUpdateDto station) => stations.Add(station);
    public void SubmitWeather(WeatherObservationDto observation) => weather.Add(observation);
    public void SubmitObject(AprsObjectDto aprsObject) => objects.Add(aprsObject);
    public void SubmitGps(GpsPositionDto position) => gps = position;
    public void SubmitRawPacket(RawPacketDto packet) => rawPackets.Add(packet);
}
