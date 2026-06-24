namespace Aprs.Services;

public sealed class SimulatedAprsPacketGenerator : ISimulatedAprsPacketGenerator
{
    public string GenerateFixedStationPacket(int index, SimulationConfiguration configuration)
    {
        var lat = configuration.AreaCenterLatitude + (index * 0.01);
        var lon = configuration.AreaCenterLongitude - (index * 0.01);
        return $"SIM{index:000}>APRS:!{AprsCoordinateFormatter.FormatLatitude(lat)}/{AprsCoordinateFormatter.FormatLongitude(lon)}-Fixed simulated station";
    }

    public string GenerateMobileStationPacket(SimulatedMobileStation station)
    {
        return $"{station.Callsign}>APRS:={AprsCoordinateFormatter.FormatLatitude(station.Latitude)}/{AprsCoordinateFormatter.FormatLongitude(station.Longitude)}>{station.CourseDegrees:000}/{station.SpeedKnots:000} Mobile simulation";
    }

    public string GenerateWeatherStationPacket(int index, SimulationConfiguration configuration)
    {
        var lat = configuration.AreaCenterLatitude - (index * 0.01);
        var lon = configuration.AreaCenterLongitude + (index * 0.01);
        return $"TESTWX{index}>APRS:!{AprsCoordinateFormatter.FormatLatitude(lat)}/{AprsCoordinateFormatter.FormatLongitude(lon)}_180/005g012t072r000p000P000h50b10132";
    }

    public string GenerateObjectPacket(int index, SimulationConfiguration configuration)
    {
        var lat = configuration.AreaCenterLatitude + 0.005 + (index * 0.005);
        var lon = configuration.AreaCenterLongitude + 0.005 + (index * 0.005);
        var name = $"OBJTEST{index}".PadRight(9)[..9];
        return $"SIMCTL>APRS:;{name}*111111z{AprsCoordinateFormatter.FormatLatitude(lat)}/{AprsCoordinateFormatter.FormatLongitude(lon)}-Sim object";
    }

    public string GenerateStatusPacket(int index)
    {
        return $"SIM{index:000}>APRS:>Simulation status online";
    }

    public string GenerateMessagePacket(int index)
    {
        return $"SIM{index:000}>APRS::N0CALL   :Simulated message{{{index:00}";
    }

    public string GenerateBulletinPacket(int index)
    {
        return $"SIMCTL>APRS::BLN{index % 10}     :Simulation bulletin {index}";
    }

    public SimulatedMobileStation CreateMobileStation(int index, SimulationConfiguration configuration)
    {
        var speed = Math.Min(configuration.MaximumSimulatedSpeedKnots, 15 + (index * 5));
        return new SimulatedMobileStation(
            $"SIM{index:000}-9",
            configuration.AreaCenterLatitude + (index * 0.004),
            configuration.AreaCenterLongitude - (index * 0.004),
            speed,
            (45 + (index * 55)) % 360);
    }

    public SimulatedMobileStation UpdateMobileStation(SimulatedMobileStation station, TimeSpan elapsed, SimulationConfiguration configuration)
    {
        if (!configuration.MovementEnabled || elapsed <= TimeSpan.Zero)
        {
            return station;
        }

        var distanceMeters = station.SpeedKnots * 0.514444 * elapsed.TotalSeconds;
        var courseRadians = station.CourseDegrees * Math.PI / 180.0;
        var deltaNorth = Math.Cos(courseRadians) * distanceMeters;
        var deltaEast = Math.Sin(courseRadians) * distanceMeters;
        var metersPerDegreeLatitude = 111_320.0;
        var metersPerDegreeLongitude = Math.Max(1, metersPerDegreeLatitude * Math.Cos(station.Latitude * Math.PI / 180.0));
        var next = station with
        {
            Latitude = station.Latitude + (deltaNorth / metersPerDegreeLatitude),
            Longitude = station.Longitude + (deltaEast / metersPerDegreeLongitude)
        };

        if (GeofenceService.CalculateDistanceMeters(configuration.AreaCenterLatitude, configuration.AreaCenterLongitude, next.Latitude, next.Longitude) > configuration.AreaRadiusMeters)
        {
            next = next with { CourseDegrees = (next.CourseDegrees + 180) % 360 };
        }

        return next;
    }
}
