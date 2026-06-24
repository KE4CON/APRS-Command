using Aprs.Core;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsParserTests
{
    private static readonly DateTimeOffset ReceivedAtUtc = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(
        "N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Test beacon",
        "N0CALL",
        null,
        "APRS",
        new[] { "TCPIP*" },
        "!3903.50N/08430.50W-Test beacon")]
    [InlineData(
        "W1AW-9>APRS,WIDE1-1,WIDE2-1:=4123.45N/07234.56W>Mobile test",
        "W1AW",
        9,
        "APRS",
        new[] { "WIDE1-1", "WIDE2-1" },
        "=4123.45N/07234.56W>Mobile test")]
    [InlineData(
        "K8ABC>APRS::N0CALL   :Hello{01",
        "K8ABC",
        null,
        "APRS",
        new string[] { },
        ":N0CALL   :Hello{01")]
    public void TryParse_ReturnsRawPacket_ForValidAprsTextLine(
        string rawLine,
        string expectedSourceCallsign,
        int? expectedSourceSsid,
        string expectedDestination,
        string[] expectedPath,
        string expectedInformation)
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

        Assert.NotNull(packet);
        Assert.True(isValid);
        Assert.True(packet.IsValid);
        Assert.Null(error);
        Assert.Empty(packet.ValidationErrors);
        Assert.Equal(rawLine, packet.RawLine);
        Assert.Equal(expectedSourceCallsign, packet.SourceCallsign);
        Assert.Equal(expectedSourceSsid, packet.SourceSsid);
        Assert.Equal(expectedDestination, packet.Destination);
        Assert.Equal(expectedPath, packet.Path);
        Assert.Equal(expectedInformation, packet.Information);
        Assert.Equal(ReceivedAtUtc, packet.ReceivedAtUtc);
    }

    [Fact]
    public void TryParse_AllowsEmptyInformationField()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse("N0CALL>APRS:", ReceivedAtUtc, out var packet, out var error);

        var rawPacket = Assert.IsType<RawAprsPacket>(packet);
        Assert.True(isValid);
        Assert.True(rawPacket.IsValid);
        Assert.Null(error);
        Assert.Equal("N0CALL", rawPacket.SourceCallsign);
        Assert.Equal("APRS", rawPacket.Destination);
        Assert.Empty(rawPacket.Path);
        Assert.Equal(string.Empty, rawPacket.Information);
    }

    [Theory]
    [InlineData("BADPACKETWITHOUTSEPARATOR")]
    [InlineData(">APRS:missing source")]
    [InlineData("")]
    public void TryParse_ReturnsInvalidRawPacket_ForMalformedLine(string rawLine)
    {
        var parser = new AprsParser();

        var exception = Record.Exception(() =>
        {
            var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

            var rawPacket = Assert.IsType<RawAprsPacket>(packet);
            Assert.False(isValid);
            Assert.False(rawPacket.IsValid);
            Assert.NotEmpty(rawPacket.ValidationErrors);
            Assert.Equal(rawLine, rawPacket.RawLine);
            Assert.Equal(rawPacket.ValidationErrors[0], error);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void TryParse_ExtractsValidationErrors_ForMissingSource()
    {
        var parser = new AprsParser();

        parser.TryParse(">APRS:missing source", ReceivedAtUtc, out var packet, out var error);

        var rawPacket = Assert.IsType<RawAprsPacket>(packet);
        Assert.Equal("APRS", rawPacket.Destination);
        Assert.Equal("missing source", rawPacket.Information);
        Assert.Contains("Packet source callsign is missing.", rawPacket.ValidationErrors);
        Assert.Equal("Packet source callsign is missing.", error);
    }

    [Fact]
    public void TryParse_ExtractsQConstruct_WhenPresentInPath()
    {
        var parser = new AprsParser();

        parser.TryParse("N0CALL>APRS,TCPIP*,qAC,T2SERVER:>status", ReceivedAtUtc, out var packet, out _);

        var statusPacket = Assert.IsType<StatusAprsPacket>(packet);
        Assert.True(statusPacket.IsValid);
        Assert.Equal("qAC", statusPacket.QConstruct);
        Assert.Equal(new[] { "TCPIP*", "qAC", "T2SERVER" }, statusPacket.Path);
    }

    [Theory]
    [InlineData(
        "N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Test beacon",
        '!',
        null,
        39.058333,
        -84.508333,
        '/',
        '-',
        "Test beacon")]
    [InlineData(
        "W1AW-9>APRS,WIDE1-1,WIDE2-1:=4123.45N/07234.56W>Mobile test",
        '=',
        null,
        41.390833,
        -72.576,
        '/',
        '>',
        "Mobile test")]
    [InlineData(
        "K8ABC>APRS:/092345z3903.50N/08430.50W>Timestamped test",
        '/',
        "092345z",
        39.058333,
        -84.508333,
        '/',
        '>',
        "Timestamped test")]
    [InlineData(
        "N8XYZ-7>APRS:!3903.50N/08430.50W#PHG5130 Test digi",
        '!',
        null,
        39.058333,
        -84.508333,
        '/',
        '#',
        "PHG5130 Test digi")]
    public void TryParse_ReturnsPositionPacket_ForUncompressedPosition(
        string rawLine,
        char expectedPositionType,
        string? expectedTimestamp,
        double expectedLatitude,
        double expectedLongitude,
        char expectedSymbolTable,
        char expectedSymbolCode,
        string expectedComment)
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

        var positionPacket = Assert.IsType<PositionAprsPacket>(packet);
        Assert.True(isValid);
        Assert.True(positionPacket.IsValid);
        Assert.Null(error);
        Assert.Equal(expectedPositionType, positionPacket.PositionType);
        Assert.Equal(expectedTimestamp, positionPacket.Timestamp);
        Assert.Equal(expectedLatitude, positionPacket.Latitude!.Value, 6);
        Assert.Equal(expectedLongitude, positionPacket.Longitude!.Value, 6);
        Assert.Equal(expectedSymbolTable, positionPacket.SymbolTableIdentifier);
        Assert.Equal(expectedSymbolCode, positionPacket.SymbolCode);
        Assert.Equal(expectedComment, positionPacket.Comment);
        Assert.Null(positionPacket.CourseDegrees);
        Assert.Null(positionPacket.SpeedKnots);
        Assert.Null(positionPacket.AltitudeFeet);
        Assert.Equal(0, positionPacket.PositionAmbiguity);
    }

    [Fact]
    public void TryParse_ExtractsCourseSpeedAndAltitude_FromPositionComment()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "MOBILE-9>APRS:!3903.50N/08430.50W>123/045/A=000789 Moving test",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var positionPacket = Assert.IsType<PositionAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal(123, positionPacket.CourseDegrees);
        Assert.Equal(45, positionPacket.SpeedKnots);
        Assert.Equal(789, positionPacket.AltitudeFeet);
        Assert.Equal("123/045/A=000789 Moving test", positionPacket.Comment);
    }

    [Fact]
    public void TryParse_TracksPositionAmbiguity_WhenCoordinatesContainSpaces()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "N0CALL>APRS:!3903.  N/08430.  W-Ambiguous position",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var positionPacket = Assert.IsType<PositionAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal(2, positionPacket.PositionAmbiguity);
        Assert.Equal(39.05, positionPacket.Latitude!.Value, 6);
        Assert.Equal(-84.5, positionPacket.Longitude!.Value, 6);
    }

    [Fact]
    public void TryParse_ReturnsValidationErrors_ForBadPosition()
    {
        var parser = new AprsParser();

        var exception = Record.Exception(() =>
        {
            var isValid = parser.TryParse(
                "BADPOS>APRS:!9999.99N/99999.99W-Bad position",
                ReceivedAtUtc,
                out var packet,
                out var error);

            var positionPacket = Assert.IsType<PositionAprsPacket>(packet);
            Assert.False(isValid);
            Assert.False(positionPacket.IsValid);
            Assert.Null(positionPacket.Latitude);
            Assert.Null(positionPacket.Longitude);
            Assert.Contains("Position packet latitude is invalid.", positionPacket.ValidationErrors);
            Assert.Contains("Position packet longitude is invalid.", positionPacket.ValidationErrors);
            Assert.Equal("Position packet latitude is invalid.", error);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void TryParse_ReturnsStatusPacket_ForStatusInformation()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "N0CALL>APRS:>Net control station online",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var statusPacket = Assert.IsType<StatusAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("Net control station online", statusPacket.StatusText);
        Assert.Equal("Net control station online", statusPacket.RawStatusText);
    }

    [Fact]
    public void TryParse_AllowsEmptyStatusPacket()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse("K8ABC>APRS:>", ReceivedAtUtc, out var packet, out var error);

        var statusPacket = Assert.IsType<StatusAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal(string.Empty, statusPacket.StatusText);
        Assert.Equal(string.Empty, statusPacket.RawStatusText);
    }

    [Fact]
    public void TryParse_ReturnsTelemetryPacket_ForTelemetryValues()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "W1AW>APRS:T#001,111,222,333,444,555,10101010",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var telemetryPacket = Assert.IsType<TelemetryAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("001,111,222,333,444,555,10101010", telemetryPacket.RawTelemetryBody);
        Assert.Equal(1, telemetryPacket.SequenceNumber);
        Assert.Equal(new[] { 111, 222, 333, 444, 555 }, telemetryPacket.AnalogValues);
        Assert.Equal(new[] { true, false, true, false, true, false, true, false }, telemetryPacket.DigitalValues);
    }

    [Theory]
    [InlineData("WX9XYZ>APRS:PARM.Temp,Volt,Wind,Rain,Hum", "PARM", "Temp,Volt,Wind,Rain,Hum", new[] { "Temp", "Volt", "Wind", "Rain", "Hum" })]
    [InlineData("WX9XYZ>APRS:UNIT.F,Volts,MPH,In,%", "UNIT", "F,Volts,MPH,In,%", new[] { "F", "Volts", "MPH", "In", "%" })]
    [InlineData("WX9XYZ>APRS:EQNS.0,1,0,0,1,0,0,1,0,0,1,0,0,1,0", "EQNS", "0,1,0,0,1,0,0,1,0,0,1,0,0,1,0", new[] { "0", "1", "0", "0", "1", "0", "0", "1", "0", "0", "1", "0", "0", "1", "0" })]
    public void TryParse_ReturnsTelemetryMetadataPacket_ForMetadata(
        string rawLine,
        string expectedKind,
        string expectedBody,
        string[] expectedValues)
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

        var metadataPacket = Assert.IsType<TelemetryMetadataAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal(expectedKind, metadataPacket.MetadataKind);
        Assert.Equal(expectedBody, metadataPacket.RawMetadataBody);
        Assert.Equal(expectedValues, metadataPacket.Values);
        Assert.Empty(metadataPacket.BitValues);
        Assert.Null(metadataPacket.ProjectTitle);
    }

    [Fact]
    public void TryParse_ReturnsTelemetryBitsMetadata_WithProjectTitle()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "WX9XYZ>APRS:BITS.10101010,Weather station",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var metadataPacket = Assert.IsType<TelemetryMetadataAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("BITS", metadataPacket.MetadataKind);
        Assert.Equal("10101010,Weather station", metadataPacket.RawMetadataBody);
        Assert.Equal(new[] { true, false, true, false, true, false, true, false }, metadataPacket.BitValues);
        Assert.Equal("Weather station", metadataPacket.ProjectTitle);
    }

    [Fact]
    public void TryParse_ReturnsCapabilityPacket_ForCapabilityInformation()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "N0CALL>APRS:<IGATE,MSG_CNT=10",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var capabilityPacket = Assert.IsType<CapabilityAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("IGATE,MSG_CNT=10", capabilityPacket.CapabilityText);
    }

    [Fact]
    public void TryParse_ReturnsValidationErrors_ForMalformedTelemetry()
    {
        var parser = new AprsParser();

        var exception = Record.Exception(() =>
        {
            var isValid = parser.TryParse(
                "BADTEL>APRS:T#abc,not,valid",
                ReceivedAtUtc,
                out var packet,
                out var error);

            var telemetryPacket = Assert.IsType<TelemetryAprsPacket>(packet);
            Assert.False(isValid);
            Assert.False(telemetryPacket.IsValid);
            Assert.Equal("abc,not,valid", telemetryPacket.RawTelemetryBody);
            Assert.Null(telemetryPacket.SequenceNumber);
            Assert.Empty(telemetryPacket.AnalogValues);
            Assert.Contains("Telemetry sequence number is invalid.", telemetryPacket.ValidationErrors);
            Assert.Contains("Telemetry analog value is invalid.", telemetryPacket.ValidationErrors);
            Assert.Equal("Telemetry sequence number is invalid.", error);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void TryParse_ReturnsUnknownPacket_ForUnsupportedInformation()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "UNKNOWN>APRS:$APRSTEST",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var unknownPacket = Assert.IsType<UnknownAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("$APRSTEST", unknownPacket.Information);
        Assert.Equal("UNKNOWN>APRS:$APRSTEST", unknownPacket.RawLine);
    }

    [Fact]
    public void TryParse_ReturnsMessagePacket_ForStandardMessage()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "K8ABC>APRS::N0CALL   :Hello there{01",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var messagePacket = Assert.IsType<MessageAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("N0CALL", messagePacket.Addressee);
        Assert.Equal("Hello there{01", messagePacket.RawMessageBody);
        Assert.Equal("Hello there", messagePacket.MessageBody);
        Assert.Equal("01", messagePacket.MessageId);
        Assert.Null(messagePacket.AcknowledgedMessageId);
        Assert.Null(messagePacket.RejectedMessageId);
        Assert.False(messagePacket.IsBulletin);
        Assert.False(messagePacket.IsQuery);
    }

    [Theory]
    [InlineData("N0CALL>APRS::K8ABC    :ack01", "01", null)]
    [InlineData("N0CALL>APRS::K8ABC    :rej01", null, "01")]
    public void TryParse_DetectsMessageAckAndRej(string rawLine, string? expectedAckId, string? expectedRejId)
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

        var messagePacket = Assert.IsType<MessageAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("K8ABC", messagePacket.Addressee);
        Assert.Equal(expectedAckId, messagePacket.AcknowledgedMessageId);
        Assert.Equal(expectedRejId, messagePacket.RejectedMessageId);
    }

    [Theory]
    [InlineData("W1AW>APRS::BLN0     :Club meeting at 1900 local", "0", "Club meeting at 1900 local")]
    [InlineData("W1AW>APRS::BLN1     :Weather net at 2000", "1", "Weather net at 2000")]
    public void TryParse_DetectsBulletinMessages(string rawLine, string expectedBulletinId, string expectedText)
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

        var messagePacket = Assert.IsType<MessageAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.True(messagePacket.IsBulletin);
        Assert.False(messagePacket.IsAnnouncement);
        Assert.Equal(expectedBulletinId, messagePacket.BulletinId);
        Assert.Equal(expectedText, messagePacket.MessageBody);
    }

    [Fact]
    public void TryParse_AllowsEmptyMessageBody()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "K8ABC>APRS::N0CALL   :",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var messagePacket = Assert.IsType<MessageAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("N0CALL", messagePacket.Addressee);
        Assert.Equal(string.Empty, messagePacket.RawMessageBody);
        Assert.Equal(string.Empty, messagePacket.MessageBody);
    }

    [Theory]
    [InlineData("BADMSG>APRS::TOOSHORT", "Message packet is missing addressee or body separator.")]
    [InlineData("BADMSG>APRS::N0CALL   Missing second colon", "Message packet is missing second ':' body separator.")]
    public void TryParse_ReturnsValidationErrors_ForMalformedMessage(string rawLine, string expectedError)
    {
        var parser = new AprsParser();

        var exception = Record.Exception(() =>
        {
            var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

            var messagePacket = Assert.IsType<MessageAprsPacket>(packet);
            Assert.False(isValid);
            Assert.False(messagePacket.IsValid);
            Assert.Contains(expectedError, messagePacket.ValidationErrors);
            Assert.Equal(expectedError, error);
        });

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("QUERY1>APRS:?APRSD")]
    public void TryParse_ReturnsQueryPacket_ForDirectQuery(string rawLine)
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

        var queryPacket = Assert.IsType<QueryAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("?APRSD", queryPacket.QueryText);
    }

    [Fact]
    public void TryParse_MarksAddressedMessageBodyAsQuery()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "QUERY2>APRS::APRS     :?APRSD",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var messagePacket = Assert.IsType<MessageAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("APRS", messagePacket.Addressee);
        Assert.True(messagePacket.IsQuery);
        Assert.Equal("?APRSD", messagePacket.QueryText);
    }

    [Theory]
    [InlineData(
        "OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1",
        "CHECKPNT1",
        true,
        false,
        "111111z",
        39.058333,
        -84.508333,
        '/',
        '-',
        "Checkpoint 1")]
    [InlineData(
        "OBJ2>APRS:;NETCTRL  *092345z3903.50N/08430.50W>Net control object",
        "NETCTRL",
        true,
        false,
        "092345z",
        39.058333,
        -84.508333,
        '/',
        '>',
        "Net control object")]
    [InlineData(
        "OBJ3>APRS:;HAZARD   _111111z3903.50N/08430.50W-Hazard cleared",
        "HAZARD",
        false,
        true,
        "111111z",
        39.058333,
        -84.508333,
        '/',
        '-',
        "Hazard cleared")]
    public void TryParse_ReturnsObjectPacket_ForAprsObject(
        string rawLine,
        string expectedObjectName,
        bool expectedIsAlive,
        bool expectedIsKilled,
        string expectedTimestamp,
        double expectedLatitude,
        double expectedLongitude,
        char expectedSymbolTable,
        char expectedSymbolCode,
        string expectedComment)
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

        var objectPacket = Assert.IsType<ObjectAprsPacket>(packet);
        Assert.True(isValid);
        Assert.True(objectPacket.IsValid);
        Assert.Null(error);
        Assert.Equal(expectedObjectName, objectPacket.ObjectName);
        Assert.Equal(expectedIsAlive, objectPacket.IsAlive);
        Assert.Equal(expectedIsKilled, objectPacket.IsKilled);
        Assert.Equal(expectedTimestamp, objectPacket.Timestamp);
        Assert.Equal(expectedLatitude, objectPacket.Latitude!.Value, 6);
        Assert.Equal(expectedLongitude, objectPacket.Longitude!.Value, 6);
        Assert.Equal(expectedSymbolTable, objectPacket.SymbolTableIdentifier);
        Assert.Equal(expectedSymbolCode, objectPacket.SymbolCode);
        Assert.Equal(expectedComment, objectPacket.Comment);
        Assert.NotEmpty(objectPacket.RawObjectBody);
    }

    [Theory]
    [InlineData(
        "ITEM1>APRS:)REPEATER!3903.50N/08430.50WrLocal repeater",
        "REPEATER",
        39.058333,
        -84.508333,
        '/',
        'r',
        "Local repeater")]
    [InlineData(
        "ITEM2>APRS:)SHELTER !3903.50N/08430.50W-Shelter location",
        "SHELTER",
        39.058333,
        -84.508333,
        '/',
        '-',
        "Shelter location")]
    public void TryParse_ReturnsItemPacket_ForAprsItem(
        string rawLine,
        string expectedItemName,
        double expectedLatitude,
        double expectedLongitude,
        char expectedSymbolTable,
        char expectedSymbolCode,
        string expectedComment)
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

        var itemPacket = Assert.IsType<ItemAprsPacket>(packet);
        Assert.True(isValid);
        Assert.True(itemPacket.IsValid);
        Assert.Null(error);
        Assert.Equal(expectedItemName, itemPacket.ItemName);
        Assert.Equal(expectedLatitude, itemPacket.Latitude!.Value, 6);
        Assert.Equal(expectedLongitude, itemPacket.Longitude!.Value, 6);
        Assert.Equal(expectedSymbolTable, itemPacket.SymbolTableIdentifier);
        Assert.Equal(expectedSymbolCode, itemPacket.SymbolCode);
        Assert.Equal(expectedComment, itemPacket.Comment);
        Assert.NotEmpty(itemPacket.RawItemBody);
    }

    [Fact]
    public void TryParse_ReturnsValidationErrors_ForMalformedObject()
    {
        var parser = new AprsParser();

        var exception = Record.Exception(() =>
        {
            var isValid = parser.TryParse("BADOBJ>APRS:;SHORT", ReceivedAtUtc, out var packet, out var error);

            var objectPacket = Assert.IsType<ObjectAprsPacket>(packet);
            Assert.False(isValid);
            Assert.False(objectPacket.IsValid);
            Assert.Equal("SHORT", objectPacket.ObjectName);
            Assert.Contains("Object name is missing or incomplete.", objectPacket.ValidationErrors);
            Assert.Contains("Object live/killed indicator is missing.", objectPacket.ValidationErrors);
            Assert.Contains("Object timestamp is missing or incomplete.", objectPacket.ValidationErrors);
            Assert.Contains("Object position latitude is missing or incomplete.", objectPacket.ValidationErrors);
            Assert.Equal("Object name is missing or incomplete.", error);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void TryParse_ReturnsValidationErrors_ForMalformedItem()
    {
        var parser = new AprsParser();

        var exception = Record.Exception(() =>
        {
            var isValid = parser.TryParse("BADITEM>APRS:)BADITEM", ReceivedAtUtc, out var packet, out var error);

            var itemPacket = Assert.IsType<ItemAprsPacket>(packet);
            Assert.False(isValid);
            Assert.False(itemPacket.IsValid);
            Assert.Equal("BADITEM", itemPacket.ItemName);
            Assert.Contains("Item position separator is missing.", itemPacket.ValidationErrors);
            Assert.Contains("Item position latitude is missing or incomplete.", itemPacket.ValidationErrors);
            Assert.Equal("Item position separator is missing.", error);
        });

        Assert.Null(exception);
    }

    [Fact]
    public void TryParse_ReturnsWeatherPacket_ForPositionWeather()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var weatherPacket = Assert.IsType<WeatherAprsPacket>(packet);
        Assert.True(isValid);
        Assert.True(weatherPacket.IsValid);
        Assert.Null(error);
        Assert.Equal(39.058333, weatherPacket.Latitude!.Value, 6);
        Assert.Equal(-84.508333, weatherPacket.Longitude!.Value, 6);
        Assert.Equal('/', weatherPacket.SymbolTableIdentifier);
        Assert.Equal('_', weatherPacket.SymbolCode);
        Assert.Null(weatherPacket.Timestamp);
        Assert.Equal("180/005g010t072r000p000P000h50b10132", weatherPacket.RawWeatherBody);
        Assert.Equal(180, weatherPacket.WindDirectionDegrees);
        Assert.Equal(5, weatherPacket.WindSpeedMph);
        Assert.Equal(10, weatherPacket.WindGustMph);
        Assert.Equal(72, weatherPacket.TemperatureFahrenheit);
        Assert.Equal(0, weatherPacket.RainLastHourHundredthsInch);
        Assert.Equal(0, weatherPacket.RainLast24HoursHundredthsInch);
        Assert.Equal(0, weatherPacket.RainSinceMidnightHundredthsInch);
        Assert.Equal(50, weatherPacket.HumidityPercent);
        Assert.Equal(1013.2, weatherPacket.BarometricPressureMillibars);
        Assert.Equal(string.Empty, weatherPacket.Comment);
    }

    [Fact]
    public void TryParse_ReturnsWeatherPacket_ForWeatherWithoutPosition()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "WX9XYZ>APRS:_111111c180s005g010t072r000p000P000h50b10132",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var weatherPacket = Assert.IsType<WeatherAprsPacket>(packet);
        Assert.True(isValid);
        Assert.True(weatherPacket.IsValid);
        Assert.Null(error);
        Assert.Null(weatherPacket.Latitude);
        Assert.Null(weatherPacket.Longitude);
        Assert.Equal("111111", weatherPacket.Timestamp);
        Assert.Equal("c180s005g010t072r000p000P000h50b10132", weatherPacket.RawWeatherBody);
        Assert.Equal(180, weatherPacket.WindDirectionDegrees);
        Assert.Equal(5, weatherPacket.WindSpeedMph);
        Assert.Equal(10, weatherPacket.WindGustMph);
        Assert.Equal(72, weatherPacket.TemperatureFahrenheit);
        Assert.Equal(50, weatherPacket.HumidityPercent);
        Assert.Equal(1013.2, weatherPacket.BarometricPressureMillibars);
    }

    [Fact]
    public void TryParse_PreservesWeatherCommentText()
    {
        var parser = new AprsParser();

        var isValid = parser.TryParse(
            "WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132Test weather station",
            ReceivedAtUtc,
            out var packet,
            out var error);

        var weatherPacket = Assert.IsType<WeatherAprsPacket>(packet);
        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal("Test weather station", weatherPacket.Comment);
        Assert.Equal("180/005g010t072r000p000P000h50b10132Test weather station", weatherPacket.RawWeatherBody);
    }

    [Theory]
    [InlineData("BADWX>APRS:_badweather")]
    [InlineData("BADWX2>APRS:!3903.50N/08430.50W_badweather")]
    public void TryParse_ReturnsValidationErrors_ForMalformedWeather(string rawLine)
    {
        var parser = new AprsParser();

        var exception = Record.Exception(() =>
        {
            var isValid = parser.TryParse(rawLine, ReceivedAtUtc, out var packet, out var error);

            var weatherPacket = Assert.IsType<WeatherAprsPacket>(packet);
            Assert.False(isValid);
            Assert.False(weatherPacket.IsValid);
            Assert.Equal("badweather", weatherPacket.RawWeatherBody);
            Assert.Contains("Weather barometric pressure is invalid.", weatherPacket.ValidationErrors);
            Assert.Contains("Weather packet contains no recognized weather fields.", weatherPacket.ValidationErrors);
            Assert.Equal("Weather barometric pressure is invalid.", error);
        });

        Assert.Null(exception);
    }
}
