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

/// <summary>A single station entry in the net control roster.</summary>
public sealed class NetControlRosterEntry : INotifyPropertyChanged
{
    private CheckInStatus status = CheckInStatus.NotCheckedIn;
    private string notes = string.Empty;
    private DateTimeOffset? checkedInAt;
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
