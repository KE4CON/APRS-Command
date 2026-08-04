using Aprs.Core;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Phase-5 sub-variant conformance: the "(verify)" items from the Phase-0 matrix, each closed with a
/// spec vector or formally flagged. Several of these caught real parser bugs (group-bulletin
/// misclassification, compressed objects/items not decoding) that are now fixed.
/// </summary>
public sealed class AprsSpecSubVariantConformanceTests
{
    private static readonly DateTimeOffset TestTime = new(2000, 8, 29, 0, 0, 0, TimeSpan.Zero);
    private static AprsPacket Parse(string raw) => new AprsParser().Parse(raw, TestTime);

    // ── §14 — Bulletins, announcements, group bulletins ──────────────────

    /// <summary>
    /// §14 Announcement (spec p.74): "BLN" + a LETTER identifier (e.g. BLNA) is an announcement.
    /// </summary>
    [Fact]
    public void Spec_Announcement_LetterIdentifier_IsAnnouncement()
    {
        var msg = Assert.IsType<MessageAprsPacket>(Parse("N0CALL>APRS::BLNA     :Field Day this weekend"));

        Assert.True(msg.IsBulletin);
        Assert.True(msg.IsAnnouncement);
        Assert.Equal("A", msg.BulletinId);
        Assert.Null(msg.BulletinGroup);
    }

    /// <summary>
    /// §14 General bulletin (spec p.73): "BLN" + a DIGIT + spaces (BLN1) is a numbered bulletin,
    /// not an announcement, and has no group.
    /// </summary>
    [Fact]
    public void Spec_GeneralBulletin_DigitIdentifier_NoGroupNotAnnouncement()
    {
        var msg = Assert.IsType<MessageAprsPacket>(Parse("N0CALL>APRS::BLN1     :Snow expected"));

        Assert.True(msg.IsBulletin);
        Assert.False(msg.IsAnnouncement);
        Assert.Equal("1", msg.BulletinId);
        Assert.Null(msg.BulletinGroup);
    }

    /// <summary>
    /// §14 Group bulletin (spec p.73): "BLN" + a DIGIT + a group name (BLN1WX) targets a group.
    /// REGRESSION FIX: the group name's letters previously mis-flagged this as an announcement, and
    /// the digit + group were lumped into the id. Now the digit is the id and "WX" is the group.
    /// </summary>
    [Fact]
    public void Spec_GroupBulletin_DigitPlusGroup_SeparatedAndNotAnnouncement()
    {
        var msg = Assert.IsType<MessageAprsPacket>(Parse("N0CALL>APRS::BLN1WX   :Storm warning"));

        Assert.True(msg.IsBulletin);
        Assert.False(msg.IsAnnouncement);
        Assert.Equal("1", msg.BulletinId);
        Assert.Equal("WX", msg.BulletinGroup);
    }

    /// <summary>
    /// §14 Message length (spec p.71): message text is up to 67 characters. The parser accepts
    /// standard-length text; it is intentionally lenient about over-length text on receive (real
    /// traffic occasionally exceeds it) rather than rejecting the packet.
    /// </summary>
    [Theory]
    [InlineData(67)]
    [InlineData(80)]
    public void Spec_MessageLength_AcceptedLeniently(int length)
    {
        var msg = Assert.IsType<MessageAprsPacket>(Parse("N0CALL>APRS::N0CALL-2 :" + new string('x', length)));

        Assert.True(msg.IsValid);
        Assert.Equal(length, msg.MessageBody.Length);
    }

    // ── §9/§11 — Compressed positions inside objects and items ────────────

    /// <summary>
    /// §9 + §11: an object may carry a base-91 compressed position. REGRESSION FIX: object positions
    /// only handled the uncompressed form, so compressed objects failed to decode (invalid, no
    /// coordinates). Now the shared position parser detects and decodes the compressed form.
    /// </summary>
    [Fact]
    public void Spec_CompressedObject_PositionDecoded()
    {
        var obj = Assert.IsType<ObjectAprsPacket>(Parse("N0CALL>APRS:;OBJECT   *092345z/5L!!<*e7>{?!"));

        Assert.True(obj.IsValid);
        Assert.Equal("OBJECT", obj.ObjectName.Trim());
        Assert.Equal(49.5, obj.Latitude!.Value, 1);
        Assert.Equal(-72.75, obj.Longitude!.Value, 2);
    }

    /// <summary>
    /// §9 + §11: an item may likewise carry a compressed position (same fix as objects).
    /// </summary>
    [Fact]
    public void Spec_CompressedItem_PositionDecoded()
    {
        var item = Assert.IsType<ItemAprsPacket>(Parse("N0CALL>APRS:)ITEM!/5L!!<*e7>{?!"));

        Assert.True(item.IsValid);
        Assert.Equal("ITEM", item.ItemName);
        Assert.Equal(49.5, item.Latitude!.Value, 1);
        Assert.Equal(-72.75, item.Longitude!.Value, 2);
    }

    // ── Flagged residuals (current behavior pinned; tracked in the plan) ──

    /// <summary>
    /// KNOWN LIMITATION (tracked in APRS_SPEC_CONFORMANCE_PLAN.md): a compressed <em>weather</em>
    /// report decodes its base-91 position correctly but does NOT yet extract the weather fields —
    /// it surfaces as a position, with the wx data left in the comment. Full compressed-weather
    /// decode is deferred (rare form). This test pins the current behavior.
    /// </summary>
    [Fact]
    public void CompressedWeather_PositionDecoded_WeatherNotYetExtracted()
    {
        var packet = Parse("N0CALL>APRS:!/5L!!<*e7_225/000g000t050r000p001");

        var position = Assert.IsType<PositionAprsPacket>(packet); // not (yet) a WeatherAprsPacket
        Assert.Equal(49.5, position.Latitude!.Value, 1);
    }

    /// <summary>
    /// KNOWN LIMITATION (tracked in the plan): base-91 / compressed telemetry (the '|' DTI, an
    /// aprs12 addition) is not decoded and falls through to Unknown. Deferred (rare, newer form).
    /// </summary>
    [Fact]
    public void Base91Telemetry_PipeDti_CurrentlyUnknown()
    {
        Assert.IsType<UnknownAprsPacket>(Parse("N0CALL>APRS:|ss11223344|"));
    }
}
