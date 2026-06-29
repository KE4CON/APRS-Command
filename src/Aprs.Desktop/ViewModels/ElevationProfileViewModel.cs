using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class ElevationProfileViewModel : INotifyPropertyChanged
{
    private readonly IStationDatabase stationDatabase;
    private readonly Services.ElevationProfileService elevationService;

    private string fromCallsign = string.Empty;
    private string toCallsign   = string.Empty;
    private string profileText  = string.Empty;
    private string statusText   = string.Empty;
    private bool isLoading;

    public ElevationProfileViewModel(IStationDatabase stationDatabase)
    {
        this.stationDatabase = stationDatabase;
        elevationService     = new Services.ElevationProfileService();
        GenerateCommand      = new DesktopCommand(async () => await GenerateAsync());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FromCallsign
    {
        get => fromCallsign;
        set { if (fromCallsign != value) { fromCallsign = value; OnPropertyChanged(); } }
    }

    public string ToCallsign
    {
        get => toCallsign;
        set { if (toCallsign != value) { toCallsign = value; OnPropertyChanged(); } }
    }

    public string ProfileText
    {
        get => profileText;
        private set { if (profileText != value) { profileText = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public bool IsLoading
    {
        get => isLoading;
        private set { if (isLoading != value) { isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotLoading)); } }
    }

    public bool IsNotLoading => !isLoading;

    public DesktopCommand GenerateCommand { get; }

    private async Task GenerateAsync()
    {
        var from = FromCallsign.Trim().ToUpperInvariant();
        var to   = ToCallsign.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            StatusText = "Enter both callsigns to generate a profile.";
            return;
        }

        var fromStation = stationDatabase.GetStation(from);
        var toStation   = stationDatabase.GetStation(to);

        if (fromStation?.Latitude is null || fromStation.Longitude is null)
        {
            StatusText = $"{from} has no position data.";
            return;
        }
        if (toStation?.Latitude is null || toStation.Longitude is null)
        {
            StatusText = $"{to} has no position data.";
            return;
        }

        IsLoading  = true;
        StatusText = "Fetching elevation data from Open Elevation API…";
        ProfileText = string.Empty;

        try
        {
            var profile = await elevationService.GetProfileAsync(
                fromStation.Latitude.Value, fromStation.Longitude.Value,
                toStation.Latitude.Value, toStation.Longitude.Value).ConfigureAwait(false);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (profile is null)
                {
                    StatusText = "Could not fetch elevation data — check internet connection.";
                    return;
                }

                ProfileText = profile.ToAsciiChart();
                StatusText  = $"{from} → {to}  ·  {profile.TotalDistanceKm:F1} km  ·  " +
                              $"Min {profile.MinElevationM:F0} m  ·  Max {profile.MaxElevationM:F0} m  ·  " +
                              $"Data: Open Elevation API";
            });
        }
        catch (Exception ex)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                StatusText = $"Elevation fetch failed: {ex.Message}");
        }
        finally
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => IsLoading = false);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
