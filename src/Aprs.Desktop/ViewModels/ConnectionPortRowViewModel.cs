using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;
using Aprs.Transport;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Editable view of a single <see cref="ConnectionPort"/> in the Connections list. Exposes the
/// common fields (name, enable / receive / transmit) plus the primary settings for the port's type;
/// the applicability flags (<see cref="IsNetwork"/> etc.) let the view show only the relevant fields.
/// </summary>
public sealed class ConnectionPortRowViewModel : INotifyPropertyChanged
{
    private ConnectionPort port;

    public ConnectionPortRowViewModel(ConnectionPort port)
        => this.port = port.Normalized();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id => port.Id;

    public ConnectionPortType Type => port.Type;

    public string TypeDisplay => port.Type switch
    {
        ConnectionPortType.AprsIs => "APRS-IS",
        ConnectionPortType.NetworkTncKiss => "Network TNC (KISS-TCP)",
        ConnectionPortType.Agwpe => "AGWPE (TCP)",
        ConnectionPortType.SerialKiss => "Serial KISS (hardware TNC)",
        ConnectionPortType.ManagedLocalModem => "Managed local modem (sound card)",
        _ => port.Type.ToString()
    };

    public string Name { get => port.Name; set => Update(port with { Name = value }); }

    public bool Enabled { get => port.Enabled; set => Update(port with { Enabled = value }); }

    public bool ReceiveEnabled { get => port.ReceiveEnabled; set => Update(port with { ReceiveEnabled = value }); }

    public bool TransmitEnabled { get => port.TransmitEnabled; set => Update(port with { TransmitEnabled = value }); }

    // --- Applicability flags for the view -------------------------------------------------------
    public bool IsNetwork => port.Type is ConnectionPortType.NetworkTncKiss or ConnectionPortType.ManagedLocalModem;
    public bool IsAgwpe => port.Type == ConnectionPortType.Agwpe;
    public bool IsSerial => port.Type == ConnectionPortType.SerialKiss;
    public bool IsAprsIs => port.Type == ConnectionPortType.AprsIs;

    // --- Network TNC (KISS-TCP) / managed local modem (loopback KISS-TCP) -----------------------
    public string NetworkHost
    {
        get => Tcp.Host;
        set => Update(port with { Configuration = port.Configuration with { NetworkTncKiss = Tcp with { Host = value } } });
    }

    public int NetworkPort
    {
        get => Tcp.Port;
        set => Update(port with { Configuration = port.Configuration with { NetworkTncKiss = Tcp with { Port = value } } });
    }

    // --- AGWPE ----------------------------------------------------------------------------------
    public string AgwpeHost
    {
        get => Agw.Host;
        set => Update(port with { Configuration = port.Configuration with { Agwpe = Agw with { Host = value } } });
    }

    public int AgwpePort
    {
        get => Agw.Port;
        set => Update(port with { Configuration = port.Configuration with { Agwpe = Agw with { Port = value } } });
    }

    // --- Serial KISS ----------------------------------------------------------------------------
    public string SerialPortName
    {
        get => Serial.PortName;
        set => Update(port with { Configuration = port.Configuration with { SerialKiss = Serial with { PortName = value } } });
    }

    public int BaudRate
    {
        get => Serial.BaudRate;
        set => Update(port with { Configuration = port.Configuration with { SerialKiss = Serial with { BaudRate = value } } });
    }

    // --- APRS-IS --------------------------------------------------------------------------------
    public string AprsIsServer
    {
        get => Is.ServerHost;
        set => Update(port with { Configuration = port.Configuration with { AprsIs = Is with { ServerHost = value } } });
    }

    public int AprsIsPort
    {
        get => Is.ServerPort;
        set => Update(port with { Configuration = port.Configuration with { AprsIs = Is with { ServerPort = value } } });
    }

    public string AprsIsPasscode
    {
        get => Is.Passcode;
        set => Update(port with { Configuration = port.Configuration with { AprsIs = Is with { Passcode = value } } });
    }

    public string AprsIsFilter
    {
        get => Is.Filter ?? string.Empty;
        set => Update(port with { Configuration = port.Configuration with { AprsIs = Is with { Filter = value } } });
    }

    /// <summary>Serial ports available on this machine, for the serial port name dropdown.</summary>
    public IReadOnlyList<string> AvailableSerialPorts { get; } =
        new SerialPortDiscovery().GetAvailablePortNames();

    /// <summary>The current edited port, with its type-specific configuration guaranteed present.</summary>
    public ConnectionPort ToModel() => port.Normalized();

    private TcpKissConfiguration Tcp => port.Configuration.NetworkTncKiss ?? TcpKissConfiguration.Default;
    private AgwpeConfiguration Agw => port.Configuration.Agwpe ?? AgwpeConfiguration.Default;
    private SerialKissConfiguration Serial => port.Configuration.SerialKiss ?? SerialKissConfiguration.Default;
    private AprsIsClientConfiguration Is => port.Configuration.AprsIs ?? AprsIsClientConfiguration.Default;

    private void Update(ConnectionPort updated)
    {
        port = updated;
        OnPropertyChanged(string.Empty); // refresh all bound fields; cheap for a single row
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
