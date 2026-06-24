using Aprs.Services;

namespace Aprs.Mapping;

public sealed record ObjectMarker(
    string ObjectName,
    AprsManagedObjectType ObjectType,
    double Latitude,
    double Longitude,
    char? SymbolTableIdentifier,
    char? SymbolCode,
    char? Overlay,
    string SymbolDescription,
    AprsSymbolCategory SymbolCategory,
    string MarkerIconKey,
    string FallbackMarkerText,
    string? Comment,
    bool IsAlive,
    bool IsKilled,
    string OwnerCallsign,
    bool IsLocallyOwned,
    bool IsAdopted,
    AprsObjectLifecycleState LifecycleState)
{
    public static bool TryCreate(AprsObjectState state, out ObjectMarker? marker)
    {
        if (state.Latitude is null || state.Longitude is null)
        {
            marker = null;
            return false;
        }

        var symbol = AprsSymbolLookupService.Default.Resolve(state.SymbolTableIdentifier, state.SymbolCode);
        marker = new ObjectMarker(
            state.Name,
            state.ObjectType,
            state.Latitude.Value,
            state.Longitude.Value,
            state.SymbolTableIdentifier,
            state.SymbolCode,
            state.Overlay ?? symbol.Overlay,
            symbol.Description,
            symbol.Category,
            state.ObjectType == AprsManagedObjectType.Item && symbol.MarkerIconKey == "unknown" ? "object" : symbol.MarkerIconKey,
            symbol.FallbackDisplayText,
            state.Comment,
            state.IsAlive,
            state.IsKilled,
            state.OwnerCallsign,
            state.IsLocallyOwned,
            state.IsAdopted,
            state.LifecycleState);

        return true;
    }
}
