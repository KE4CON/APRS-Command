using System.Collections.Concurrent;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Fires scheduled APRS messages at the configured time.
/// Runs a background timer loop checking every 30 seconds for due messages.
/// </summary>
public sealed class ScheduledMessageService : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, ScheduledMessage> messages = new();
    private readonly Func<string, string, string, Task> sendFunc;
    private readonly CancellationTokenSource cts = new();
    private readonly Task timerLoop;

    public ScheduledMessageService(Func<string, string, string, Task> sendFunc)
    {
        this.sendFunc = sendFunc;
        timerLoop = RunLoopAsync(cts.Token);
    }

    public event EventHandler<ScheduledMessage>? MessageFired;
    public event EventHandler<ScheduledMessage>? MessageAdded;
    public event EventHandler<Guid>? MessageRemoved;

    public IReadOnlyCollection<ScheduledMessage> PendingMessages =>
        messages.Values.Where(m => !m.IsSent && m.SendAtUtc > DateTimeOffset.UtcNow).ToList();

    public IReadOnlyCollection<ScheduledMessage> AllMessages =>
        messages.Values.OrderBy(m => m.SendAtUtc).ToList();

    public Guid Schedule(string recipient, string body, DateTimeOffset sendAt)
    {
        var msg = new ScheduledMessage(Guid.NewGuid(), recipient, body, sendAt);
        messages[msg.Id] = msg;
        MessageAdded?.Invoke(this, msg);
        return msg.Id;
    }

    public bool Cancel(Guid id)
    {
        if (messages.TryRemove(id, out _))
        {
            MessageRemoved?.Invoke(this, id);
            return true;
        }
        return false;
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);

                var now = DateTimeOffset.UtcNow;
                var due = messages.Values
                    .Where(m => !m.IsSent && m.SendAtUtc <= now)
                    .ToList();

                foreach (var msg in due)
                {
                    try
                    {
                        await sendFunc(msg.Recipient, msg.Body, msg.Id.ToString("N")[..8]).ConfigureAwait(false);
                        var sent = msg with { IsSent = true, SentAtUtc = now };
                        messages[msg.Id] = sent;
                        MessageFired?.Invoke(this, sent);
                    }
                    catch
                    {
                        // Keep in queue on failure — will retry next tick
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* swallow, keep running */ }
        }
    }

    public async ValueTask DisposeAsync()
    {
        cts.Cancel();
        try { await timerLoop.ConfigureAwait(false); } catch { }
        cts.Dispose();
    }
}

public sealed record ScheduledMessage(
    Guid Id,
    string Recipient,
    string Body,
    DateTimeOffset SendAtUtc,
    bool IsSent = false,
    DateTimeOffset? SentAtUtc = null)
{
    public string SendAtLocal  => SendAtUtc.ToLocalTime().ToString("MM/dd HH:mm");
    public string StatusLabel  => IsSent ? $"✓ Sent {SentAtUtc!.Value.ToLocalTime():HH:mm}" : "Pending";
    public string StatusColor  => IsSent ? "#15803d" : "#1d4ed8";
}
