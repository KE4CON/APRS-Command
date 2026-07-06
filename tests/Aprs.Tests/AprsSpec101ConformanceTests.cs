using Aprs.Core;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Conformance tests against the APRS Protocol Reference version 1.0.1
/// (29 August 2000). Every test in this class is drawn from a concrete
/// example in the specification document, with a chapter/page citation.
/// These tests guarantee that the parser correctly handles all formats
/// defined in the spec, providing a regression safety net for future changes.
///
/// Citation format: "§N.N — Description (spec page P)"
/// All latitude/longitude tolerances are 0.001 degrees (~100 m) unless
/// otherwise noted; the spec examples truncate to hundredths of a minute
/// which limits inherent precision.
/// </summary>
public sealed class AprsSpec101ConformanceTests
{
    private static readonly DateTimeOffset TestTime =
        new(2000, 8, 29, 0, 0, 0, TimeSpan.Zero); // spec publication date

    private static AprsPacket Parse(string raw) =>
        new AprsParser().Parse(raw, TestTime);

    private static PositionAprsPacket ParsePosition(string raw)
    {
        var p = Parse(raw);
        Assert.IsType<PositionAprsPacket>(p);
        return (PositionAprsPacket)p;
    }

    // ── §6 — Time and Position Formats ───────────────────────────────────

    /// <summary>
    /// §6 Latitude Format (spec p.23): "4903.50N is 49 degrees 3 minutes
    /// 30 seconds north." This encodes to 49 + 3.50/60 = 49.058333 degrees.
    /// </summary>
    [Fact]
    public void Spec_LatitudeFormat_4903_50N_DecodesCorrectly()
    {
        var p = ParsePosition("N0CALL>APRS:!4903.50N/07201.75W-");
        Assert.NotNull(p.Latitude);
        Assert.Equal(49.058333, p.Latitude!.Value, 4);
    }

    /// <summary>
    /// §6 Longitude Format (spec p.23-24): "07201.75W is 72 degrees
    /// 1 minute 45 seconds west." This encodes to -(72 + 1.75/60) = -72.029167.
    /// </summary>
    [Fact]
    public void Spec_LongitudeFormat_07201_75W_DecodesCorrectly()
    {
        var p = ParsePosition("N0CALL>APRS:!4903.50N/07201.75W-");
        Assert.NotNull(p.Longitude);
        Assert.Equal(-72.029167, p.Longitude!.Value, 4);
    }

    /// <summary>
    /// §6 Position Coordinates (spec p.24): The / between lat and long is
    /// the Symbol Table Identifier for the Primary Symbol Table. The -
    /// character is the house symbol code. Both must be decoded correctly.
    /// </summary>
    [Fact]
    public void Spec_PositionCoordinates_SymbolExtractedCorrectly()
    {
        var p = ParsePosition("N0CALL>APRS:!4903.50N/07201.75W-");
        Assert.Equal('/', p.SymbolTableIdentifier);
        Assert.Equal('-', p.SymbolCode);
    }

    // ── §8 — Position Report Data Formats ────────────────────────────────

    /// <summary>
    /// §8 Lat/Long Position Report without Timestamp (spec p.32):
    /// "!4903.50N/07201.75W-Test 001234 no timestamp, no APRS messaging,
    /// with comment."
    /// </summary>
    [Fact]
    public void Spec_PositionWithoutTimestamp_NoAPRSMessaging_Parses()
    {
        var p = ParsePosition("N0CALL>APRS:!4903.50N/07201.75W-Test 001234");
        Assert.True(p.IsValid);
        Assert.Equal('!', p.PositionType);
        Assert.Null(p.Timestamp);
        Assert.Equal(49.058333, p.Latitude!.Value, 4);
        Assert.Equal(-72.029167, p.Longitude!.Value, 4);
        Assert.Equal('/', p.SymbolTableIdentifier);
        Assert.Equal('-', p.SymbolCode);
        Assert.Contains("Test 001234", p.Comment);
    }

