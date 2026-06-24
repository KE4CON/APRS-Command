namespace Aprs.Transport;

public interface IAprsTransport : IAsyncDisposable
{
    string Name { get; }
    IAsyncEnumerable<string> ReadPacketsAsync(CancellationToken cancellationToken);
    Task SendPacketAsync(string packet, CancellationToken cancellationToken);
}
