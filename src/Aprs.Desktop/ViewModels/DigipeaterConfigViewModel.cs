using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Operator-facing digipeater configuration. Exposes the enable toggle, mode (fill-in vs full),
/// supported aliases, and transmit port. Saving writes to the settings store and calls
/// <see cref="IDigipeaterService.SetEnabled"/> so the change takes effect immediately.
/// </summary>
public sealed class DigipeaterConfigViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private readonly IDigipeaterService? digipeaterService;

    private bool enabled;
    private bool rfTransmitEnabled;
    private string digipeaterCallsign = string.Empty;
    private bool fillInMode;
    private bool fullMode;
    private string supportedAliases = string.Empty;
    private string rfTransmitPort = string.Empty;
    private string statusText = string.Empty;

    public DigipeaterConfigViewModel(IAppSettingsStore store, IDigipeaterService? digipeaterService = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.digipeaterService = digipeaterService;
        SaveCommand   = new DesktopCommand(Save);
        RevertCommand = new DesktopCommand(Load);
        Load();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Enabled
    {
        get => enabled;
        set { if (enabled != value) { enabled = value; OnPropertyChanged(); } }
    }

    public bool RfTransmitEnabled
    {
        get => rfTransmitEnabled;
        set { if (rfTransmitEnabled != value) { rfTransmitEnabled = value; OnPropertyChanged(); } }
    }

    public string DigipeaterCallsign
    {
        get => digipeaterCallsign;
        set { if (digipeaterCallsign != value) { digipeaterCallsign = value; OnPropertyChanged(); } }
    }

    public bool FillInMode
    {
        get => fillInMode;
        set { if (fillInMode != value) { fillInMode = value; OnPropertyChanged(); } }
    }

    public bool FullMode
    {
        get => fullMode;
        set { if (fullMode != value) { fullMode = value; OnPropertyChanged(); } }
    }

    public string SupportedAliases
    {
        get => supportedAliases;
        set { if (supportedAliases != value) { supportedAliases = value; OnPropertyChanged(); } }
    }

    public string RfTransmitPort
    {
        get => rfTransmitPort;
        set { if (rfTransmitPort != value) { rfTransmitPort = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public DesktopCommand SaveCommand { get; }
    public DesktopCommand RevertCommand { get; }

    public void Load()
    {
        var s = store.Load().Digipeater;
        Enabled              = s.Enabled;
        RfTransmitEnabled    = s.RfTransmitEnabled;
        DigipeaterCallsign   = s.DigipeaterCallsign;
        FillInMode           = s.FillInMode;
        FullMode             = s.FullMode;
        SupportedAliases     = s.SupportedAliases;
        RfTransmitPort       = s.RfTransmitPort ?? string.Empty;
        StatusText = "Loaded.";
    }

    public void Save()
    {
        var settings = new DigipeaterSettings(
            Enabled:           Enabled,
            RfTransmitEnabled: RfTransmitEnabled,
            DigipeaterCallsign: DigipeaterCallsign.Trim().ToUpperInvariant(),
            FillInMode:        FillInMode,
            FullMode:          FullMode,
            SupportedAliases:  SupportedAliases.Trim(),
            RfTransmitPort:    string.IsNullOrWhiteSpace(RfTransmitPort) ? null : RfTransmitPort.Trim());

        store.Update(s => s with { Digipeater = settings });

        // Apply immediately to the running service — no restart needed.
        digipeaterService?.SetEnabled(Enabled);

        StatusText = Enabled
            ? $"Digipeater enabled ({(FillInMode ? "fill-in" : "full")} mode). Listening for packets to repeat."
            : "Digipeater disabled. No packets will be repeated.";
    }

    public static DigipeaterConfigViewModel CreateDesignTime()
        => new(new InMemoryAppSettingsStore());

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