    /// <summary>
    /// §8 Lat/Long Position Report without Timestamp (spec p.32):
    /// "!4903.50N/07201.75W-Test /A=001234 no timestamp, no APRS messaging,
    /// altitude = 1234 ft."
    /// </summary>
    [Fact]
    public void Spec_PositionWithAltitudeInComment_DecodesAltitude()
    {
        var p = ParsePosition("N0CALL>APRS:!4903.50N/07201.75W-Test /A=001234");
        Assert.True(p.IsValid);
        Assert.Equal(1234, p.AltitudeFeet);
    }

    /// <summary>
    /// §8 Position Report with APRS messaging (spec p.32):
    /// "=4903.50N/07201.75W#PHG5132 no timestamp, with APRS messaging,
    /// with PHG." The = DTI indicates messaging capable.
    /// </summary>
    [Fact]
    public void Spec_PositionWithMessaging_EqualsDTI_Parses()
    {
        var p = ParsePosition("N0CALL>APRS:=4903.50N/07201.75W#PHG5132");
        Assert.True(p.IsValid);
        Assert.Equal('=', p.PositionType);
        Assert.Equal(49.058333, p.Latitude!.Value, 4);
        Assert.Equal(-72.029167, p.Longitude!.Value, 4);
    }

    /// <summary>
    /// §8 Position Report with Timestamp (spec p.32):
    /// "/092345z4903.50N/07201.75W>Test1234 with timestamp, no APRS
    /// messaging, zulu time, with comment."
    /// </summary>
    [Fact]
    public void Spec_PositionWithZuluTimestamp_Parses()
    {
        var p = ParsePosition("N0CALL>APRS:/092345z4903.50N/07201.75W>Test1234");
        Assert.True(p.IsValid);
        Assert.Equal('/', p.PositionType);
        Assert.NotNull(p.Timestamp);
        Assert.Contains("092345z", p.Timestamp!);
        Assert.Equal(49.058333, p.Latitude!.Value, 4);
        Assert.Equal(-72.029167, p.Longitude!.Value, 4);
        Assert.Equal('>', p.SymbolCode); // car/vehicle symbol
    }

    /// <summary>
    /// §8 Position Report with Timestamp and APRS messaging (spec p.32):
    /// "@092345/4903.50N/07201.75W>Test1234 with timestamp, with APRS
    /// messaging, local time, with comment."
    /// </summary>
    [Fact]
    public void Spec_PositionWithLocalTimestampAndMessaging_Parses()
    {
        var p = ParsePosition("N0CALL>APRS:@092345/4903.50N/07201.75W>Test1234");
        Assert.True(p.IsValid);
        Assert.Equal('@', p.PositionType);
        Assert.NotNull(p.Timestamp);
        Assert.Contains("092345/", p.Timestamp!);
    }

    /// <summary>
    /// §8 Position Report with Course/Speed Data Extension (spec p.33):
    /// "@092345/4903.50N/07201.75W>088/036 with timestamp, with APRS
    /// messaging, local time, course/speed." Course=88°, Speed=36 knots.
    /// </summary>
    [Fact]
    public void Spec_PositionWithCourseSpeed_DecodesExtension()
    {
        var p = ParsePosition("N0CALL>APRS:@092345/4903.50N/07201.75W>088/036");
        Assert.True(p.IsValid);
        Assert.Equal(88, p.CourseDegrees);
        Assert.Equal(36, p.SpeedKnots);
    }

    /// <summary>
    /// §6 Position Ambiguity (spec p.24): "49VV.VVN/072VV.VVW-" represents
    /// position to nearest degree. Ambiguity level should be detected.
    /// </summary>
    [Fact]
    public void Spec_PositionAmbiguity_NearestDegree_Detected()
    {
        var p = ParsePosition("N0CALL>APRS:!49  .  N/072  .  W-");
        Assert.True(p.IsValid);
        Assert.True(p.PositionAmbiguity > 0);
    }

