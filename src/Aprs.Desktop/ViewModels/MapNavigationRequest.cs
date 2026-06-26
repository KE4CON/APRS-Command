namespace Aprs.Desktop.ViewModels;

public enum MapNavigationKind
{
    Home,
    CentreOnStation,
    CentreOnPosition
}

/// <summary>
/// Navigation action requested by sidebar buttons or the station list.
/// For <see cref="MapNavigationKind.CentreOnPosition"/>, Latitude and Longitude are set.
/// </summary>
public sealed record MapNavigationRequest(
    MapNavigationKind Kind,
    double? Latitude = null,
    double? Longitude = null)
{
    public static MapNavigationRequest Home { get; } = new(MapNavigationKind.Home);
    public static MapNavigationRequest CentreOnStation { get; } = new(MapNavigationKind.CentreOnStation);
}
