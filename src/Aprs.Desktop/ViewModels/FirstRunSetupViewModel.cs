using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class FirstRunSetupViewModel : INotifyPropertyChanged
{
    private readonly FirstRunSetupService setupService;

    public FirstRunSetupViewModel(FirstRunSetupConfiguration configuration, FirstRunSetupService? setupService = null)
    {
        Configuration = configuration;
        this.setupService = setupService ?? new FirstRunSetupService();
        Layout = ApplicationFolderLayout.FromRoot(configuration.ApplicationDataFolderPath);
        FinishSetupCommand = new DesktopCommand(FinishSetup);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FirstRunSetupConfiguration Configuration { get; private set; }

    public ApplicationFolderLayout Layout { get; }

    public DesktopCommand FinishSetupCommand { get; }

    public string Title => "Welcome to APRS Command";

    public string StatusText => Configuration.FirstRunCompleted
        ? "First-run setup marked complete."
        : "First-run setup is not complete.";

    public string ApplicationDataFolderPath => Configuration.ApplicationDataFolderPath;

    public string LogsFolderPath => Configuration.LogsFolderPath;

    public string MapCacheFolderPath => Configuration.MapCacheFolderPath;

    public string ExportFolderPath => Configuration.ExportFolderPath;

    public IReadOnlyList<string> SafetyDefaults =>
    [
        "APRS Command does not transmit by default.",
        "APRS-IS transmit is disabled until configured.",
        "RF transmit is disabled until configured.",
        "iGate and digipeater modes are disabled until configured.",
        "Beaconing and weather beaconing are disabled until configured.",
        "REST API, WebSocket streams, file hooks, and plugin loading are disabled until configured."
    ];

    public bool TransmitDisabled => !Configuration.TransmitEnabled
        && !Configuration.AprsIsTransmitEnabled
        && !Configuration.RfTransmitEnabled;

    public bool ExtensionInputsDisabled => !Configuration.RestApiEnabled
        && !Configuration.WebSocketEnabled
        && !Configuration.FileHooksEnabled
        && !Configuration.PluginLoadingEnabled;

    public static FirstRunSetupViewModel CreateDesignTime()
    {
        return new FirstRunSetupViewModel(FirstRunSetupConfiguration.CreateDefault(
            new DateTimeOffset(2026, 6, 12, 12, 0, 0, TimeSpan.Zero)));
    }

    private void FinishSetup()
    {
        Configuration = setupService.MarkCompleted(Configuration, DateTimeOffset.UtcNow);
        OnPropertyChanged(nameof(Configuration));
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
