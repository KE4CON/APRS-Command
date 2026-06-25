using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Operator-facing iGate configuration. Exposes the enable toggle and gating options;
/// saving writes to the settings store and also calls <see cref="IIGateService.SetEnabled"/>
/// so the change takes effect immediately without a restart.
/// </summary>
public sealed class IGateConfigViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private readonly IIGateService? iGateService;

    private bool enabled;
    private bool rfToAprsIsGatingEnabled;
    private bool aprsIsTransmitEnabled;
    private bool gatePositionPackets;
    private bool gateWeatherPackets;
    private bool gateMessages;
    private bool gateObjectItemPackets;
    private string statusText = string.Empty;

    public IGateConfigViewModel(IAppSettingsStore store, IIGateService? iGateService = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.iGateService = iGateService;
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

    public bool RfToAprsIsGatingEnabled
    {
        get => rfToAprsIsGatingEnabled;
        set { if (rfToAprsIsGatingEnabled != value) { rfToAprsIsGatingEnabled = value; OnPropertyChanged(); } }
    }

    public bool AprsIsTransmitEnabled
    {
        get => aprsIsTransmitEnabled;
        set { if (aprsIsTransmitEnabled != value) { aprsIsTransmitEnabled = value; OnPropertyChanged(); } }
    }

    public bool GatePositionPackets
    {
        get => gatePositionPackets;
        set { if (gatePositionPackets != value) { gatePositionPackets = value; OnPropertyChanged(); } }
    }

    public bool GateWeatherPackets
    {
        get => gateWeatherPackets;
        set { if (gateWeatherPackets != value) { gateWeatherPackets = value; OnPropertyChanged(); } }
    }

    public bool GateMessages
    {
        get => gateMessages;
        set { if (gateMessages != value) { gateMessages = value; OnPropertyChanged(); } }
    }

    public bool GateObjectItemPackets
    {
        get => gateObjectItemPackets;
        set { if (gateObjectItemPackets != value) { gateObjectItemPackets = value; OnPropertyChanged(); } }
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
        var s = store.Load().IGate;
        Enabled                = s.Enabled;
        RfToAprsIsGatingEnabled = s.RfToAprsIsGatingEnabled;
        AprsIsTransmitEnabled  = s.AprsIsTransmitEnabled;
        GatePositionPackets    = s.GatePositionPackets;
        GateWeatherPackets     = s.GateWeatherPackets;
        GateMessages           = s.GateMessages;
        GateObjectItemPackets  = s.GateObjectItemPackets;
        StatusText = "Loaded.";
    }

    public void Save()
    {
        var settings = new IGateSettings(
            Enabled:                Enabled,
            RfToAprsIsGatingEnabled: RfToAprsIsGatingEnabled,
            AprsIsTransmitEnabled:  AprsIsTransmitEnabled,
            GatePositionPackets:    GatePositionPackets,
            GateWeatherPackets:     GateWeatherPackets,
            GateMessages:           GateMessages,
            GateObjectItemPackets:  GateObjectItemPackets);

        store.Update(s => s with { IGate = settings });

        // Apply immediately to the running service — no restart needed.
        iGateService?.SetEnabled(Enabled);

        StatusText = Enabled
            ? "iGating enabled. RF packets will be gated to APRS-IS."
            : "iGating disabled. No packets will be gated.";
    }

    public static IGateConfigViewModel CreateDesignTime()
        => new(new InMemoryAppSettingsStore());

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
