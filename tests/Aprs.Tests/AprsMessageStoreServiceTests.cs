using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsMessageStoreServiceTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddIncomingMessage_AddsParsedMessageToInbox()
    {
        var service = new AprsMessageStoreService();

        var record = service.AddIncomingMessage(
            ParseMessage("K8ABC>APRS::N0CALL   :Hello there{01"),
            "N0CALL",
            AprsPacketSource.AprsIs);

        Assert.Equal(AprsMessageDirection.Incoming, record.Direction);
        Assert.Equal(AprsMessageStatus.Received, record.Status);
        Assert.Equal("K8ABC", record.RemoteStationCallsign);
        Assert.Equal("N0CALL", record.LocalStationCallsign);
        Assert.Equal("Hello there", record.MessageBody);
        Assert.Equal("01", record.MessageId);
        Assert.Equal(AprsPacketSource.AprsIs, record.Source);
        Assert.Single(service.GetInboxMessages());
    }

    [Fact]
    public void AddIncomingMessage_IdentifiesBulletinsAndAnnouncements()
    {
        var service = new AprsMessageStoreService();

        var bulletin = service.AddIncomingMessage(ParseMessage("W1AW>APRS::BLN0     :Club meeting"), "N0CALL");
        var announcement = service.AddIncomingMessage(ParseMessage("W1AW>APRS::BLNA     :Announcement"), "N0CALL");

        Assert.Equal(AprsMessageKind.Bulletin, bulletin.Kind);
        Assert.Equal(AprsMessageKind.Announcement, announcement.Kind);
    }

    [Fact]
    public void CreateDraft_CreatesOutgoingDraft()
    {
        var service = new AprsMessageStoreService();

        var draft = service.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello"), TestNow);

        Assert.Equal(AprsMessageDirection.Draft, draft.Direction);
        Assert.Equal(AprsMessageStatus.Draft, draft.Status);
        Assert.Equal("K8ABC", draft.RemoteStationCallsign);
        Assert.Empty(draft.ValidationErrors);
        Assert.Single(service.GetDrafts());
    }

    [Fact]
    public void QueueMessage_MovesDraftToOutbox()
    {
        var service = new AprsMessageStoreService();
        var draft = service.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello"), TestNow);

        var queued = service.QueueMessage(draft.Id, TestNow.AddMinutes(1));

        Assert.Equal(AprsMessageDirection.Outgoing, queued.Direction);
        Assert.Equal(AprsMessageStatus.Queued, queued.Status);
        Assert.Single(service.GetOutboxMessages());
        Assert.Empty(service.GetDrafts());
    }

    [Fact]
    public void MarkSent_UpdatesMessageStatus()
    {
        var service = new AprsMessageStoreService();
        var draft = service.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello"), TestNow);
        var queued = service.QueueMessage(draft.Id, TestNow.AddMinutes(1));

        var sent = service.MarkSent(queued.Id, TestNow.AddMinutes(2));

        Assert.Equal(AprsMessageStatus.Sent, sent.Status);
        Assert.Equal(TestNow.AddMinutes(2), sent.SentAtUtc);
    }

    [Fact]
    public void MarkFailed_UpdatesMessageStatusAndValidationErrors()
    {
        var service = new AprsMessageStoreService();
        var draft = service.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello"), TestNow);
        var queued = service.QueueMessage(draft.Id, TestNow.AddMinutes(1));

        var failed = service.MarkFailed(queued.Id, TestNow.AddMinutes(2), "No transport configured.");

        Assert.Equal(AprsMessageStatus.Failed, failed.Status);
        Assert.Contains("No transport configured.", failed.ValidationErrors);
    }

    [Fact]
    public void GetMessagesByRemoteStation_ReturnsMatchingMessages()
    {
        var service = new AprsMessageStoreService();
        service.AddIncomingMessage(ParseMessage("K8ABC>APRS::N0CALL   :Hello"), "N0CALL");
        service.AddIncomingMessage(ParseMessage("W1AW>APRS::N0CALL   :Bulletin check"), "N0CALL");

        var messages = service.GetMessagesByRemoteStation("k8abc");

        var message = Assert.Single(messages);
        Assert.Equal("K8ABC", message.RemoteStationCallsign);
    }

    [Fact]
    public void GetConversation_ReturnsIncomingAndOutgoingMessagesWithStation()
    {
        var service = new AprsMessageStoreService();
        service.AddIncomingMessage(ParseMessage("K8ABC>APRS::N0CALL   :Hello"), "N0CALL");
        var draft = service.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Reply"), TestNow.AddMinutes(1));
        service.QueueMessage(draft.Id, TestNow.AddMinutes(2));

        var conversation = service.GetConversation("K8ABC");

        Assert.Equal(2, conversation.Count);
        Assert.Contains(conversation, message => message.Direction == AprsMessageDirection.Incoming);
        Assert.Contains(conversation, message => message.Direction == AprsMessageDirection.Outgoing);
    }

    [Fact]
    public void AddIncomingMessage_MalformedPacketDoesNotCrash()
    {
        var service = new AprsMessageStoreService();

        var record = service.AddIncomingMessage(ParseMessage("BADMSG>APRS::TOOSHORT"), "N0CALL");

        Assert.Equal(AprsMessageStatus.Received, record.Status);
        Assert.NotEmpty(record.ValidationErrors);
        Assert.Single(service.GetInboxMessages());
    }

    [Fact]
    public void ValidateComposeRequest_RejectsMissingRecipient()
    {
        var service = new AprsMessageStoreService();

        var validation = service.ValidateComposeRequest(new AprsMessageComposeRequest("N0CALL", "", "Hello"));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("Recipient", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateComposeRequest_RejectsEmptyBody()
    {
        var service = new AprsMessageStoreService();

        var validation = service.ValidateComposeRequest(new AprsMessageComposeRequest("N0CALL", "K8ABC", ""));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("Message body", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateComposeRequest_RejectsLineBreaks()
    {
        var service = new AprsMessageStoreService();

        var validation = service.ValidateComposeRequest(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello\nthere"));

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("line breaks", StringComparison.OrdinalIgnoreCase));
    }

    private static MessageAprsPacket ParseMessage(string rawLine)
    {
        var parser = new AprsParser();
        var parsed = parser.Parse(rawLine, TestNow);
        return Assert.IsType<MessageAprsPacket>(parsed);
    }
}
