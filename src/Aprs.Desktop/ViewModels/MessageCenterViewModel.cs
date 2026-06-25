using Aprs.Desktop.Configuration;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class MessageCenterViewModel
{
    private readonly IAprsMessageStoreService messageStore;
    private readonly IAprsBulletinService bulletinService;
    private readonly List<MessageRowViewModel> inbox = [];
    private readonly List<MessageRowViewModel> outbox = [];
    private readonly List<MessageRowViewModel> drafts = [];
    private readonly List<MessageRowViewModel> conversation = [];
    private readonly List<BulletinRowViewModel> bulletins = [];
    private readonly List<AnnouncementRowViewModel> announcements = [];
    private readonly List<QueryRowViewModel> queries = [];

    public MessageCenterViewModel(IAprsMessageStoreService messageStore)
        : this(messageStore, new AprsBulletinService())
    {
    }

    public MessageCenterViewModel(IAprsMessageStoreService messageStore, IAprsBulletinService bulletinService)
    {
        this.messageStore = messageStore;
        this.bulletinService = bulletinService;
        Refresh();
        SelectedMessage = Inbox.FirstOrDefault() ?? Outbox.FirstOrDefault() ?? Drafts.FirstOrDefault();
        if (SelectedMessage is not null)
        {
            SelectMessage(SelectedMessage);
        }
    }

    public IReadOnlyList<MessageRowViewModel> Inbox => inbox;

    public IReadOnlyList<MessageRowViewModel> Outbox => outbox;

    public IReadOnlyList<MessageRowViewModel> Drafts => drafts;

    public IReadOnlyList<MessageRowViewModel> Conversation => conversation;

    public IReadOnlyList<BulletinRowViewModel> Bulletins => bulletins;

    public IReadOnlyList<AnnouncementRowViewModel> Announcements => announcements;

    public IReadOnlyList<QueryRowViewModel> Queries => queries;

    public MessageRowViewModel? SelectedMessage { get; set; }

    public string ComposeLocalStation { get; set; } = StationProfile.Load().FullCallsign;

    public string ComposeRecipient { get; set; } = "K8ABC";

    public string ComposeBody { get; set; } = "Reply text";

    public string ComposeValidationSummary { get; private set; } = "Not validated";

    public int InboxCount => Inbox.Count;

    public int OutboxCount => Outbox.Count;

    public int DraftCount => Drafts.Count;

    public int BulletinCount => Bulletins.Count;

    public int AnnouncementCount => Announcements.Count;

    public int QueryCount => Queries.Count;

    public static MessageCenterViewModel CreateDesignTime()
    {
        var store = new AprsMessageStoreService();
        var bulletins = new AprsBulletinService();
        var now = DateTimeOffset.UtcNow;
        store.AddIncomingMessage(CreateDemoMessage("K8ABC", "N0CALL", "Hello there", now.AddMinutes(-12)), "N0CALL", AprsPacketSource.Simulation);
        var draft = store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "K8ABC", "Draft reply"), now.AddMinutes(-5));
        store.QueueMessage(draft.Id, now.AddMinutes(-4));
        store.CreateDraft(new AprsMessageComposeRequest("N0CALL", "W1AW", "Question for net control"), now.AddMinutes(-2));
        bulletins.AcceptPacket(CreateDemoMessage("W1AW", "BLN0", "Club meeting at 1900 local", now.AddMinutes(-20)), AprsPacketSource.Simulation);
        bulletins.AcceptPacket(CreateDemoMessage("W1AW", "BLNQST", "Weather net at 2000", now.AddMinutes(-18)), AprsPacketSource.Simulation);
        bulletins.AcceptPacket(ParseDemoPacket("QUERY1>APRS:?APRSD", now.AddMinutes(-10)), AprsPacketSource.Simulation);
        bulletins.AcceptPacket(CreateDemoMessage("QUERY2", "APRS", "?APRSD", now.AddMinutes(-8)), AprsPacketSource.Simulation);
        return new MessageCenterViewModel(store, bulletins);
    }

    public void Refresh()
    {
        inbox.Clear();
        inbox.AddRange(messageStore.GetInboxMessages()
            .Where(message => message.Kind == AprsMessageKind.PrivateMessage)
            .Select(message => new MessageRowViewModel(message)));
        outbox.Clear();
        outbox.AddRange(messageStore.GetOutboxMessages().Select(message => new MessageRowViewModel(message)));
        drafts.Clear();
        drafts.AddRange(messageStore.GetDrafts().Select(message => new MessageRowViewModel(message)));
        bulletins.Clear();
        bulletins.AddRange(bulletinService.GetAllBulletins().Select(bulletin => new BulletinRowViewModel(bulletin)));
        announcements.Clear();
        announcements.AddRange(bulletinService.GetAllAnnouncements().Select(announcement => new AnnouncementRowViewModel(announcement)));
        queries.Clear();
        queries.AddRange(bulletinService.GetQueries().Select(query => new QueryRowViewModel(query)));
        if (SelectedMessage is not null)
        {
            RefreshConversation(SelectedMessage.RemoteStation);
        }
    }

    public void SelectMessage(MessageRowViewModel message)
    {
        SelectedMessage = message;
        RefreshConversation(message.RemoteStation);
    }

    public AprsMessageComposeValidationResult ValidateCompose()
    {
        var validation = messageStore.ValidateComposeRequest(new AprsMessageComposeRequest(
            ComposeLocalStation,
            ComposeRecipient,
            ComposeBody));
        ComposeValidationSummary = validation.IsValid
            ? "Ready to queue"
            : string.Join("; ", validation.Errors);
        return validation;
    }

    private void RefreshConversation(string remoteStation)
    {
        conversation.Clear();
        conversation.AddRange(messageStore.GetConversation(remoteStation).Select(message => new MessageRowViewModel(message)));
    }

    private static Aprs.Core.MessageAprsPacket CreateDemoMessage(string sender, string recipient, string body, DateTimeOffset receivedAtUtc)
    {
        var raw = $"{sender}>APRS::{recipient.PadRight(9)}:{body}";
        var parser = new Aprs.Core.AprsParser();
        return (Aprs.Core.MessageAprsPacket)parser.Parse(raw, receivedAtUtc);
    }

    private static Aprs.Core.AprsPacket ParseDemoPacket(string rawLine, DateTimeOffset receivedAtUtc)
    {
        var parser = new Aprs.Core.AprsParser();
        return parser.Parse(rawLine, receivedAtUtc);
    }
}
