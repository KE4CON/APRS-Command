using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WinlinkGatewaysViewModel : INotifyPropertyChanged
{
    private readonly Services.WinlinkRmsGatewayService service;
    private readonly IAppSettingsStore settingsStore;
    private readonly ILocalStationProfileService profileService;

    private string apiKeyInput = string.Empty;
    private string statusText  = string.Empty;
    private bool isQuerying;

    public WinlinkGatewaysViewModel(
        IAppSettingsStore settingsStore,
        ILocalStationProfileService profileService)
    {
        this.settingsStore  = settingsStore;
        this.profileService = profileService;

        var apiKey = settingsStore.Load().Winlink.ApiKey;
        service    = new Services.WinlinkRmsGatewayService(apiKey);
        ApiKeyInput = apiKey ?? string.Empty;

        Gateways      = [];
        QueryCommand  = new DesktopCommand(async () => await QueryAsync());
        SaveKeyCommand = new DesktopCommand(SaveApiKey);

        if (!service.IsConfigured)
        {
            StatusText = "No Winlink API key configured. Request one from a Winlink " +
                         "administrator at api.winlink.org, then enter it below.";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WinlinkGatewayRowViewModel> Gateways { get; }

    public string ApiKeyInput
    {
        get => apiKeyInput;
        set { if (apiKeyInput != value) { apiKeyInput = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public bool IsQuerying
    {
        get => isQuerying;
        private set { if (isQuerying != value) { isQuerying = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotQuerying)); } }
    }

    public bool IsNotQuerying => !isQuerying;

    public bool IsConfigured => service.IsConfigured;

    public DesktopCommand QueryCommand    { get; }
    public DesktopCommand SaveKeyCommand  { get; }

    private void SaveApiKey()
    {
        var key = ApiKeyInput.Trim();
        settingsStore.Update(s => s with
        {
            Winlink = new WinlinkSettings(string.IsNullOrWhiteSpace(key) ? null : key, !string.IsNullOrWhiteSpace(key))
        });
        StatusText = string.IsNullOrWhiteSpace(key)
            ? "API key cleared."
            : "API key saved. Restart the Winlink Gateways window to use it, or click Query Nearby Gateways.";
        OnPropertyChanged(nameof(IsConfigured));
    }

    private async Task QueryAsync()
    {
        var profile = profileService.GetCurrentProfile();
        var lat = profile.FixedLatitude;
        var lon = profile.FixedLongitude;

        if (lat is null || lon is null)
        {
            StatusText = "Set your station's fixed position in Station Setup first.";
            return;
        }

        IsQuerying = true;
        StatusText = "Querying nearby RMS gateways…";
        Gateways.Clear();

        var liveService = new Services.WinlinkRmsGatewayService(
            settingsStore.Load().Winlink.ApiKey);

        var result = await liveService.QueryNearbyGatewaysAsync(lat.Value, lon.Value)
                                      .ConfigureAwait(false);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsQuerying = false;

            if (result.NeedsKey)
            {
                StatusText = result.Message ?? "API key required.";
                return;
            }

            if (!result.Success)
            {
                StatusText = result.Message ?? "Query failed.";
                return;
            }

            foreach (var gw in result.Gateways)
                Gateways.Add(new WinlinkGatewayRowViewModel(gw));

            StatusText = Gateways.Count == 0
                ? "No RMS gateways found in range."
                : $"{Gateways.Count} gateway(s) found.";
        });
    }

    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public sealed class WinlinkGatewayRowViewModel
{
    public WinlinkGatewayRowViewModel(Services.WinlinkGateway gw)
    {
        Callsign  = gw.Callsign;
        Frequency = gw.Frequency;
        Mode      = gw.Mode;
        Position  = $"{gw.Latitude:F4}°, {gw.Longitude:F4}°";
    }

    public string Callsign  { get; }
    public string Frequency { get; }
    public string Mode      { get; }
    public string Position  { get; }
}
