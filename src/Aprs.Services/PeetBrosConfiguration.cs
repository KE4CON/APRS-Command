using Aprs.Transport;

namespace Aprs.Services;

public sealed record PeetBrosConfiguration(
    bool Enabled,
    string DriverId,
    string SourceName,
    string SerialPortName,
    int BaudRate,
    int DataBits,
    SerialKissParity Parity,
    SerialKissStopBits StopBits,
    TimeSpan ReadTimeout,
    bool ReconnectEnabled,
    TimeSpan ReconnectDelay,
    TimeSpan StaleDataThreshold,
    string? ModelName,
    string? Notes)
{
    public static PeetBrosConfiguration Default { get; } = new(
        Enabled: false,
        DriverId: "peet-bros-ultimeter",
        SourceName: "Peet Bros ULTIMETER",
        SerialPortName: string.Empty,
        BaudRate: 2400,
        DataBits: 8,
        Parity: SerialKissParity.None,
        StopBits: SerialKissStopBits.One,
        ReadTimeout: TimeSpan.FromSeconds(10),
        ReconnectEnabled: true,
        ReconnectDelay: TimeSpan.FromSeconds(30),
        StaleDataThreshold: TimeSpan.FromMinutes(15),
        ModelName: null,
        Notes: "Receive-only Peet Bros ULTIMETER serial/text weather input. APRS weather transmit is not supported here.");

    public WeatherInputDriverConfiguration ToDriverConfiguration()
    {
        return new WeatherInputDriverConfiguration(
            DriverId,
            SourceName,
            WeatherInputDriverType.Serial,
            Enabled,
            SourceName,
            StaleDataThreshold,
            ReconnectEnabled,
            ReconnectDelay,
            ConnectionTimeout: ReadTimeout,
            ReadTimeout,
            Notes);
    }
}
