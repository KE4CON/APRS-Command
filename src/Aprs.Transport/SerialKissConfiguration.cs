namespace Aprs.Transport;

public sealed record SerialKissConfiguration(
    string PortName,
    int BaudRate,
    int DataBits,
    SerialKissParity Parity,
    SerialKissStopBits StopBits,
    SerialKissHandshake Handshake,
    bool Enabled,
    bool ReceiveEnabled,
    bool TransmitEnabled,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    string SourceName,
    TimeSpan ReadTimeout,
    TimeSpan WriteTimeout)
{
    public static SerialKissConfiguration Default { get; } = new(
        PortName: string.Empty,
        BaudRate: 9600,
        DataBits: 8,
        Parity: SerialKissParity.None,
        StopBits: SerialKissStopBits.One,
        Handshake: SerialKissHandshake.None,
        Enabled: false,
        ReceiveEnabled: true,
        TransmitEnabled: false,
        ReconnectEnabled: true,
        ReconnectDelay: TimeSpan.FromSeconds(5),
        SourceName: "Serial KISS TNC",
        ReadTimeout: TimeSpan.FromSeconds(30),
        WriteTimeout: TimeSpan.FromSeconds(10));
}