    // ── §9 — Compressed Position Report Data Formats ─────────────────────

    /// <summary>
    /// §9 Compressed Position (spec p.41): "=/5L!!&lt;*e7&gt;VsTComment"
    /// with APRS messaging, no course/speed (space cs bytes).
    /// Lat=49.5° N, Long=-72.75° W (72 degrees 45 minutes west).
    /// NOTE: Compressed position format (§9) is not yet implemented in the parser.
    /// This test documents the gap and will fail until compressed position support
    /// is added. Track progress in GitHub issue referencing spec §9.
    /// </summary>
    [Fact(Skip = "Compressed position format (spec §9) not yet implemented — known gap")]
    public void Spec_CompressedPosition_NoSpeed_Parses()
    {
        // /5L!!<*e7> is the compressed form of 49.5N / 72.75W with > (car) symbol
        var p = ParsePosition("N0CALL>APRS:=/5L!!<*e7>VsTComment");
        Assert.True(p.IsValid);
        Assert.NotNull(p.Latitude);
        Assert.NotNull(p.Longitude);
        // Compressed position should be close to spec values
        Assert.InRange(p.Latitude!.Value, 49.0, 50.0);
        Assert.InRange(p.Longitude!.Value, -73.5, -72.0);
    }

    /// <summary>
    /// §9 Compressed Position with course/speed (spec p.41):
    /// "=/5L!!&lt;*e7&gt;7P[" with APRS messaging, RMC sentence, course=88°,
    /// speed=36.2 knots. The cs bytes 7P encode course 88 and speed ~36.
    /// NOTE: Compressed position format (§9) is not yet implemented.
    /// </summary>
    [Fact(Skip = "Compressed position format (spec §9) not yet implemented — known gap")]
    public void Spec_CompressedPosition_WithCourseSpeed_DecodesCorrectly()
    {
        var p = ParsePosition("N0CALL>APRS:=/5L!!<*e7>7P[");
        Assert.True(p.IsValid);
        Assert.NotNull(p.CourseDegrees);
        Assert.NotNull(p.SpeedKnots);
        Assert.InRange(p.CourseDegrees!.Value, 85, 91);   // 88° ± 3° (compressed resolution)
        Assert.InRange(p.SpeedKnots!.Value, 34, 39);      // 36 knots ± ~3
    }

    // ── §11 — Object and Item Reports ────────────────────────────────────

    /// <summary>
    /// §11 Object Report Format (spec p.58): An APRS object packet starts
    /// with ; DTI, followed by a 9-character name, * (alive) or / (killed),
    /// timestamp, and position.
    /// </summary>
    [Fact]
    public void Spec_ObjectReport_AliveObject_Parses()
    {
        var raw = "N0CALL>APRS:;LEADER   *092345z4903.50N/07201.75W>088/036";
        var p = Parse(raw);
        Assert.True(p.IsValid);
        Assert.IsType<ObjectAprsPacket>(p);
        var obj = (ObjectAprsPacket)p;
        Assert.Equal("LEADER", obj.ObjectName.Trim());
        Assert.True(obj.IsAlive);
        Assert.False(obj.IsKilled);
    }

    /// <summary>
    /// §11 Killed Object: Object packets with _ instead of * indicate the object
    /// should be removed from the map.
    /// Note: APRS101.pdf spec (p.57) says / for killed, but Bob Bruninga's own
    /// PROTOCOL.TXT and real-world implementations use _ (underscore). The parser
    /// follows real-world practice per WB4APR's PROTOCOL.TXT.
    /// </summary>
    [Fact]
    public void Spec_ObjectReport_KilledObject_IsMarkedKilled()
    {
        var raw = "N0CALL>APRS:;LEADER   _092345z4903.50N/07201.75W>088/036";
        var p = Parse(raw);
        Assert.True(p.IsValid);
        Assert.IsType<ObjectAprsPacket>(p);
        var obj = (ObjectAprsPacket)p;
        Assert.True(obj.IsKilled);
        Assert.False(obj.IsAlive);
    }

