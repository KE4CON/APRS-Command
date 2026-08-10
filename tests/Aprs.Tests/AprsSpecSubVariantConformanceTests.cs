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
    /// §9: a compressed position's leading byte is the Symbol Table Identifier, which may be an OVERLAY
    /// letter ('A'–'Z' uppercase, or 'a'–'j' for numeric overlays 0–9) — not only '/' or '\'. Deep-audit
    /// (Dire-Wolf-confirmed): overlaid compressed positions were misrouted to the uncompressed parser and
    /// lost. All three overlay forms must decode to the same coordinates as the primary-table form.
    /// </summary>
    [Theory]
    [InlineData("N0CALL>APRS:!A5L!!<*e7>7P[")] // overlay 'A'
    [InlineData("N0CALL>APRS:!a5L!!<*e7>7P[")] // overlay '0' (encoded as 'a')
    [InlineData("N0CALL>APRS:!S5L!!<*e7>7P[")] // alternate-table symbol 'S'
    public void Spec_CompressedPosition_WithOverlaySymbol_Decoded(string raw)
    {
        var pos = Assert.IsType<PositionAprsPacket>(Parse(raw));

        Assert.True(pos.IsValid);
        Assert.Equal(49.5, pos.Latitude!.Value, 1);
        Assert.Equal(-72.75, pos.Longitude!.Value, 2);
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
    /// §12 Compressed weather (verified against Dire Wolf 1.8.1 <c>decode_aprs</c>): a compressed
    /// position whose symbol code is '_' is a weather station. The wind direction/speed ride in the
    /// compressed course/speed bytes; the remaining fields (gust/temp/rain/humidity/baro) follow.
    /// REGRESSION FIX: this previously surfaced as a plain position with the wx data dropped.
    /// </summary>
    [Fact]
    public void Spec_CompressedWeather_DecodedWithWindFromCompressedCourseSpeed()
    {
        var wx = Assert.IsType<WeatherAprsPacket>(
            Parse("N0CALL>APRS:!/5L!!<*e7_7P[g010t072r000p000P000h50b10132"));

        Assert.Equal(49.5, wx.Latitude!.Value, 1);
        Assert.Equal(-72.75, wx.Longitude!.Value, 2);
        Assert.Equal(88, wx.WindDirectionDegrees);        // Dire Wolf: direction 88
        Assert.InRange(wx.WindSpeedMph!.Value, 40, 43);    // Dire Wolf: 41.7 mph (from the cs bytes)
        Assert.Equal(10, wx.WindGustMph);
        Assert.Equal(72, wx.TemperatureFahrenheit);
        Assert.Equal(50, wx.HumidityPercent);
    }

    /// <summary>
    /// Base-91 / compressed telemetry: a packet whose info field starts with the '|' DTI is NOT a
    /// standalone type — Dire Wolf 1.8.1 also reports "Unknown APRS Data Type Indicator |". So our
    /// Unknown classification is correct and matches the reference decoder (base-91 telemetry rides
    /// inside other packets' comments, out of scope here).
    /// </summary>
    [Fact]
    public void Base91Telemetry_PipeDti_UnknownMatchesDireWolf()
    {
        Assert.IsType<UnknownAprsPacket>(Parse("N0CALL>APRS:|ss11223344|"));
    }
}
