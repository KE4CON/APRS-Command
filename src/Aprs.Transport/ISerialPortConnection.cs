namespace Aprs.Transport;

public interface ISerialPortConnection : IAsyncDisposable
{
    string PortName { get; }

    int BaudRate { get; }

    bool IsOpen { get; }

    Task OpenAsync(CancellationToken cancellationToken);

    Task CloseAsync(CancellationToken cancellationToken);

    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
}
