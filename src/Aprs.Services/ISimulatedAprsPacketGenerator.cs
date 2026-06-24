namespace Aprs.Services;

public interface ISimulatedAprsPacketGenerator
{
    string GenerateFixedStationPacket(int index, SimulationConfiguration configuration);

    string GenerateMobileStationPacket(SimulatedMobileStation station);

    string GenerateWeatherStationPacket(int index, SimulationConfiguration configuration);

    string GenerateObjectPacket(int index, SimulationConfiguration configuration);

    string GenerateStatusPacket(int index);

    string GenerateMessagePacket(int index);

    string GenerateBulletinPacket(int index);

    SimulatedMobileStation CreateMobileStation(int index, SimulationConfiguration configuration);

    SimulatedMobileStation UpdateMobileStation(SimulatedMobileStation station, TimeSpan elapsed, SimulationConfiguration configuration);
}
