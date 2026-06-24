using Aprs.Core;

namespace Aprs.Services;

public sealed class AprsBulletinService : IAprsBulletinService
{
    private readonly AprsBulletinConfiguration configuration;
    private readonly Dictionary<string, AprsBulletinRecord> bulletins = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AprsAnnouncementRecord> announcements = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AprsQueryRecord> queries = [];

    public AprsBulletinService()
        : this(AprsBulletinConfiguration.Default)
    {
    }

    public AprsBulletinService(AprsBulletinConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public AprsBulletinAcceptResult AcceptPacket(AprsPacket packet, AprsPacketSource source = AprsPacketSource.Unknown)
    {
        return packet switch
        {
            MessageAprsPacket messagePacket when messagePacket.IsAnnouncement => StoreAnnouncement(messagePacket, source),
            MessageAprsPacket messagePacket when messagePacket.IsBulletin => StoreBulletin(messagePacket, source),
            MessageAprsPacket messagePacket when messagePacket.IsQuery => StoreMessageQuery(messagePacket, source),
            QueryAprsPacket queryPacket => StoreDirectQuery(queryPacket, source),
            _ => AprsBulletinAcceptResult.NotHandled
        };
    }

    public IReadOnlyList<AprsBulletinRecord> GetAllBulletins()
    {
        return bulletins.Values
            .OrderByDescending(bulletin => bulletin.LastUpdatedAtUtc)
            .ThenBy(bulletin => bulletin.SenderCallsign, StringComparer.OrdinalIgnoreCase)
            .ThenBy(bulletin => bulletin.BulletinId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<AprsBulletinRecord> GetActiveBulletins(DateTimeOffset now)
    {
        return GetAllBulletins()
            .Where(bulletin => IsActive(bulletin.IsActive, bulletin.ExpiresAtUtc, now))
            .ToArray();
    }

    public IReadOnlyList<AprsAnnouncementRecord> GetAllAnnouncements()
    {
        return announcements.Values
            .OrderByDescending(announcement => announcement.LastUpdatedAtUtc)
            .ThenBy(announcement => announcement.SenderCallsign, StringComparer.OrdinalIgnoreCase)
            .ThenBy(announcement => announcement.AnnouncementId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<AprsAnnouncementRecord> GetActiveAnnouncements(DateTimeOffset now)
    {
        return GetAllAnnouncements()
            .Where(announcement => IsActive(announcement.IsActive, announcement.ExpiresAtUtc, now))
            .ToArray();
    }

    public IReadOnlyList<AprsQueryRecord> GetQueries()
    {
        return queries
            .OrderByDescending(query => query.ReceivedAtUtc)
            .ToArray();
    }

    public int ClearExpired(DateTimeOffset now)
    {
        var expiredBulletins = bulletins
            .Where(pair => !IsActive(pair.Value.IsActive, pair.Value.ExpiresAtUtc, now))
            .Select(pair => pair.Key)
            .ToArray();
        var expiredAnnouncements = announcements
            .Where(pair => !IsActive(pair.Value.IsActive, pair.Value.ExpiresAtUtc, now))
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var key in expiredBulletins)
        {
            bulletins.Remove(key);
        }

        foreach (var key in expiredAnnouncements)
        {
            announcements.Remove(key);
        }

        return expiredBulletins.Length + expiredAnnouncements.Length;
    }

    public void Clear()
    {
        bulletins.Clear();
        announcements.Clear();
        queries.Clear();
    }

    private AprsBulletinAcceptResult StoreBulletin(MessageAprsPacket packet, AprsPacketSource source)
    {
        var sender = FormatSourceCallsign(packet.SourceCallsign, packet.SourceSsid);
        var details = ExtractMessageDetails(packet);
        var bulletinId = string.IsNullOrWhiteSpace(details.BulletinId) ? details.Addressee : details.BulletinId;
        var receivedAt = packet.ReceivedAtUtc;
        var record = new AprsBulletinRecord(
            NormalizeBulletinId(bulletinId),
            sender,
            details.Addressee,
            details.MessageBody,
            packet.RawLine,
            receivedAt,
            source,
            receivedAt,
            IsActive: !string.IsNullOrWhiteSpace(details.MessageBody),
            ExpiresAtUtc: configuration.BulletinLifetime is null ? null : receivedAt.Add(configuration.BulletinLifetime.Value),
            packet.ValidationErrors);

        bulletins[CreateKey(sender, record.BulletinId)] = record;
        return new AprsBulletinAcceptResult(record, null, null);
    }

    private AprsBulletinAcceptResult StoreAnnouncement(MessageAprsPacket packet, AprsPacketSource source)
    {
        var sender = FormatSourceCallsign(packet.SourceCallsign, packet.SourceSsid);
        var details = ExtractMessageDetails(packet);
        var announcementId = string.IsNullOrWhiteSpace(details.BulletinId) ? details.Addressee : details.BulletinId;
        var receivedAt = packet.ReceivedAtUtc;
        var record = new AprsAnnouncementRecord(
            NormalizeBulletinId(announcementId),
            sender,
            details.Addressee,
            details.MessageBody,
            packet.RawLine,
            receivedAt,
            source,
            receivedAt,
            IsActive: !string.IsNullOrWhiteSpace(details.MessageBody),
            ExpiresAtUtc: configuration.AnnouncementLifetime is null ? null : receivedAt.Add(configuration.AnnouncementLifetime.Value),
            packet.ValidationErrors);

        announcements[CreateKey(sender, record.AnnouncementId)] = record;
        return new AprsBulletinAcceptResult(null, record, null);
    }

    private AprsBulletinAcceptResult StoreMessageQuery(MessageAprsPacket packet, AprsPacketSource source)
    {
        var record = CreateQuery(
            FormatSourceCallsign(packet.SourceCallsign, packet.SourceSsid),
            packet.QueryText ?? packet.RawMessageBody,
            packet.RawLine,
            packet.ReceivedAtUtc,
            source,
            packet.ValidationErrors);
        queries.Add(record);
        return new AprsBulletinAcceptResult(null, null, record);
    }

    private AprsBulletinAcceptResult StoreDirectQuery(QueryAprsPacket packet, AprsPacketSource source)
    {
        var record = CreateQuery(
            FormatSourceCallsign(packet.SourceCallsign, packet.SourceSsid),
            packet.QueryText,
            packet.RawLine,
            packet.ReceivedAtUtc,
            source,
            packet.ValidationErrors);
        queries.Add(record);
        return new AprsBulletinAcceptResult(null, null, record);
    }

    private static AprsQueryRecord CreateQuery(
        string sender,
        string queryBody,
        string rawPacket,
        DateTimeOffset receivedAtUtc,
        AprsPacketSource source,
        IReadOnlyList<string> validationErrors)
    {
        var trimmed = string.IsNullOrWhiteSpace(queryBody) ? string.Empty : queryBody.Trim();
        var queryType = trimmed.StartsWith('?') ? trimmed[1..] : trimmed;
        var separatorIndex = queryType.IndexOfAny([' ', ',']);
        if (separatorIndex >= 0)
        {
            queryType = queryType[..separatorIndex];
        }

        return new AprsQueryRecord(
            string.IsNullOrWhiteSpace(queryType) ? "Unknown" : queryType.ToUpperInvariant(),
            sender,
            trimmed,
            rawPacket,
            receivedAtUtc,
            source,
            validationErrors);
    }

    private static (string Addressee, string BulletinId, string MessageBody) ExtractMessageDetails(MessageAprsPacket packet)
    {
        var addressee = packet.Addressee;
        var messageBody = packet.MessageBody;

        if ((addressee.Contains(':', StringComparison.Ordinal) || string.IsNullOrEmpty(messageBody))
            && packet.Information.StartsWith(':'))
        {
            var bodySeparatorIndex = packet.Information.IndexOf(':', 1);
            if (bodySeparatorIndex > 1)
            {
                addressee = packet.Information[1..bodySeparatorIndex].TrimEnd();
                messageBody = packet.Information[(bodySeparatorIndex + 1)..];
            }
        }

        var bulletinId = addressee.StartsWith("BLN", StringComparison.OrdinalIgnoreCase) && addressee.Length > 3
            ? addressee[3..]
            : packet.BulletinId ?? addressee;

        return (addressee, bulletinId, messageBody);
    }

    private static bool IsActive(bool activeFlag, DateTimeOffset? expiresAtUtc, DateTimeOffset now)
    {
        return activeFlag && (expiresAtUtc is null || expiresAtUtc > now);
    }

    private static string CreateKey(string sender, string id)
    {
        return $"{sender}|{id}".ToUpperInvariant();
    }

    private static string NormalizeBulletinId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "UNKNOWN" : value.Trim().TrimEnd(':').ToUpperInvariant();
    }

    private static string FormatSourceCallsign(string callsign, int? ssid)
    {
        var normalized = string.IsNullOrWhiteSpace(callsign) ? string.Empty : callsign.Trim().ToUpperInvariant();
        return ssid is null ? normalized : $"{normalized}-{ssid}";
    }
}
