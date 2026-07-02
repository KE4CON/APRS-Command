using Aprs.Core;

namespace Aprs.Services;

public interface IWeatherDisplayService
{
    WeatherStationDisplayRecord? AcceptWeatherPacket(WeatherAprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown);

    WeatherStationDisplayRecord UpsertWeatherStation(WeatherStationDisplayRecord record);

    void UpdateStaleStates(DateTimeOffset now);

    IReadOnlyCollection<WeatherStationDisplayRecord> GetAllWeatherStations();

    IReadOnlyCollection<WeatherStationDisplayRecord> GetCurrentWeatherStations();

    IReadOnlyCollection<WeatherStationDisplayRecord> GetStaleWeatherStations();

    WeatherStationDisplayRecord? GetWeatherStation(string stationId);

    IReadOnlyList<WeatherHistoryRecord> GetHistory(string stationId);

    void Clear();
}
