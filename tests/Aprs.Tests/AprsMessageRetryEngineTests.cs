using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsMessageRetryEngineTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SendMessageAsync_EntersWaitingForAckState()
    {
        var (store, transmitter, engine) = CreateEngine();
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello", "01"), TestNow);

        var sent = await engine.SendMessageAsync(draft.Id, TestNow, CancellationToken.None);

        Assert.Equal(AprsMessageStatus.WaitingForAck, sent.Status);
        Assert.Equal(AprsMessageDeliveryState.WaitingForAck, sent.DeliveryState);
        Assert.Equal("01", sent.MessageId);
        Assert.Equal(TestNow, sent.FirstSentAtUtc);
        Assert.Equal(TestNow.AddSeconds(30), sent.NextRetryAtUtc);
        Assert.Equal(1, transmitter.SendCallCount);
        Assert.Equal("N0CALL>APCMD0::K8ABC    :Hello{01", transmitter.LastPacket);
    }

    [Fact]
    public async Task ProcessAckOrRej_AckMarksMatchingMessageAcknowledged()
    {
        var (store, _, engine) = CreateEngine();
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello", "01"), TestNow);
        await engine.SendMessageAsync(draft.Id, TestNow, CancellationToken.None);

        var acknowledged = engine.ProcessAckOrRej(ParseMessage("K8ABC>APCMD0::N0CALL   :ack01"), TestNow.AddSeconds(5));

        Assert.NotNull(acknowledged);
        Assert.Equal(AprsMessageStatus.Acknowledged, acknowledged.Status);
        Assert.Equal(AprsMessageDeliveryState.Acknowledged, acknowledged.DeliveryState);
        Assert.Equal(TestNow.AddSeconds(5), acknowledged.AcknowledgedAtUtc);
        Assert.Null(acknowledged.NextRetryAtUtc);
    }

    [Fact]
    public async Task ProcessAckOrRej_RejMarksMatchingMessageRejected()
    {
        var (store, _, engine) = CreateEngine();
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello", "01"), TestNow);
        await engine.SendMessageAsync(draft.Id, TestNow, CancellationToken.None);

        var rejected = engine.ProcessAckOrRej(ParseMessage("K8ABC>APCMD0::N0CALL   :rej01"), TestNow.AddSeconds(5));

        Assert.NotNull(rejected);
        Assert.Equal(AprsMessageStatus.Rejected, rejected.Status);
        Assert.Equal(AprsMessageDeliveryState.Rejected, rejected.DeliveryState);
        Assert.Contains("rejected", rejected.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessAckOrRej_UnrelatedAckDoesNotAcknowledgeWrongMessage()
    {
        var (store, _, engine) = CreateEngine();
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello", "01"), TestNow);
        await engine.SendMessageAsync(draft.Id, TestNow, CancellationToken.None);

        var result = engine.ProcessAckOrRej(ParseMessage("K8ABC>APCMD0::N0CALL   :ack99"), TestNow.AddSeconds(5));

        Assert.Null(result);
        var stored = Assert.Single(store.GetOutboxMessages());
        Assert.Equal(AprsMessageStatus.WaitingForAck, stored.Status);
    }

    [Fact]
    public async Task GetMessagesDueForRetry_ReturnsMessageAfterRetryInterval()
    {
        var (store, _, engine) = CreateEngine();
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello", "01"), TestNow);
        await engine.SendMessageAsync(draft.Id, TestNow, CancellationToken.None);

        Assert.Empty(engine.GetMessagesDueForRetry(TestNow.AddSeconds(29)));
        var due = engine.GetMessagesDueForRetry(TestNow.AddSeconds(30));

        Assert.Single(due);
    }

    [Fact]
    public async Task ProcessRetriesAsync_IncrementsRetryCountWhenDue()
    {
        var (store, transmitter, engine) = CreateEngine();
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello", "01"), TestNow);
        await engine.SendMessageAsync(draft.Id, TestNow, CancellationToken.None);

        var retried = await engine.ProcessRetriesAsync(TestNow.AddSeconds(30), CancellationToken.None);

        var message = Assert.Single(retried);
        Assert.Equal(1, message.RetryCount);
        Assert.Equal(AprsMessageStatus.WaitingForAck, message.Status);
        Assert.Equal(2, transmitter.SendCallCount);
    }

    [Fact]
    public async Task ProcessRetriesAsync_FailsAfterMaxRetries()
    {
        var (store, _, engine) = CreateEngine(maximumRetries: 1);
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello", "01"), TestNow);
        await engine.SendMessageAsync(draft.Id, TestNow, CancellationToken.None);
        await engine.ProcessRetriesAsync(TestNow.AddSeconds(30), CancellationToken.None);

        var failed = await engine.ProcessRetriesAsync(TestNow.AddSeconds(60), CancellationToken.None);

        var message = Assert.Single(failed);
        Assert.Equal(AprsMessageStatus.Failed, message.Status);
        Assert.Equal(AprsMessageDeliveryState.Failed, message.DeliveryState);
        Assert.Contains("Maximum message retries", message.FailureReason);
    }

    [Fact]
    public async Task CancelledMessage_DoesNotRetry()
    {
        var (store, transmitter, engine) = CreateEngine();
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello", "01"), TestNow);
        var sent = await engine.SendMessageAsync(draft.Id, TestNow, CancellationToken.None);

        engine.Cancel(sent.Id, TestNow.AddSeconds(5), "Operator cancelled.");
        var due = await engine.ProcessRetriesAsync(TestNow.AddMinutes(1), CancellationToken.None);

        Assert.Empty(due);
        Assert.Equal(1, transmitter.SendCallCount);
        Assert.Equal(AprsMessageStatus.Cancelled, store.GetOutboxMessages().Single().Status);
    }

    [Fact]
    public async Task ProcessRetriesAsync_FakeTransmitCalledOnlyWhenRetryDue()
    {
        var (store, transmitter, engine) = CreateEngine();
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello", "01"), TestNow);
        await engine.SendMessageAsync(draft.Id, TestNow, CancellationToken.None);

        await engine.ProcessRetriesAsync(TestNow.AddSeconds(29), CancellationToken.None);

        Assert.Equal(1, transmitter.SendCallCount);
    }

    [Fact]
    public async Task FailedTransmitResultRecordsFailureReason()
    {
        var (store, transmitter, engine) = CreateEngine();
        transmitter.NextFailureReason = "APRS-IS transmit disabled.";
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello", "01"), TestNow);

        var failed = await engine.SendMessageAsync(draft.Id, TestNow, CancellationToken.None);

        Assert.Equal(AprsMessageStatus.Failed, failed.Status);
        Assert.Equal(AprsMessageDeliveryState.Failed, failed.DeliveryState);
        Assert.Equal("APRS-IS transmit disabled.", failed.FailureReason);
        Assert.Contains("APRS-IS transmit disabled.", failed.ValidationErrors);
    }

    [Fact]
    public void SequentialMessageIdGenerator_GeneratesAprsFriendlyIds()
    {
        var generator = new SequentialAprsMessageIdGenerator(seed: 8);

        Assert.Equal("08", generator.NextId());
        Assert.Equal("09", generator.NextId());
    }

    private static (AprsMessageStoreService Store, FakeMessageTransmitter Transmitter, AprsMessageRetryEngine Engine) CreateEngine(int maximumRetries = 3)
    {
        var store = new AprsMessageStoreService();
        var transmitter = new FakeMessageTransmitter();
        var engine = new AprsMessageRetryEngine(
            store,
            transmitter,
            new SequentialAprsMessageIdGenerator(),
            AprsMessageRetryConfiguration.Default with
            {
                MaximumRetries = maximumRetries,
                RetryInterval = TimeSpan.FromSeconds(30)
            });

        return (store, transmitter, engine);
    }

    private static MessageAprsPacket ParseMessage(string rawLine)
    {
        var parser = new AprsParser();
        return Assert.IsType<MessageAprsPacket>(parser.Parse(rawLine, TestNow));
    }

    private sealed class FakeMessageTransmitter : IAprsMessageTransmitService
    {
        public int SendCallCount { get; private set; }

        public string? LastPacket { get; private set; }

        public string? NextFailureReason { get; set; }

        public Task<AprsMessageTransmitResult> SendAsync(string rawPacket, CancellationToken cancellationToken)
        {
            SendCallCount++;
            LastPacket = rawPacket;
            if (NextFailureReason is not null)
            {
                var reason = NextFailureReason;
                NextFailureReason = null;
                return Task.FromResult(AprsMessageTransmitResult.Failed(TestNow, rawPacket, reason));
            }

            return Task.FromResult(AprsMessageTransmitResult.Succeeded(TestNow, rawPacket));
        }
    }
}
