using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsBulletinServiceTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AcceptPacket_StoresBln0AsBulletin()
    {
        var service = new AprsBulletinService();

        var result = service.AcceptPacket(Parse("W1AW>APRS::BLN0     :Club meeting at 1900 local"), AprsPacketSource.AprsIs);

        Assert.True(result.WasHandled);
        var bulletin = Assert.Single(service.GetAllBulletins());
        Assert.Equal("0", bulletin.BulletinId);
        Assert.Equal("W1AW", bulletin.SenderCallsign);
        Assert.Equal("BLN0", bulletin.Addressee);
        Assert.Equal("Club meeting at 1900 local", bulletin.BulletinText);
        Assert.Equal(AprsPacketSource.AprsIs, bulletin.Source);
        Assert.True(bulletin.IsActive);
    }

    [Fact]
    public void AcceptPacket_StoresBln1AsBulletin()
    {
        var service = new AprsBulletinService();

        service.AcceptPacket(Parse("W1AW>APRS::BLN1     :Weather net at 2000"));

        var bulletin = Assert.Single(service.GetAllBulletins());
        Assert.Equal("1", bulletin.BulletinId);
        Assert.Equal("Weather net at 2000", bulletin.BulletinText);
    }

    [Fact]
    public void AcceptPacket_UpdatesExistingBulletinFromSameSenderAndId()
    {
        var service = new AprsBulletinService();
        service.AcceptPacket(Parse("W1AW>APRS::BLN0     :Club meeting at 1900 local"));

        service.AcceptPacket(Parse("W1AW>APRS::BLN0     :Club meeting moved to 1930", TestNow.AddMinutes(5)));

        var bulletin = Assert.Single(service.GetAllBulletins());
        Assert.Equal("Club meeting moved to 1930", bulletin.BulletinText);
        Assert.Equal(TestNow.AddMinutes(5), bulletin.LastUpdatedAtUtc);
    }

    [Fact]
    public void AcceptPacket_DoesNotStorePrivateMessagesAsBulletins()
    {
        var service = new AprsBulletinService();

        var result = service.AcceptPacket(Parse("K8ABC>APRS::N0CALL   :Hello there{01"));

        Assert.False(result.WasHandled);
        Assert.Empty(service.GetAllBulletins());
        Assert.Empty(service.GetAllAnnouncements());
        Assert.Empty(service.GetQueries());
    }

    [Fact]
    public void AcceptPacket_StoresAnnouncementStyleBulletinSeparately()
    {
        var service = new AprsBulletinService();

        service.AcceptPacket(Parse("K8ABC>APRS::BLNQST  :ARES net starting now"));

        Assert.Empty(service.GetAllBulletins());
        var announcement = Assert.Single(service.GetAllAnnouncements());
        Assert.Equal("QST", announcement.AnnouncementId);
        Assert.Equal("ARES net starting now", announcement.AnnouncementText);
        Assert.True(announcement.IsActive);
    }

    [Fact]
    public void AcceptPacket_StoresDirectQueryPacket()
    {
        var service = new AprsBulletinService();

        service.AcceptPacket(Parse("QUERY1>APRS:?APRSD"), AprsPacketSource.Replay);

        var query = Assert.Single(service.GetQueries());
        Assert.Equal("APRSD", query.QueryType);
        Assert.Equal("?APRSD", query.QueryBody);
        Assert.Equal("QUERY1", query.SenderCallsign);
        Assert.Equal(AprsPacketSource.Replay, query.Source);
    }

    [Fact]
    public void AcceptPacket_StoresAddressedQueryMessage()
    {
        var service = new AprsBulletinService();

        service.AcceptPacket(Parse("QUERY2>APRS::APRS     :?APRSD"));

        var query = Assert.Single(service.GetQueries());
        Assert.Equal("APRSD", query.QueryType);
        Assert.Equal("?APRSD", query.QueryBody);
        Assert.Equal("QUERY2", query.SenderCallsign);
    }

    [Fact]
    public void AcceptPacket_MalformedBulletinLikePacketDoesNotCrash()
    {
        var service = new AprsBulletinService();

        var exception = Record.Exception(() => service.AcceptPacket(Parse("BADMSG>APRS::BLN")));

        Assert.Null(exception);
        Assert.Empty(service.GetAllBulletins());
    }

    [Fact]
    public void Clear_RemovesBulletinsAnnouncementsAndQueries()
    {
        var service = new AprsBulletinService();
        service.AcceptPacket(Parse("W1AW>APRS::BLN0     :Club meeting"));
        service.AcceptPacket(Parse("K8ABC>APRS::BLNQST  :ARES net"));
        service.AcceptPacket(Parse("QUERY1>APRS:?APRSD"));

        service.Clear();

        Assert.Empty(service.GetAllBulletins());
        Assert.Empty(service.GetAllAnnouncements());
        Assert.Empty(service.GetQueries());
    }

    [Fact]
    public void ClearExpired_RemovesExpiredBulletinsAndAnnouncements()
    {
        var service = new AprsBulletinService(new AprsBulletinConfiguration(
            BulletinLifetime: TimeSpan.FromMinutes(10),
            AnnouncementLifetime: TimeSpan.FromMinutes(10)));
        service.AcceptPacket(Parse("W1AW>APRS::BLN0     :Club meeting"));
        service.AcceptPacket(Parse("K8ABC>APRS::BLNQST  :ARES net"));

        var removed = service.ClearExpired(TestNow.AddMinutes(11));

        Assert.Equal(2, removed);
        Assert.Empty(service.GetAllBulletins());
        Assert.Empty(service.GetAllAnnouncements());
    }

    private static AprsPacket Parse(string rawLine, DateTimeOffset? receivedAtUtc = null)
    {
        var parser = new AprsParser();
        return parser.Parse(rawLine, receivedAtUtc ?? TestNow);
    }
}
