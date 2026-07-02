using Aprs.Core;
using Aprs.Services;
using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Owns the APRS message ACK/retry pipeline. Wires the <see cref="AprsMessageRetryEngine"/>
/// to a live APRS-IS client, runs a background retry tick loop, and routes incoming ACK/REJ
/// packets from the ingestion pipeline to the retry engine so outgoing messages are correctly
/// marked Acknowledged or Rejected.
///
/// <para>The operator experience: send a message → it is transmitted immediately → if no ACK
/// arrives within 30 seconds it is retried → up to 3 retries → marked Failed if still no ACK.
/// When an ACK arrives the message is marked Acknowledged instantly.</para>
/// </summary>
public sealed class MessageAckCoordinator : IAsyncDisposable
{
    private readonly AprsMessageRetryEngine retryEngine;
    private readonly CancellationTokenSource cts = new();
    private Task? tickLoop;

    public MessageAckCoordinator(AprsMessageRetryEngine retryEngine)
    {
        this.retryEngine = retryEngine ?? throw new ArgumentNullException(nameof(retryEngine));
    }

    /// <summary>
    /// Creates a coordinator wired to the provided APRS-IS client for transmit.
    /// </summary>
    public static MessageAckCoordinator Create(
        IAprsMessageStoreService messageStore,
        IAprsIsClient aprsIsClient,
        bool transmitConfirmed = true)
    {
        var transmitService = new AprsIsMessageTransmitService(aprsIsClient, transmitConfirmed);
        var retryEngine = new AprsMessageRetryEngine(messageStore, transmitService);
        return new MessageAckCoordinator(retryEngine);
    }

    /// <summary>
    /// Creates a coordinator wired to both APRS-IS and an optional RF transmit path.
    /// Messages are sent on whichever paths are available and connected.
    /// </summary>
    public static MessageAckCoordinator CreateWithRf(
        IAprsMessageStoreService messageStore,
        IAprsIsClient? aprsIsClient,
        IRfBeaconTransmitClient? rfClient,
        bool transmitConfirmed = true)
    {
        var transmitService = new CompositeMessageTransmitService(aprsIsClient, rfClient, transmitConfirmed);
        var retryEngine = new AprsMessageRetryEngine(messageStore, transmitService);
        return new MessageAckCoordinator(retryEngine);
    }

    /// <summary>Sends a message and starts the ACK/retry cycle for it.</summary>
    public Task<AprsMessageRecord> SendAsync(Guid messageRecordId, CancellationToken cancellationToken = default)
        => retryEngine.SendMessageAsync(messageRecordId, DateTimeOffset.UtcNow, cancellationToken);

    /// <summary>
    /// Routes an incoming packet to the ACK/REJ processor. Call this from the ingestion
    /// pipeline for every parsed message packet — the engine ignores non-ACK packets.
    /// </summary>
    public void ProcessIncomingPacket(AprsPacket packet)
    {
        if (packet is MessageAprsPacket mp)
        {
            retryEngine.ProcessAckOrRej(mp, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>Cancels a pending outgoing message.</summary>
    public AprsMessageRecord Cancel(Guid messageRecordId, string? reason = null)
        => retryEngine.Cancel(messageRecordId, DateTimeOffset.UtcNow, reason);

    /// <summary>Starts the background retry tick loop (checks every 10 seconds).</summary>
    public void Start()
    {
        tickLoop = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cts.Token).ConfigureAwait(false);
                    await retryEngine.ProcessRetriesAsync(DateTimeOffset.UtcNow, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Never crash the retry loop.
                }
            }
        }, cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);
        if (tickLoop is not null)
        {
            try { await tickLoop.ConfigureAwait(false); }
            catch { /* suppress */ }
        }
        cts.Dispose();
    }
}
