using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class MessageCenterViewModelTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateDesignTime_LoadsDemoMessages()
    {
        var viewModel = MessageCenterViewModel.CreateDesignTime();

        Assert.True(viewModel.InboxCount >= 1);
        Assert.True(viewModel.OutboxCount >= 1);
        Assert.True(viewModel.DraftCount >= 1);
        Assert.True(viewModel.BulletinCount >= 1);
        Assert.True(viewModel.AnnouncementCount >= 1);
        Assert.True(viewModel.QueryCount >= 1);
        Assert.NotNull(viewModel.SelectedMessage);
    }

    [Fact]
    public void Constructor_LoadsRowsFromMessageStore()
    {
        var store = new AprsMessageStoreService();
        store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Hello"), TestNow);

        var viewModel = new MessageCenterViewModel(store);

        var draft = Assert.Single(viewModel.Drafts);
        Assert.Equal("K8ABC", draft.RemoteStation);
        Assert.Equal("Hello", draft.Body);
        Assert.Equal(nameof(AprsMessageStatus.Draft), draft.Status);
    }

    [Fact]
    public void SelectMessage_LoadsConversation()
    {
        var store = new AprsMessageStoreService();
        store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Reply"), TestNow);
        var viewModel = new MessageCenterViewModel(store);
        var draft = Assert.Single(viewModel.Drafts);

        viewModel.SelectMessage(draft);

        Assert.Same(draft, viewModel.SelectedMessage);
        Assert.Single(viewModel.Conversation);
        Assert.Equal("K8ABC", viewModel.Conversation.Single().RemoteStation);
    }

    [Fact]
    public void Constructor_LoadsBulletinAnnouncementAndQueryRows()
    {
        var store = new AprsMessageStoreService();
        var bulletins = new AprsBulletinService();
        var parser = new Aprs.Core.AprsParser();
        bulletins.AcceptPacket(parser.Parse("W1AW>APRS::BLN0     :Club meeting", TestNow));
        bulletins.AcceptPacket(parser.Parse("K8ABC>APRS::BLNQST  :ARES net", TestNow));
        bulletins.AcceptPacket(parser.Parse("QUERY1>APRS:?APRSD", TestNow));

        var viewModel = new MessageCenterViewModel(store, bulletins);

        Assert.Equal("Club meeting", Assert.Single(viewModel.Bulletins).Text);
        Assert.Equal("ARES net", Assert.Single(viewModel.Announcements).Text);
        Assert.Equal("APRSD", Assert.Single(viewModel.Queries).QueryType);
    }

    [Fact]
    public void Refresh_SeparatesPrivateInboxFromBulletins()
    {
        var store = new AprsMessageStoreService();
        var bulletins = new AprsBulletinService();
        var parser = new Aprs.Core.AprsParser();
        store.AddIncomingMessage((Aprs.Core.MessageAprsPacket)parser.Parse("K8ABC>APRS::N0CALL   :Hello", TestNow), "N0CALL");
        var bulletinPacket = (Aprs.Core.MessageAprsPacket)parser.Parse("W1AW>APRS::BLN0     :Club meeting", TestNow);
        store.AddIncomingMessage(bulletinPacket, "N0CALL");
        bulletins.AcceptPacket(bulletinPacket);

        var viewModel = new MessageCenterViewModel(store, bulletins);

        var inbox = Assert.Single(viewModel.Inbox);
        Assert.Equal("K8ABC", inbox.RemoteStation);
        Assert.Equal("Club meeting", Assert.Single(viewModel.Bulletins).Text);
    }

    [Fact]
    public void ValidateCompose_ReportsValidationErrors()
    {
        var viewModel = new MessageCenterViewModel(new AprsMessageStoreService())
        {
            ComposeLocalStation = "N0CALL",
            ComposeRecipient = string.Empty,
            ComposeBody = "Hello"
        };

        var validation = viewModel.ValidateCompose();

        Assert.False(validation.IsValid);
        Assert.Contains("Recipient", viewModel.ComposeValidationSummary);
    }

    [Fact]
    public void MessageRowViewModel_FormatsValidationSummarySafely()
    {
        var record = new AprsMessageRecord(
            Guid.NewGuid(),
            MessageId: null,
            LocalStationCallsign: "N0CALL",
            RemoteStationCallsign: "K8ABC",
            Addressee: "N0CALL",
            Sender: "K8ABC",
            Recipient: "N0CALL",
            MessageBody: "Hello",
            RawPacket: null,
            AprsMessageDirection.Incoming,
            AprsMessageStatus.Received,
            TestNow,
            SentAtUtc: null,
            ReceivedAtUtc: TestNow,
            TestNow,
            AprsPacketSource.Unknown,
            AprsMessageKind.PrivateMessage,
            ValidationErrors: []);

        var row = new MessageRowViewModel(record);

        Assert.Equal("None", row.ValidationSummary);
        Assert.Equal("K8ABC", row.RemoteStation);
        Assert.Equal("Hello", row.Body);
    }
}
