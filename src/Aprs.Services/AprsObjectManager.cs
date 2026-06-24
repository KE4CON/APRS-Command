using Aprs.Core;

namespace Aprs.Services;

public sealed class AprsObjectManager : IAprsObjectManager
{
    private readonly Dictionary<string, AprsObjectState> objects = new(StringComparer.OrdinalIgnoreCase);
    private readonly AprsObjectManagerConfiguration configuration;

    public AprsObjectManager()
        : this(AprsObjectManagerConfiguration.Default)
    {
    }

    public AprsObjectManager(AprsObjectManagerConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public AprsObjectState? AcceptPacket(AprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown)
    {
        return packet switch
        {
            ObjectAprsPacket aprsObject => AcceptObject(aprsObject, packetSource),
            ItemAprsPacket item => AcceptItem(item, packetSource),
            _ => null
        };
    }

    public AprsObjectState? AcceptObject(ObjectAprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown)
    {
        if (string.IsNullOrWhiteSpace(packet.ObjectName))
        {
            return null;
        }

        var name = NormalizeName(packet.ObjectName);
        var existing = objects.GetValueOrDefault(name);
        var ownerCallsign = FormatSourceCallsign(packet.SourceCallsign, packet.SourceSsid);
        var lifecycleState = packet.IsKilled
            ? AprsObjectLifecycleState.Killed
            : packet.IsValid && packet.IsAlive ? AprsObjectLifecycleState.Active : AprsObjectLifecycleState.Expired;
        var ownershipWarning = CreateOwnershipWarning(existing, ownerCallsign);

        var state = new AprsObjectState(
            name,
            AprsManagedObjectType.Object,
            ownerCallsign,
            packet.IsAlive,
            packet.IsKilled,
            lifecycleState,
            packet.Latitude ?? existing?.Latitude,
            packet.Longitude ?? existing?.Longitude,
            packet.SymbolTableIdentifier ?? existing?.SymbolTableIdentifier,
            packet.SymbolCode ?? existing?.SymbolCode,
            existing?.Overlay,
            string.IsNullOrWhiteSpace(packet.Comment) ? existing?.Comment : packet.Comment,
            packet.Timestamp ?? existing?.PacketTimestamp,
            existing?.FirstHeardUtc ?? packet.ReceivedAtUtc,
            packet.ReceivedAtUtc,
            packet.ReceivedAtUtc,
            packet.RawLine,
            packetSource,
            existing?.IsLocallyCreated ?? false,
            existing?.IsLocallyOwned ?? false,
            existing?.IsAdopted ?? false,
            ownershipWarning ?? existing?.OwnershipWarning,
            packet.ValidationErrors);

        objects[name] = state;
        return state;
    }

    public AprsObjectState? AcceptItem(ItemAprsPacket packet, AprsPacketSource packetSource = AprsPacketSource.Unknown)
    {
        if (string.IsNullOrWhiteSpace(packet.ItemName))
        {
            return null;
        }

        var name = NormalizeName(packet.ItemName);
        var existing = objects.GetValueOrDefault(name);
        var ownerCallsign = FormatSourceCallsign(packet.SourceCallsign, packet.SourceSsid);
        var ownershipWarning = CreateOwnershipWarning(existing, ownerCallsign);
        var state = new AprsObjectState(
            name,
            AprsManagedObjectType.Item,
            ownerCallsign,
            packet.IsValid,
            false,
            packet.IsValid ? AprsObjectLifecycleState.Active : AprsObjectLifecycleState.Expired,
            packet.Latitude ?? existing?.Latitude,
            packet.Longitude ?? existing?.Longitude,
            packet.SymbolTableIdentifier ?? existing?.SymbolTableIdentifier,
            packet.SymbolCode ?? existing?.SymbolCode,
            existing?.Overlay,
            string.IsNullOrWhiteSpace(packet.Comment) ? existing?.Comment : packet.Comment,
            null,
            existing?.FirstHeardUtc ?? packet.ReceivedAtUtc,
            packet.ReceivedAtUtc,
            packet.ReceivedAtUtc,
            packet.RawLine,
            packetSource,
            existing?.IsLocallyCreated ?? false,
            existing?.IsLocallyOwned ?? false,
            existing?.IsAdopted ?? false,
            ownershipWarning ?? existing?.OwnershipWarning,
            packet.ValidationErrors);

        objects[name] = state;
        return state;
    }

    public AprsObjectState? GetObject(string name)
    {
        return objects.TryGetValue(NormalizeName(name), out var state) ? state : null;
    }

    public IReadOnlyList<AprsObjectState> GetAllObjects()
    {
        return SortObjects(objects.Values);
    }

    public IReadOnlyList<AprsObjectState> GetActiveObjects(DateTimeOffset now)
    {
        return objects.Values
            .Select(state => state with { LifecycleState = CalculateLifecycleState(state, now) })
            .Where(state => state.LifecycleState == AprsObjectLifecycleState.Active)
            .OrderBy(state => state.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<AprsObjectState> GetKilledObjects()
    {
        return SortObjects(objects.Values.Where(state => state.IsKilled || state.LifecycleState == AprsObjectLifecycleState.Killed));
    }

    public IReadOnlyList<AprsObjectState> GetInactiveObjects(DateTimeOffset now)
    {
        return objects.Values
            .Select(state => state with { LifecycleState = CalculateLifecycleState(state, now) })
            .Where(state => state.LifecycleState is AprsObjectLifecycleState.Stale or AprsObjectLifecycleState.Expired or AprsObjectLifecycleState.Killed)
            .OrderBy(state => state.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void UpdateLifecycleStates(DateTimeOffset now)
    {
        foreach (var (name, state) in objects.ToArray())
        {
            objects[name] = state with { LifecycleState = CalculateLifecycleState(state, now) };
        }
    }

    public AprsObjectState? MarkLocallyCreated(string name, string localStationCallsign, DateTimeOffset now)
    {
        var normalizedName = NormalizeName(name);
        if (!objects.TryGetValue(normalizedName, out var state))
        {
            return null;
        }

        var local = NormalizeCallsign(localStationCallsign);
        var warning = string.Equals(state.OwnerCallsign, local, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Object is owned by {state.OwnerCallsign}; local station {local} should adopt before managing.";
        var updated = state with
        {
            IsLocallyCreated = true,
            IsLocallyOwned = string.Equals(state.OwnerCallsign, local, StringComparison.OrdinalIgnoreCase),
            OwnershipWarning = warning,
            LastUpdatedUtc = now
        };

        objects[normalizedName] = updated;
        return updated;
    }

    public AprsObjectState? AdoptObject(string name, string localStationCallsign, DateTimeOffset now)
    {
        var normalizedName = NormalizeName(name);
        if (!objects.TryGetValue(normalizedName, out var state))
        {
            return null;
        }

        var local = NormalizeCallsign(localStationCallsign);
        var updated = state with
        {
            IsAdopted = true,
            IsLocallyOwned = true,
            OwnershipWarning = string.Equals(state.OwnerCallsign, local, StringComparison.OrdinalIgnoreCase)
                ? null
                : $"Object adopted locally from owner {state.OwnerCallsign}.",
            LastUpdatedUtc = now
        };

        objects[normalizedName] = updated;
        return updated;
    }

    public bool RemoveObject(string name)
    {
        return objects.Remove(NormalizeName(name));
    }

    public void Clear()
    {
        objects.Clear();
    }

    private AprsObjectLifecycleState CalculateLifecycleState(AprsObjectState state, DateTimeOffset now)
    {
        if (state.IsKilled || state.LifecycleState == AprsObjectLifecycleState.Killed)
        {
            return AprsObjectLifecycleState.Killed;
        }

        if (state.ValidationErrors.Count > 0)
        {
            return AprsObjectLifecycleState.Expired;
        }

        var age = now - state.LastHeardUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        if (age >= configuration.ExpiredThreshold)
        {
            return AprsObjectLifecycleState.Expired;
        }

        if (age >= configuration.StaleThreshold)
        {
            return AprsObjectLifecycleState.Stale;
        }

        return AprsObjectLifecycleState.Active;
    }

    private static string? CreateOwnershipWarning(AprsObjectState? existing, string newOwnerCallsign)
    {
        if (existing is null
            || !existing.IsLocallyOwned
            || string.Equals(existing.OwnerCallsign, newOwnerCallsign, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"Object was locally owned by {existing.OwnerCallsign} but was updated by {newOwnerCallsign}.";
    }

    private static IReadOnlyList<AprsObjectState> SortObjects(IEnumerable<AprsObjectState> values)
    {
        return values
            .OrderBy(state => state.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeName(string name)
    {
        return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim().ToUpperInvariant();
    }

    private static string FormatSourceCallsign(string callsign, int? ssid)
    {
        var normalized = NormalizeCallsign(callsign);
        return ssid is null ? normalized : $"{normalized}-{ssid}";
    }

    private static string NormalizeCallsign(string callsign)
    {
        return string.IsNullOrWhiteSpace(callsign) ? string.Empty : callsign.Trim().ToUpperInvariant();
    }
}