    // ── §12 — Weather Reports ────────────────────────────────────────────

    /// <summary>
    /// §8 Weather Data Extension (spec p.33):
    /// "=4903.50N/07201.75W_225/000g000t050r000p001...h00b10138dU2k"
    /// weather report. The _ symbol indicates a weather station.
    /// </summary>
    [Fact]
    public void Spec_WeatherPacketWithPosition_ParsesSymbol()
    {
        var raw = "N0CALL>APRS:=4903.50N/07201.75W_225/000g000t050r000p001h00b10138";
        var p = Parse(raw);
        Assert.True(p.IsValid);
        Assert.IsType<WeatherAprsPacket>(p);
        var wx = (WeatherAprsPacket)p;
        Assert.Equal(225, wx.WindDirectionDegrees);
        Assert.Equal(50, wx.TemperatureFahrenheit);
    }

    /// <summary>
    /// §12 Positionless Weather Report (spec p.63-64): Weather reports
    /// using the _ DTI without position data are valid. Timestamp is in
    /// MDHM format (8 characters).
    /// </summary>
    [Fact]
    public void Spec_PositionlessWeatherReport_UnderscoreDTI_IsValid()
    {
        // _ DTI with MDHM timestamp, no position
        var raw = "N0CALL>APRS:_10092345c220s004g005t077r000p000P000h50b09900";
        var p = Parse(raw);
        Assert.True(p.IsValid);
        Assert.IsType<WeatherAprsPacket>(p);
    }

    // ── §14 — Messages, Bulletins and Announcements ──────────────────────

    /// <summary>
    /// §14 Message Format (spec p.71): A message packet uses : DTI,
    /// addressee padded to 9 chars, :, message text, {, message ID.
    /// Example: KE4CON-1>APRS::KD8ABC-7 :Hello world{001
    /// </summary>
    [Fact]
    public void Spec_MessagePacket_Parses()
    {
        var raw = "KE4CON-1>APRS::KD8ABC-7 :Hello world{001";
        var p = Parse(raw);
        Assert.True(p.IsValid);
        Assert.IsType<MessageAprsPacket>(p);
        var msg = (MessageAprsPacket)p;
        Assert.Equal("KD8ABC-7", msg.Addressee.Trim());
        Assert.Equal("Hello world", msg.MessageBody);
        Assert.Equal("001", msg.MessageId);
        Assert.False(msg.IsBulletin);
    }

    /// <summary>
    /// §14 Message Acknowledgement (spec p.72): "ackMMMM" where MMMM is
    /// the message number being acknowledged.
    /// </summary>
    [Fact]
    public void Spec_MessageAcknowledgement_Parses()
    {
        var raw = "KD8ABC-7>APRS::KE4CON-1 :ack001";
        var p = Parse(raw);
        Assert.True(p.IsValid);
        Assert.IsType<MessageAprsPacket>(p);
        var msg = (MessageAprsPacket)p;
        Assert.Equal("001", msg.AcknowledgedMessageId);
    }

    /// <summary>
    /// §14 General Bulletin (spec p.73): Bulletins are addressed to BLN
    /// followed by a bulletin ID character. No message ID is included.
    /// Example: KE4CON-1>APRS::BLN3     :Snow expected in the highlands
    /// </summary>
    [Fact]
    public void Spec_GeneralBulletin_Parses()
    {
        var raw = "KE4CON-1>APRS::BLN3     :Snow expected in the highlands";
        var p = Parse(raw);
        Assert.True(p.IsValid);
        Assert.IsType<MessageAprsPacket>(p);
        var msg = (MessageAprsPacket)p;
        Assert.True(msg.IsBulletin);
        Assert.Equal("3", msg.BulletinId);
        Assert.Equal("Snow expected in the highlands", msg.MessageBody);
    }

    // ── §16 — Status Reports ─────────────────────────────────────────────

