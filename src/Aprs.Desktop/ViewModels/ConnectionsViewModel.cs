using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Manages the operator's list of connection ports: loads them from the settings store, supports
/// add / remove / edit, and saves the list back. This is the logic behind the Connections settings
/// screen; the view binds to <see cref="Ports"/>, <see cref="SelectedPort"/>, and the commands.
/// </summary>
public sealed class ConnectionsViewModel : INotifyPropertyChanged
{
    private readonly IAppSettingsStore store;
    private ConnectionPortRowViewModel? selectedPort;
    private ConnectionPortType newPortType = ConnectionPortType.AprsIs;
    private string statusText = string.Empty;
    private string repeaterBookApiToken = string.Empty;
    private int repeaterBookRadiusMiles = 25;
    private int addCounter;

    public ConnectionsViewModel(IAppSettingsStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));

        Ports = new ObservableCollection<ConnectionPortRowViewModel>();
        AddPortCommand = new DesktopCommand(AddPort);
        RemoveSelectedPortCommand = new DesktopCommand(RemoveSelectedPort, () => SelectedPort is not null);
        SaveCommand = new DesktopCommand(Save);
        RevertCommand = new DesktopCommand(Load);

        Load();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ConnectionPortRowViewModel> Ports { get; }

    /// <summary>The connection types the operator can add, for the type picker.</summary>
    public IReadOnlyList<ConnectionPortType> AvailablePortTypes { get; } =
    [
        ConnectionPortType.AprsIs,
        ConnectionPortType.NetworkTncKiss,
        ConnectionPortType.Agwpe,
        ConnectionPortType.SerialKiss,
        ConnectionPortType.ManagedLocalModem
    ];

    public ConnectionPortType NewPortType
    {
        get => newPortType;
        set
        {
            if (newPortType == value)
            {
                return;
            }

            newPortType = value;
            OnPropertyChanged();
        }
    }

    public ConnectionPortRowViewModel? SelectedPort
    {
        get => selectedPort;
        set
        {
            if (ReferenceEquals(selectedPort, value))
            {
                return;
            }

            selectedPort = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            RemoveSelectedPortCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>True when a port is selected, so the view can show/enable the editor panel.</summary>
    public bool HasSelection => selectedPort is not null;

    public string StatusText
    {
        get => statusText;
        private set
        {
            if (statusText == value)
            {
                return;
            }

            statusText = value;
            OnPropertyChanged();
        }
    }

    public string RepeaterBookApiToken
    {
        get => repeaterBookApiToken;
        set { repeaterBookApiToken = value; OnPropertyChanged(); }
    }

    public int RepeaterBookRadiusMiles
    {
        get => repeaterBookRadiusMiles;
        set { repeaterBookRadiusMiles = Math.Max(5, Math.Min(250, value)); OnPropertyChanged(); }
    }

    public DesktopCommand AddPortCommand { get; }

    public DesktopCommand RemoveSelectedPortCommand { get; }

    public DesktopCommand SaveCommand { get; }

    public DesktopCommand RevertCommand { get; }

    /// <summary>(Re)loads the port list from the saved settings, discarding unsaved edits.</summary>
    public void Load()
    {
        var settings = store.Load();
        Ports.Clear();
        foreach (var port in settings.Connections.Normalized().Ports)
        {
            Ports.Add(new ConnectionPortRowViewModel(port));
        }

        SelectedPort = Ports.FirstOrDefault();
        RepeaterBookApiToken    = settings.RepeaterBook.ApiToken ?? string.Empty;
        RepeaterBookRadiusMiles = settings.RepeaterBook.SearchRadiusMiles;
        StatusText = $"{Ports.Count} port(s) loaded.";
    }

    /// <summary>Writes the current list of ports back to the settings store.</summary>
    public void Save()
    {
        var ports = Ports.Select(row => row.ToModel()).ToList();
        var rbSettings = new RepeaterBookSettings(
            ApiToken:         string.IsNullOrWhiteSpace(RepeaterBookApiToken) ? null : RepeaterBookApiToken.Trim(),
            SearchRadiusMiles: RepeaterBookRadiusMiles);
        store.Update(settings => settings with
        {
            Connections  = new ConnectionSettings(ports),
            RepeaterBook = rbSettings
        });
        StatusText = $"Saved {ports.Count} port(s).";
    }

    private void AddPort()
    {
        var id = $"port-{++addCounter}-{Guid.NewGuid():N}"[..16];
        var name = NewPortType switch
        {
            ConnectionPortType.AprsIs => "APRS-IS",
            ConnectionPortType.NetworkTncKiss => "Network TNC",
            ConnectionPortType.Agwpe => "AGWPE",
            ConnectionPortType.SerialKiss => "Hardware TNC",
            ConnectionPortType.ManagedLocalModem => "Local modem",
            _ => "Port"
        };

        var port = new ConnectionPort(
            Id: id,
            Name: name,
            Type: NewPortType,
            Enabled: false,
            ReceiveEnabled: true,
            TransmitEnabled: false,
            Configuration: new PortConfiguration().EnsureFor(NewPortType));

        var row = new ConnectionPortRowViewModel(port);
        Ports.Add(row);
        SelectedPort = row;
        StatusText = $"Added a {row.TypeDisplay} port. Save to keep it.";
    }

    private void RemoveSelectedPort()
    {
        if (SelectedPort is null)
        {
            return;
        }

        var removed = SelectedPort;
        var index = Ports.IndexOf(removed);
        Ports.Remove(removed);
        SelectedPort = Ports.Count == 0 ? null : Ports[Math.Min(index, Ports.Count - 1)];
        StatusText = $"Removed \"{removed.Name}\". Save to keep the change.";
    }

    public static ConnectionsViewModel CreateDesignTime()
    {
        var store = new InMemoryAppSettingsStore(AppSettings.Default with
        {
            Connections = new ConnectionSettings(
            [
                ConnectionPort.DefaultAprsIs(),
                new ConnectionPort(
                    "design-rf", "Field radio", ConnectionPortType.SerialKiss,
                    Enabled: true, ReceiveEnabled: true, TransmitEnabled: false,
                    PortConfiguration.ForSerialKiss(Aprs.Transport.SerialKissConfiguration.Default))
            ])
        });

        return new ConnectionsViewModel(store);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
