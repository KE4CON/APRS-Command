using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aprs.Desktop.ViewModels;

/// <summary>Check-in status for a station in the net control roster.</summary>
public enum CheckInStatus
{
    NotCheckedIn,
    CheckedIn,
    Standby,
    Departed
}

/// <summary>
/// ICS resource typing — indicates the capability level of a resource.
/// Type 1 is the highest capability, Type 4 the lowest. Typing allows
/// incident commanders to request resources by capability level.
/// </summary>
public enum ResourceType
{
    /// <summary>Not typed / unknown.</summary>
    Untyped,
    /// <summary>Type 1 — highest capability, most experienced.</summary>
    Type1,
    /// <summary>Type 2 — high capability.</summary>
    Type2,
    /// <summary>Type 3 — moderate capability.</summary>
    Type3,
    /// <summary>Type 4 — entry level / basic capability.</summary>
    Type4
}

/// <summary>
/// ICS operational resource status — tracks the current assignment state
/// of a resource during a net, exercise, or activation. Distinct from
/// check-in status which tracks net participation.
/// </summary>
public enum ResourceStatus
{
    /// <summary>Not yet assigned — available for tasking.</summary>
    Available,
    /// <summary>Has been given an assignment and is en route or preparing.</summary>
    Assigned,
    /// <summary>At the assigned location and actively working the task.</summary>
    OnScene,
    /// <summary>Task complete, returning to staging or available position.</summary>
    Returning,
    /// <summary>Out of service — unavailable for assignment.</summary>
    OutOfService
}

/// <summary>A single station entry in the net control roster.</summary>
public sealed class NetControlRosterEntry : INotifyPropertyChanged
{
    private CheckInStatus status = CheckInStatus.NotCheckedIn;
    private ResourceStatus resourceStatus = ResourceStatus.Available;
    private ResourceType resourceType = ResourceType.Untyped;
    private string resourceCategory = string.Empty;
    private string assignment = string.Empty;
    private string notes = string.Empty;
    private DateTimeOffset? checkedInAt;
    public DateTimeOffset? CheckedInAt => checkedInAt;
    private DateTimeOffset lastHeardUtc;
    private double? latitude;
    private double? longitude;
    private string comment = string.Empty;

    public string Callsign { get; init; } = string.Empty;
    public string TacticalLabel { get; init; } = string.Empty;

    public CheckInStatus Status
    {
        get => status;
        set
        {
            if (status != value)
            {
                status = value;
                if (value == CheckInStatus.CheckedIn && checkedInAt is null)
                    checkedInAt = DateTimeOffset.UtcNow;
                else if (value == CheckInStatus.NotCheckedIn)
                    checkedInAt = null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(CheckedInAtText));
                OnPropertyChanged(nameof(IsCheckedIn));
            }
        }
    }

    public string Notes
    {
        get => notes;
        set { if (notes != value) { notes = value; OnPropertyChanged(); } }
    }

    public ResourceStatus ResourceStatus
    {
        get => resourceStatus;
        set
        {
            if (resourceStatus != value)
            {
                resourceStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ResourceStatusLabel));
                OnPropertyChanged(nameof(ResourceStatusColor));
                OnPropertyChanged(nameof(ResourceStatusIcon));
            }
        }
    }

    public string Assignment
    {
        get => assignment;
        set { if (assignment != value) { assignment = value; OnPropertyChanged(); } }
    }

    public ResourceType ResourceType
    {
        get => resourceType;
        set { if (resourceType != value) { resourceType = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResourceTypeLabel)); } }
    }

    /// <summary>Optional category description e.g. "Communications", "Medical", "Logistics".</summary>
    public string ResourceCategory
    {
        get => resourceCategory;
        set { if (resourceCategory != value) { resourceCategory = value; OnPropertyChanged(); } }
    }

    public string ResourceTypeLabel => resourceType switch
    {
        ResourceType.Type1   => "Type 1",
        ResourceType.Type2   => "Type 2",
        ResourceType.Type3   => "Type 3",
        ResourceType.Type4   => "Type 4",
        _                    => string.Empty
    };

    public DateTimeOffset LastHeardUtc
    {
        get => lastHeardUtc;
        set
        {
            if (lastHeardUtc != value)
            {
                lastHeardUtc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastHeardText));
                OnPropertyChanged(nameof(MinutesSinceHeard));
                OnPropertyChanged(nameof(IsStale));
            }
        }
    }

    public double? Latitude
    {
        get => latitude;
        set { if (latitude != value) { latitude = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionText)); } }
    }

    public double? Longitude
    {
        get => longitude;
        set { if (longitude != value) { longitude = value; OnPropertyChanged(); OnPropertyChanged(nameof(PositionText)); } }
    }

    public string Comment
    {
        get => comment;
        set { if (comment != value) { comment = value; OnPropertyChanged(); } }
    }

    // ── Computed display properties ────────────────────────────────────

    public bool IsCheckedIn => status == CheckInStatus.CheckedIn;

    public string StatusIcon => status switch
    {
        CheckInStatus.CheckedIn    => "✓",
        CheckInStatus.Standby      => "⏸",
        CheckInStatus.Departed     => "✗",
        _                          => "○"
    };

    public string StatusColor => status switch
    {
        CheckInStatus.CheckedIn    => "#15803d",
        CheckInStatus.Standby      => "#b45309",
        CheckInStatus.Departed     => "#6b7280",
        _                          => "#94a3b8"
    };

    public double MinutesSinceHeard =>
        (DateTimeOffset.UtcNow - lastHeardUtc).TotalMinutes;

    public bool IsStale => MinutesSinceHeard > 30;

    public string LastHeardText
    {
        get
        {
            var mins = MinutesSinceHeard;
            if (mins < 1)   return "Just now";
            if (mins < 60)  return $"{(int)mins}m ago";
            if (mins < 1440) return $"{(int)(mins / 60)}h {(int)(mins % 60)}m ago";
            return lastHeardUtc.ToLocalTime().ToString("MM/dd HH:mm");
        }
    }

    public string CheckedInAtText =>
        checkedInAt.HasValue
            ? checkedInAt.Value.ToLocalTime().ToString("HH:mm:ss")
            : string.Empty;

    public string PositionText =>
        latitude.HasValue && longitude.HasValue
            ? $"{latitude:F4}°, {longitude:F4}°"
            : "No position";

    public string DisplayName =>
        string.IsNullOrWhiteSpace(TacticalLabel) ? Callsign : $"{TacticalLabel} ({Callsign})";

    public string ResourceStatusLabel => resourceStatus switch
    {
        ResourceStatus.Available    => "Available",
        ResourceStatus.Assigned     => "Assigned",
        ResourceStatus.OnScene      => "On Scene",
        ResourceStatus.Returning    => "Returning",
        ResourceStatus.OutOfService => "Out of Service",
        _                           => "Available"
    };

    public string ResourceStatusColor => resourceStatus switch
    {
        ResourceStatus.Available    => "#15803d",   // green
        ResourceStatus.Assigned     => "#1d4ed8",   // blue
        ResourceStatus.OnScene      => "#7c3aed",   // purple
        ResourceStatus.Returning    => "#b45309",   // amber
        ResourceStatus.OutOfService => "#6b7280",   // gray
        _                           => "#15803d"
    };

    public string ResourceStatusIcon => resourceStatus switch
    {
        ResourceStatus.Available    => "●",
        ResourceStatus.Assigned     => "→",
        ResourceStatus.OnScene      => "★",
        ResourceStatus.Returning    => "←",
        ResourceStatus.OutOfService => "○",
        _                           => "●"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
