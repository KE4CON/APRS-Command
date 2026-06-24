using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class ObjectListRowViewModel
{
    public ObjectListRowViewModel(AprsObjectState state)
    {
        Name = state.Name;
        Type = state.ObjectType.ToString();
        Owner = state.OwnerCallsign;
        State = state.LifecycleState.ToString();
        Position = state.Latitude is null || state.Longitude is null
            ? "-"
            : $"{state.Latitude.Value:0.0000}, {state.Longitude.Value:0.0000}";
        Symbol = state.SymbolTableIdentifier is null || state.SymbolCode is null
            ? "-"
            : $"{state.SymbolTableIdentifier}{state.SymbolCode}";
        Comment = string.IsNullOrWhiteSpace(state.Comment) ? "-" : state.Comment;
        Ownership = state.IsLocallyOwned
            ? "Local"
            : state.IsAdopted ? "Adopted" : "Remote";
    }

    public string Name { get; }

    public string Type { get; }

    public string Owner { get; }

    public string State { get; }

    public string Position { get; }

    public string Symbol { get; }

    public string Comment { get; }

    public string Ownership { get; }
}
