namespace Aprs.Desktop.ViewModels;

/// <summary>Navigation actions the sidebar buttons request from the MapView.</summary>
public enum MapNavigationRequest
{
    /// <summary>Reset the map to the operator's home QTH at a regional zoom level.</summary>
    Home,

    /// <summary>Centre the map on the operator's own station position.</summary>
    CentreOnStation
}
