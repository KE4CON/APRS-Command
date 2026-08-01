using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class StationListRowViewModel
{
    public StationListRowViewModel(StationMarkerViewModel marker)
    {
        Marker = marker;
        Callsign = marker.Callsign;
        DisplayName = marker.DisplayName;
        SymbolDescription = marker.SymbolDescription;
        Distance = "Unknown";
        Bearing = "Unknown";
        LastHeardUtc = marker.LastHeardUtc;
        LastHeard = FormatAge(DateTimeOffset.UtcNow - marker.LastHeardUtc);
        AgeState = marker.AgeState;
        AgeStateLabel = marker.AgeState.ToString();
        Speed = marker.SpeedKnots is null ? "Unknown" : $"{marker.SpeedKnots} kt";
        Course = marker.CourseDegrees is null ? "Unknown" : $"{marker.CourseDegrees} deg";
        Comment = string.IsNullOrWhiteSpace(marker.Comment) ? "None" : marker.Comment;
        PacketSource = marker.PacketSource;
        PacketSourceLabel = marker.PacketSource.ToString();
        IsLoRa = marker.IsLoRa;
        Device = marker.Device;
        HasDevice = marker.DeviceIdentity is not null;
    }

    /// <summary>The sending device/software, e.g. "APRS Command (Desktop software)" or "Unknown".</summary>
    public string Device { get; }

    /// <summary>True when the device was identified (so the UI can hide an "Unknown" line).</summary>
    public bool HasDevice { get; }

    public StationMarkerViewModel Marker { get; }

    public string Callsign { get; }

    public string DisplayName { get; }

    public string SymbolDescription { get; }

    public string Distance { get; }

    public string Bearing { get; }

    public DateTimeOffset LastHeardUtc { get; }

    public string LastHeard { get; }

    public StationLifecycleState AgeState { get; }

    public string AgeStateLabel { get; }

    public string Speed { get; }

    public string Course { get; }

    public string Comment { get; }

    public AprsPacketSource PacketSource { get; }

    public string PacketSourceLabel { get; }
    public bool IsLoRa { get; }

    private static string FormatAge(TimeSpan age)
    {
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (age.TotalHours < 1)
        {
            return $"{(int)age.TotalMinutes} min ago";
        }

        if (age.TotalDays < 1)
        {
            return $"{(int)age.TotalHours} hr ago";
        }

        return $"{(int)age.TotalDays} d ago";
    }
}