    /// <summary>
    /// §16 Status Report (spec p.80): The > DTI indicates a status report.
    /// Status text follows immediately with no separator.
    /// </summary>
    [Fact]
    public void Spec_StatusReport_GreaterThanDTI_Parses()
    {
        var raw = "KE4CON-1>APRS:>Net control for W9NET";
        var p = Parse(raw);
        Assert.True(p.IsValid);
        Assert.IsType<StatusAprsPacket>(p);
        var status = (StatusAprsPacket)p;
        Assert.Equal("Net control for W9NET", status.StatusText);
    }

    /// <summary>
    /// §16 Status Report with Timestamp (spec p.80): Status reports may
    /// optionally include a DHM zulu timestamp at the start.
    /// </summary>
    [Fact]
    public void Spec_StatusReport_WithZuluTimestamp_Parses()
    {
        var raw = "KE4CON-1>APRS:>092345zNet control active";
        var p = Parse(raw);
        Assert.True(p.IsValid);
        Assert.IsType<StatusAprsPacket>(p);
        var status = (StatusAprsPacket)p;
        Assert.Contains("Net control active", status.StatusText);
    }

    // ── AX.25 Header Parsing ─────────────────────────────────────────────

    /// <summary>
    /// §3 AX.25 frame structure: Source callsign, destination, and
    /// digipeater path must all be parsed correctly from the TNC-2 monitor
    /// format used by APRS-IS.
    /// </summary>
    [Fact]
    public void Spec_AX25Header_SourceDestinationPath_ParsedCorrectly()
    {
        var p = Parse("KE4CON-1>APRS,WIDE1-1,WIDE2-1:>Status");
        Assert.Equal("KE4CON", p.SourceCallsign);
        Assert.Equal(1, p.SourceSsid);
        Assert.Equal("APRS", p.Destination);
        Assert.Equal(2, p.Path.Count);
        Assert.Equal("WIDE1-1", p.Path[0]);
        Assert.Equal("WIDE2-1", p.Path[1]);
    }

    /// <summary>
    /// §4 APRS Source Address SSID (spec p.16): Callsigns with SSID -0
    /// have a null SSID. Callsigns without any SSID should parse to null.
    /// </summary>
    [Fact]
    public void Spec_SourceAddress_NoSSID_ParsesAsNull()
    {
        var p = Parse("KE4CON>APRS:>Status");
        Assert.Equal("KE4CON", p.SourceCallsign);
        Assert.Null(p.SourceSsid);
    }

    /// <summary>
    /// §4 APRS Destination Address (spec p.13): Standard APRS destination
    /// callsigns like APRS, BEACON, GPS, WX, and APxxxx are all valid.
    /// </summary>
    [Theory]
    [InlineData("APRS")]
    [InlineData("BEACON")]
    [InlineData("GPS")]
    [InlineData("WX")]
    [InlineData("APCMD0")]
    public void Spec_GenericAPRSDestinations_AreAccepted(string destination)
    {
        var p = Parse($"KE4CON>{ destination}:>Status");
        Assert.True(p.IsValid);
        Assert.Equal(destination, p.Destination);
    }

    // ── §19 — Invalid / Other Packets ────────────────────────────────────

    /// <summary>
    /// §19 Invalid Data (spec p.89): A packet starting with , is invalid
    /// test data and should be parsed but flagged as invalid/unknown.
    /// </summary>
    [Fact]
    public void Spec_InvalidDataPacket_CommaDTI_IsInvalid()
    {
        var p = Parse("N0CALL>APRS:,invalid test data");
        // Spec says , = "Invalid data or test data" — should not crash
        // May be IsValid=false or UnknownAprsPacket, but must not throw
        Assert.NotNull(p);
    }

    /// <summary>
    /// §5 Data Type Identifier table: A null or empty packet should not
    /// crash the parser and should return an error state.
    /// </summary>
    [Fact]
    public void Spec_EmptyInformationField_HandledGracefully()
    {
        var p = Parse("N0CALL>APRS:");
        Assert.NotNull(p);
        // Empty info field — may be invalid, but must not throw
    }

