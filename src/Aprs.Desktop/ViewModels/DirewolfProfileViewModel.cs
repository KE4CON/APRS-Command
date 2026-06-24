using System.ComponentModel;
using Aprs.Transport;

namespace Aprs.Desktop.ViewModels;

public sealed class DirewolfProfileViewModel : INotifyPropertyChanged
{
    private readonly DirewolfProfileService profileService;
    private DirewolfProfile profile;
    private string connectionStatus;

    public DirewolfProfileViewModel(DirewolfProfileService profileService)
    {
        this.profileService = profileService;
        profile = profileService.GetCurrentProfile();
        connectionStatus = "Connection test not run.";
        TestConnectionCommand = new DesktopCommand(ValidateConnectionSettings);
        ResetCommand = new DesktopCommand(Reset);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ProfileName
    {
        get => profile.ProfileName;
        set => UpdateProfile(profile with { ProfileName = value });
    }

    public string Host
    {
        get => profile.Host;
        set => UpdateProfile(profile with { Host = value });
    }

    public int KissPort
    {
        get => profile.KissPort;
        set => UpdateProfile(profile with { KissPort = value });
    }

    public bool Enabled
    {
        get => profile.Enabled;
        set => UpdateProfile(profile with { Enabled = value });
    }

    public bool ReceiveEnabled
    {
        get => profile.ReceiveEnabled;
        set => UpdateProfile(profile with { ReceiveEnabled = value });
    }

    public bool TransmitEnabled
    {
        get => profile.TransmitEnabled;
        set => UpdateProfile(profile with { TransmitEnabled = value });
    }

    public bool AutoReconnect
    {
        get => profile.AutoReconnect;
        set => UpdateProfile(profile with { AutoReconnect = value });
    }

    public string SourceName
    {
        get => profile.SourceName;
        set => UpdateProfile(profile with { SourceName = value });
    }

    public string Notes
    {
        get => profile.Notes ?? string.Empty;
        set => UpdateProfile(profile with { Notes = value });
    }

    public string ConnectionStatus
    {
        get => connectionStatus;
        private set
        {
            if (connectionStatus == value)
            {
                return;
            }

            connectionStatus = value;
            OnPropertyChanged(nameof(ConnectionStatus));
        }
    }

    public IReadOnlyList<string> SetupNotes => DirewolfSetupNotes.Notes;

    public string SafetySummary => profile.TransmitEnabled
        ? "RF transmit requires separate safety approval before it can be used."
        : "RF transmit is disabled.";

    public DesktopCommand TestConnectionCommand { get; }

    public DesktopCommand ResetCommand { get; }

    public DirewolfProfile CurrentProfile => profile;

    public static DirewolfProfileViewModel CreateDesignTime()
    {
        return new DirewolfProfileViewModel(new DirewolfProfileService(new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero)));
    }

    private void ValidateConnectionSettings()
    {
        var validation = profileService.ValidateProfile(profile);
        ConnectionStatus = validation.Errors.Count == 0
            ? "Profile settings are valid. Start Direwolf before testing the live TCP connection."
            : string.Join(" ", validation.Errors);
    }

    private void Reset()
    {
        profile = profileService.ResetToDefaults(DateTimeOffset.UtcNow);
        ConnectionStatus = "Profile reset to safe defaults.";
        OnPropertyChanged(string.Empty);
    }

    private void UpdateProfile(DirewolfProfile updated)
    {
        profile = profileService.UpdateProfile(updated, DateTimeOffset.UtcNow);
        OnPropertyChanged(string.Empty);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
