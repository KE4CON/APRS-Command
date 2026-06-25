namespace Aprs.Desktop.Configuration;

/// <summary>
/// The kind of a connection port. Each value selects which configuration in
/// <see cref="PortConfiguration"/> is authoritative for that port.
/// </summary>
public enum ConnectionPortType
{
    /// <summary>APRS-IS internet connection.</summary>
    AprsIs,

    /// <summary>KISS over TCP to a software modem already running (GrayWolf, or a Direwolf you manage).</summary>
    NetworkTncKiss,

    /// <summary>AGWPE / AGW host-mode over TCP.</summary>
    Agwpe,

    /// <summary>Hardware TNC over a serial port (KISS).</summary>
    SerialKiss,

    /// <summary>
    /// APRS Command launches and configures a local Direwolf and connects to it (sound-card path for
    /// SignaLink / DigiRig). Its audio-device + PTT configuration is added with the managed-modem
    /// feature; until then a managed port carries its loopback KISS-TCP settings.
    /// </summary>
    ManagedLocalModem
}
