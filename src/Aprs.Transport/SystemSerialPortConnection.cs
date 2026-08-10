using System.IO.Ports;

namespace Aprs.Transport;

/// <summary>
/// ISerialPortConnection implementation backed by System.IO.Ports.SerialPort.
/// Cross-platform: works on macOS, Windows, Linux, and Raspberry Pi via the
/// System.IO.Ports NuGet package already referenced by the transport project.
/// </summary>
internal sealed class SystemSerialPortConnection : ISerialPortConnection, IAsyncDisposable
{
    private readonly SerialPort port;

    public SystemSerialPortConnection(SerialKissConfiguration config)
    {
        port = new SerialPort(
            config.PortName,
            config.BaudRate,
            MapParity(config.Parity),
            config.DataBits,
            MapStopBits(config.StopBits))
        {
            Handshake    = MapHandshake(config.Handshake),
            ReadTimeout  = (int)config.ReadTimeout.TotalMilliseconds,
            WriteTimeout = (int)config.WriteTimeout.TotalMilliseconds,
        };
    }

    public string PortName => port.PortName;
    public int    BaudRate => port.BaudRate;
    public bool   IsOpen   => port.IsOpen;

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        port.Open();
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken cancellationToken)
    {
        if (port.IsOpen) port.Close();
        return Task.CompletedTask;
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        // System.IO.Ports.SerialPort doesn't natively support async/cancellation in .NET 10
        // on all platforms, so we wrap in a Task to keep it off the UI thread.
        return await Task.Run(() =>
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    return port.BaseStream.Read(buffer.Span);
                }
                catch (TimeoutException)
                {
                    // A quiet serial link legitimately produces read timeouts every ReadTimeout ms.
                    // That is neither a fault nor a disconnect, so keep waiting for bytes rather than
                    // returning 0 (which the KISS receive loop treats as a closed connection) or
                    // letting the exception fault a healthy idle link (transport M6).
                    continue;
                }
                catch when (cancellationToken.IsCancellationRequested)
                {
                    return 0;
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            try { port.BaseStream.Write(buffer.Span); }
            catch when (cancellationToken.IsCancellationRequested) { }
        }, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        try { if (port.IsOpen) port.Close(); }
        catch { }
        port.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Enum mapping ──────────────────────────────────────────────────────────

    private static Parity MapParity(SerialKissParity p) => p switch
    {
        SerialKissParity.None  => Parity.None,
        SerialKissParity.Even  => Parity.Even,
        SerialKissParity.Odd   => Parity.Odd,
        SerialKissParity.Mark  => Parity.Mark,
        SerialKissParity.Space => Parity.Space,
        _                      => Parity.None
    };

    private static StopBits MapStopBits(SerialKissStopBits s) => s switch
    {
        SerialKissStopBits.One         => StopBits.One,
        SerialKissStopBits.OnePointFive => StopBits.OnePointFive,
        SerialKissStopBits.Two         => StopBits.Two,
        _                              => StopBits.One
    };

    private static Handshake MapHandshake(SerialKissHandshake h) => h switch
    {
        SerialKissHandshake.None             => Handshake.None,
        SerialKissHandshake.RequestToSend    => Handshake.RequestToSend,
        SerialKissHandshake.XOnXOff          => Handshake.XOnXOff,
        SerialKissHandshake.RequestToSendXOnXOff => Handshake.RequestToSendXOnXOff,
        _                                    => Handshake.None
    };
}
