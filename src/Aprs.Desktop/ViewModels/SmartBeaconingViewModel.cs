using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Smart beaconing settings editor. SmartBeaconing adjusts the beacon interval
/// based on speed and course changes — fast beacons when moving quickly or turning,
/// slow beacons when stationary. Requires GPS to be active.
/// </summary>
public sealed class SmartBeaconingViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private bool enabled;
    private double lowSpeedThresholdKnots;
    private double highSpeedThresholdKnots;
    private int slowRateMinutes;
    private int fastRateMinutes;
    private double minimumTurnAngleDegrees;
    private bool enabledForAprsIs;
    private bool enabledForRf;
    private string statusText = string.Empty;

    public SmartBeaconingViewModel(IAppSettingsStore store)
    {
        this.store = store;
        var s = store.Load().SmartBeaconing;
        enabled                  = s.Enabled;
        lowSpeedThresholdKnots   = s.LowSpeedThresholdKnots;
        highSpeedThresholdKnots  = s.HighSpeedThresholdKnots;
        slowRateMinutes          = s.SlowRateMinutes;
        fastRateMinutes          = s.FastRateMinutes;
        minimumTurnAngleDegrees  = s.MinimumTurnAngleDegrees;
        enabledForAprsIs         = s.EnabledForAprsIs;
        enabledForRf             = s.EnabledForRf;
        SaveCommand              = new DesktopCommand(Save);
        ResetDefaultsCommand     = new DesktopCommand(ResetDefaults);
    }

    public bool Enabled
    {
        get => enabled;
        set { if (enabled != value) { enabled = value; OnPropertyChanged(); } }
    }

    public double LowSpeedThresholdKnots
    {
        get => lowSpeedThresholdKnots;
        set { if (lowSpeedThresholdKnots != value) { lowSpeedThresholdKnots = value; OnPropertyChanged(); } }
    }

    public double HighSpeedThresholdKnots
    {
        get => highSpeedThresholdKnots;
        set { if (highSpeedThresholdKnots != value) { highSpeedThresholdKnots = value; OnPropertyChanged(); } }
    }

    public int SlowRateMinutes
    {
        get => slowRateMinutes;
        set { if (slowRateMinutes != value) { slowRateMinutes = value; OnPropertyChanged(); } }
    }

    public int FastRateMinutes
    {
        get => fastRateMinutes;
        set { if (fastRateMinutes != value) { fastRateMinutes = value; OnPropertyChanged(); } }
    }

    public double MinimumTurnAngleDegrees
    {
        get => minimumTurnAngleDegrees;
        set { if (minimumTurnAngleDegrees != value) { minimumTurnAngleDegrees = value; OnPropertyChanged(); } }
    }

    public bool EnabledForAprsIs
    {
        get => enabledForAprsIs;
        set { if (enabledForAprsIs != value) { enabledForAprsIs = value; OnPropertyChanged(); } }
    }

    public bool EnabledForRf
    {
        get => enabledForRf;
        set { if (enabledForRf != value) { enabledForRf = value; OnPropertyChanged(); } }
    }

    public string StatusText
    {
        get => statusText;
        private set { if (statusText != value) { statusText = value; OnPropertyChanged(); } }
    }

    public DesktopCommand SaveCommand         { get; }
    public DesktopCommand ResetDefaultsCommand { get; }

    private void Save()
    {
        store.Update(s => s with
        {
            SmartBeaconing = new SmartBeaconingSettings(
                Enabled:                 Enabled,
                LowSpeedThresholdKnots:  LowSpeedThresholdKnots,
                HighSpeedThresholdKnots: HighSpeedThresholdKnots,
                SlowRateMinutes:         SlowRateMinutes,
                FastRateMinutes:         FastRateMinutes,
                MinimumTurnAngleDegrees: MinimumTurnAngleDegrees,
                EnabledForAprsIs:        EnabledForAprsIs,
                EnabledForRf:            EnabledForRf)
        });
        StatusText = "Smart beaconing settings saved. Restart required for changes to take effect.";
    }

    private void ResetDefaults()
    {
        var d = SmartBeaconingSettings.Default;
        Enabled                 = d.Enabled;
        LowSpeedThresholdKnots  = d.LowSpeedThresholdKnots;
        HighSpeedThresholdKnots = d.HighSpeedThresholdKnots;
        SlowRateMinutes         = d.SlowRateMinutes;
        FastRateMinutes         = d.FastRateMinutes;
        MinimumTurnAngleDegrees = d.MinimumTurnAngleDegrees;
        EnabledForAprsIs        = d.EnabledForAprsIs;
        EnabledForRf            = d.EnabledForRf;
        StatusText              = "Defaults restored — click Save to apply.";
    }

    public static SmartBeaconingViewModel CreateDesignTime()
        => new(new InMemoryAppSettingsStore());

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
