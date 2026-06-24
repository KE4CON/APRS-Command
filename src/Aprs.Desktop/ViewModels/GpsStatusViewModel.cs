using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class GpsStatusViewModel
{
    private static readonly TimeSpan DefaultStaleThreshold = TimeSpan.FromSeconds(10);

    public GpsStatusViewModel(GpsPosition? position, DateTimeOffset now, TimeSpan? staleThreshold = null, bool disconnected = false)
    {
        var threshold = staleThreshold ?? DefaultStaleThreshold;
        Position = position;
        FixState = DetermineFixState(position, now, threshold, disconnected);
        Source = FormatSource(position);
        SourceName = FormatOptional(position?.SourceName);
        FixStatus = FormatFixStatus(FixState);
        StatusBrush = FormatStatusBrush(FixState);
        Latitude = FormatLatitude(position?.Latitude);
        Longitude = FormatLongitude(position?.Longitude);
        Altitude = FormatAltitude(position?.AltitudeMeters);
        Speed = FormatSpeed(position?.SpeedKnots);
        Course = FormatCourse(position?.CourseDegrees);
        SatelliteCount = FormatNullableInt(position?.SatelliteCount);
        UsedSatelliteCount = FormatNullableInt(position?.UsedSatelliteCount);
        Hdop = position?.Hdop is null ? "Unknown" : position.Hdop.Value.ToString("0.0");
        LastUpdate = position is null ? "Unknown" : position.LastUpdateUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        Age = position is null ? "Unknown" : FormatAge(now - position.LastUpdateUtc);
        RawSource = FormatOptional(position?.RawNmeaSentence);
    }

    public GpsPosition? Position { get; }

    public GpsFixDisplayState FixState { get; }

    public string Source { get; }

    public string SourceName { get; }

    public string FixStatus { get; }

    public string StatusBrush { get; }

    public string Latitude { get; }

    public string Longitude { get; }

    public string Altitude { get; }

    public string Speed { get; }

    public string Course { get; }

    public string SatelliteCount { get; }

    public string UsedSatelliteCount { get; }

    public string Hdop { get; }

    public string LastUpdate { get; }

    public string Age { get; }

    public string RawSource { get; }

    public bool HasPosition => Position is not null;

    public static GpsStatusViewModel CreateDesignTime()
    {
        var now = DateTimeOffset.UtcNow;
        var position = new GpsPosition(
            Latitude: 39.058333,
            Longitude: -84.508333,
            AltitudeMeters: 250,
            SpeedKnots: 12.5,
            CourseDegrees: 45,
            TimestampUtc: now,
            FixValid: true,
            FixQuality: 3,
            SatelliteCount: 8,
            Hdop: 0.9,
            SourceName: "gpsd:/dev/ttyUSB0",
            RawNmeaSentence: "{\"class\":\"TPV\",\"mode\":3}",
            LastUpdateUtc: now)
        {
            UsedSatelliteCount = 6
        };

        return new GpsStatusViewModel(position, now);
    }

    public static GpsStatusViewModel FromGpsService(IGpsService gpsService, DateTimeOffset now, TimeSpan? staleThreshold = null)
    {
        return new GpsStatusViewModel(gpsService.CurrentPosition, now, staleThreshold);
    }

    private static GpsFixDisplayState DetermineFixState(GpsPosition? position, DateTimeOffset now, TimeSpan staleThreshold, bool disconnected)
    {
        if (disconnected)
        {
            return GpsFixDisplayState.Disconnected;
        }

        if (position is null)
        {
            return GpsFixDisplayState.Unknown;
        }

        if (!position.FixValid || position.Latitude is null || position.Longitude is null)
        {
            return GpsFixDisplayState.NoFix;
        }

        return now - position.LastUpdateUtc > staleThreshold
            ? GpsFixDisplayState.Stale
            : GpsFixDisplayState.Valid;
    }

    private static string FormatSource(GpsPosition? position)
    {
        if (position is null || string.IsNullOrWhiteSpace(position.SourceName))
        {
            return "Unknown";
        }

        var source = position.SourceName;
        if (source.StartsWith("gpsd", StringComparison.OrdinalIgnoreCase))
        {
            return "gpsd";
        }

        if (source.Contains("simulation", StringComparison.OrdinalIgnoreCase))
        {
            return "Simulation";
        }

        if (source.Contains("manual", StringComparison.OrdinalIgnoreCase))
        {
            return "Manual";
        }

        return source.Contains("nmea", StringComparison.OrdinalIgnoreCase)
            ? "NMEA"
            : source;
    }

    private static string FormatFixStatus(GpsFixDisplayState state)
    {
        return state switch
        {
            GpsFixDisplayState.Valid => "Valid fix",
            GpsFixDisplayState.Stale => "Stale fix",
            GpsFixDisplayState.NoFix => "No fix",
            GpsFixDisplayState.Disconnected => "Disconnected",
            _ => "Unknown"
        };
    }

    private static string FormatStatusBrush(GpsFixDisplayState state)
    {
        return state switch
        {
            GpsFixDisplayState.Valid => "#166534",
            GpsFixDisplayState.Stale => "#B45309",
            GpsFixDisplayState.NoFix => "#B91C1C",
            GpsFixDisplayState.Disconnected => "#991B1B",
            _ => "#64748B"
        };
    }

    private static string FormatLatitude(double? latitude)
    {
        if (latitude is null)
        {
            return "Unknown";
        }

        return $"{Math.Abs(latitude.Value):0.00000} {(latitude >= 0 ? "N" : "S")}";
    }

    private static string FormatLongitude(double? longitude)
    {
        if (longitude is null)
        {
            return "Unknown";
        }

        return $"{Math.Abs(longitude.Value):0.00000} {(longitude >= 0 ? "E" : "W")}";
    }

    private static string FormatAltitude(double? altitudeMeters)
    {
        return altitudeMeters is null
            ? "Unknown"
            : $"{Math.Round(altitudeMeters.Value * 3.28084):0} ft";
    }

    private static string FormatSpeed(double? speedKnots)
    {
        return speedKnots is null ? "Unknown" : $"{speedKnots.Value:0.0} kt";
    }

    private static string FormatCourse(double? courseDegrees)
    {
        return courseDegrees is null ? "Unknown" : $"{courseDegrees.Value:000} deg";
    }

    private static string FormatNullableInt(int? value)
    {
        return value is null ? "Unknown" : value.Value.ToString();
    }

    private static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalSeconds < 1)
        {
            return "Just now";
        }

        if (age.TotalMinutes < 1)
        {
            return $"{(int)age.TotalSeconds} sec ago";
        }

        if (age.TotalHours < 1)
        {
            return $"{(int)age.TotalMinutes} min ago";
        }

        return $"{(int)age.TotalHours} hr ago";
    }

    private static string FormatOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
    }
}