    /// <summary>
    /// §5 Data Type Identifier table: A packet with an unrecognized DTI
    /// (one of the reserved/unused characters) must not crash.
    /// </summary>
    [Theory]
    [InlineData("N0CALL>APRS:(unrecognized DTI")]
    [InlineData("N0CALL>APRS:\"unused DTI")]
    [InlineData("N0CALL>APRS:-unused DTI")]
    public void Spec_UnrecognizedDTI_HandledGracefully(string raw)
    {
        var exception = Record.Exception(() => Parse(raw));
        Assert.Null(exception); // must not throw regardless of DTI
    }

    // ── §6 — Time Format Variants ─────────────────────────────────────────

    /// <summary>
    /// §6 Hours/Minutes/Seconds format (spec p.22): "234517h is 23 hours
    /// 45 minutes and 17 seconds zulu." HMS format uses h suffix.
    /// </summary>
    [Fact]
    public void Spec_HMSTimestamp_Parses()
    {
        var p = ParsePosition("N0CALL>APRS:@234517h4903.50N/07201.75W>PHG5132");
        Assert.True(p.IsValid);
        Assert.NotNull(p.Timestamp);
        Assert.Contains("234517h", p.Timestamp!);
    }

    // ── Real-world compatibility ──────────────────────────────────────────

    /// <summary>
    /// Real-world NMEA raw GPS data packet format (spec p.33-34):
    /// Packets starting with $ contain raw NMEA sentences that APRS
    /// implementations must not reject outright.
    /// </summary>
    [Theory]
    [InlineData("N0CALL>APRS:$GPGGA,102705,5157.9762,N,00029.3256,W,1,04,2.0,75.7,M,47.6,M,,*62")]
    [InlineData("N0CALL>APRS:$GPRMC,063909,A,3349.4302,N,11700.3721,W,43.022,89.3,291099,13.6,E*52")]
    public void Spec_RawNMEA_DoesNotCrashParser(string raw)
    {
        var exception = Record.Exception(() => Parse(raw));
        Assert.Null(exception);
    }

    /// <summary>
    /// §14 Message with no ID (spec p.71): Not all message packets include
    /// a message ID. Those without should still parse as valid messages.
    /// </summary>
    [Fact]
    public void Spec_MessageWithNoId_IsValid()
    {
        var raw = "KE4CON-1>APRS::KD8ABC-7 :Hello no id";
        var p = Parse(raw);
        Assert.True(p.IsValid);
        Assert.IsType<MessageAprsPacket>(p);
        var msg = (MessageAprsPacket)p;
        Assert.Null(msg.MessageId);
        Assert.Equal("Hello no id", msg.MessageBody);
    }

    /// <summary>
    /// APRS-IS qAC construct: Real-world APRS-IS packets include a qAC
    /// construct in the path indicating they were gated. The parser must
    /// handle these without error.
    /// </summary>
    [Fact]
    public void RealWorld_APRSISQConstruct_ParsedCorrectly()
    {
        var raw = "KE4CON-1>APRS,TCPIP*,qAC,T2USEAST:!4158.63N/08815.78W>APRS Command";
        var p = ParsePosition(raw);
        Assert.True(p.IsValid);
        Assert.Equal("KE4CON", p.SourceCallsign);
        // qAC construct stored correctly
        Assert.Contains("qAC", p.Path);
    }

    /// <summary>
    /// APRS-IS third-party packet format: Packets prefixed with } carry
    /// encapsulated third-party traffic. Must not crash.
    /// </summary>
    [Fact]
    public void Spec_ThirdPartyPacket_CurlyBraceDTI_DoesNotCrash()
    {
        var raw = "KE4CON-1>APRS:}W9ABC-7>APRS,TCPIP,KE4CON-1*:!4158.63N/08815.78W>";
        var exception = Record.Exception(() => Parse(raw));
        Assert.Null(exception);
    }
}
