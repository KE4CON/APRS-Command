using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// A no-op <see cref="IAprsIsClient"/> used by <see cref="BeaconService"/> when no
/// transmit-capable APRS-IS port is configured. All transmit attempts return a blocked result
/// rather than throwing, so the scheduler can report the reason cleanly.
/// </summary>
internal sealed class NullAprsIsClient : IAprsIsClient
{
    public event EventHandler<AprsIsRawPacketReceivedEventArgs>? RawPacketReceived
    {
        add { }
        remove { }
    }

    public AprsIsConnectionState State => AprsIsConnectionState.Disconnected;

    public Exception? LastError => null;

    public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<AprsIsTransmitResult> SendRawPacketAsync(
        string rawPacketLine,
        bool transmitConfirmed,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(AprsIsTransmitResult.Failed(
            DateTimeOffset.UtcNow,
            rawPacketLine,
            AprsIsConnectionState.Disconnected,
            "No transmit-capable APRS-IS port is configured. Add an APRS-IS port with a valid passcode in Settings → Connections."));
    }

    public async IAsyncEnumerable<AprsIsRawPacketReceivedEventArgs> ReadPacketsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
