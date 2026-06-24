using Aprs.Mapping;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class ObjectMarkerViewModel
{
    private const double LongitudeMin = -180;
    private const double LongitudeMax = 180;
    private const double LatitudeMin = -90;
    private const double LatitudeMax = 90;

    public ObjectMarkerViewModel(ObjectMarker marker)
    {
        ObjectName = marker.ObjectName;
        ObjectType = marker.ObjectType;
        Latitude = marker.Latitude;
        Longitude = marker.Longitude;
        SymbolTableIdentifier = marker.SymbolTableIdentifier;
        SymbolCode = marker.SymbolCode;
        Overlay = marker.Overlay;
        SymbolDescription = marker.SymbolDescription;
        SymbolCategory = marker.SymbolCategory;
        MarkerIconKey = marker.MarkerIconKey;
        FallbackMarkerText = marker.FallbackMarkerText;
        Comment = marker.Comment;
        IsAlive = marker.IsAlive;
        IsKilled = marker.IsKilled;
        OwnerCallsign = marker.OwnerCallsign;
        IsLocallyOwned = marker.IsLocallyOwned;
        IsAdopted = marker.IsAdopted;
        LifecycleState = marker.LifecycleState;
    }

    public string ObjectName { get; }

    public AprsManagedObjectType ObjectType { get; }

    public double Latitude { get; }

    public double Longitude { get; }

    public char? SymbolTableIdentifier { get; }

    public char? SymbolCode { get; }

    public char? Overlay { get; }

    public string SymbolDescription { get; }

    public AprsSymbolCategory SymbolCategory { get; }

    public string MarkerIconKey { get; }

    public string FallbackMarkerText { get; }

    public string? Comment { get; }

    public bool IsAlive { get; }

    public bool IsKilled { get; }

    public string OwnerCallsign { get; }

    public bool IsLocallyOwned { get; }

    public bool IsAdopted { get; }

    public AprsObjectLifecycleState LifecycleState { get; }

    public string SymbolLabel => Overlay is null ? FallbackMarkerText : $"{Overlay}{FallbackMarkerText}";

    public string StateLabel => IsKilled ? "Killed" : LifecycleState.ToString();

    public bool IsInactive => LifecycleState is AprsObjectLifecycleState.Expired or AprsObjectLifecycleState.Killed || IsKilled;

    public double MapLeftPercent => Normalize(Longitude, LongitudeMin, LongitudeMax) * 100;

    public double MapTopPercent => (1 - Normalize(Latitude, LatitudeMin, LatitudeMax)) * 100;

    private static double Normalize(double value, double minimum, double maximum)
    {
        var normalized = (value - minimum) / (maximum - minimum);
        return Math.Clamp(normalized, 0, 1);
    }
}
