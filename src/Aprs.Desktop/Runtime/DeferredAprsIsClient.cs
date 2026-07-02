namespace Aprs.Desktop.Runtime;

/// <summary>
/// An IAprsIsClient proxy that holds a settable inner client.
/// Used to break the circular dependency where IGateService needs an
/// IAprsIsClient at DI registration time, but the live client only
/// exists after DesktopRuntime finishes constructing.
///
/// Set InnerClient to the real client once it is available.
/// Until then all calls are no-ops or return disconnected state.
/// </summary>
public sealed class DeferredAprsIsClient : Aprs.Transport.IAprsIsClient
{
    private Aprs.Transport.IAprsIsClient? inner;

    public Aprs.Transport.IAprsIsClient? InnerClient
    {
        get => inner;
        set
        {
            if (inner == value) return;
            if (inner is not null) inner.RawPacketReceived -= OnRawPacketReceived;
            inner = value;
            if (inner is not null) inner.RawPacketReceived += OnRawPacketReceived;
        }
    }

    // ── IAprsIsClient ─────────────────────────────────────────────────────────

    public event EventHandler<Aprs.Transport.AprsIsRawPacketReceivedEventArgs>? RawPacketReceived;

    public Aprs.Transport.AprsIsConnectionState State
        => inner?.State ?? Aprs.Transport.AprsIsConnectionState.Disconnected;

    public Exception? LastError => inner?.LastError;

    public Task ConnectAsync(CancellationToken cancellationToken)
        => inner?.ConnectAsync(cancellationToken) ?? Task.CompletedTask;

    public Task DisconnectAsync(CancellationToken cancellationToken)
        => inner?.DisconnectAsync(cancellationToken) ?? Task.CompletedTask;

    public Task<Aprs.Transport.AprsIsTransmitResult> SendRawPacketAsync(
        string rawPacketLine, bool transmitConfirmed, CancellationToken cancellationToken)
        => inner?.SendRawPacketAsync(rawPacketLine, transmitConfirmed, cancellationToken)
           ?? Task.FromResult(Aprs.Transport.AprsIsTransmitResult.Failed(
               DateTimeOffset.UtcNow,
               rawPacketLine,
               Aprs.Transport.AprsIsConnectionState.Disconnected,
               "No live APRS-IS client available."));

    public async IAsyncEnumerable<Aprs.Transport.AprsIsRawPacketReceivedEventArgs> ReadPacketsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (inner is null) yield break;
        await foreach (var pkt in inner.ReadPacketsAsync(cancellationToken).ConfigureAwait(false))
            yield return pkt;
    }

    private void OnRawPacketReceived(object? sender, Aprs.Transport.AprsIsRawPacketReceivedEventArgs e)
        => RawPacketReceived?.Invoke(this, e);

    public ValueTask DisposeAsync() => inner?.DisposeAsync() ?? ValueTask.CompletedTask;
}
