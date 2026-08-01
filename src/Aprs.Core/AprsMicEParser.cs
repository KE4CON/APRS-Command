namespace Aprs.Core;

/// <summary>
/// Decodes MIC-E position packets. The data type indicator is 0x60 (current GPS) or 0x27 (old GPS),
/// plus the legacy 0x1C/0x1D forms. MIC-E is a compact format used by many Kenwood/Yaesu radios and
/// trackers: the latitude, N/S sign, longitude 100 degree offset, W/E sign, and the message code are
/// encoded in the AX.25 <b>destination</b> field, while the longitude, speed, course, and symbol live
/// in the information field. Output is a <see cref="PositionAprsPacket"/> so MIC-E stations appear on
/// the map exactly like any other position report.
///
/// <para>Implementation follows the APRS 1.2 reference and Dire Wolf's decoder (decode_aprs.c): the
/// destination character table (<see cref="DecodeDigit"/>), the message-bit mapping (all-zero maps to
/// the Emergency status), and the longitude/speed/course byte formulas.</para>
/// </summary>
public sealed class AprsMicEParser
{
    // MIC-E data type indicators (kept as code points to avoid non-printable source characters).
    private const char CurrentGps = (char)0x60;      // '`' current GPS data
    private const char OldGps = (char)0x27;          // apostrophe, old GPS data
    private const char LegacyCurrentGps = (char)0x1C;
    private const char LegacyOldGps = (char)0x1D;

    // Indexed by the 3 message bits (A,B,C). Both-zero (index 0) is Emergency; index 7 is Off Duty.
    private static readonly string[] StandardMessages =
        { "Emergency", "Priority", "Special", "Committed", "Returning", "In Service", "En Route", "Off Duty" };
    private static readonly string[] CustomMessages =
        { "Emergency", "Custom-6", "Custom-5", "Custom-4", "Custom-3", "Custom-2", "Custom-1", "Custom-0" };

    public bool CanParse(string information)
        => information.Length > 0
           && information[0] is CurrentGps or OldGps or LegacyCurrentGps or LegacyOldGps;

    public AprsPacket Parse(RawAprsPacket rawPacket)
    {
        var errors = rawPacket.ValidationErrors.ToList();
        var info = rawPacket.Information;
        var dest = rawPacket.Destination ?? string.Empty;

        // Destination carries the 6 latitude digits + N/S, offset, W/E flags; info carries the rest.
        if (dest.Length < 6)
        {
            errors.Add("MIC-E destination address is too short (needs 6 characters).");
            return Unknown(rawPacket, errors);
        }

        // info[0] DTI, [1..3] longitude, [4..6] speed/course, [7] symbol code, [8] symbol table.
        if (info.Length < 8)
        {
            errors.Add("MIC-E information field is too short.");
            return Unknown(rawPacket, errors);
        }

        int stdMsg = 0, custMsg = 0, ambiguity = 0;
        var digits = new int[6];
        var masks = new[] { 4, 2, 1, 0, 0, 0 }; // message bits A,B,C live in destination bytes 1-3
        for (var i = 0; i < 6; i++)
        {
            digits[i] = DecodeDigit(dest[i], masks[i], ref stdMsg, ref custMsg, ref ambiguity);
            if (digits[i] < 0)
            {
                errors.Add("MIC-E destination contains an invalid character.");
                return Unknown(rawPacket, errors);
            }
        }

        // Latitude: DD MM.HH from the six digits; N/S from byte 4, using the high (P-Z) vs low group.
        var latMinutes = (digits[2] * 10 + digits[3]) + (digits[4] * 10 + digits[5]) / 100.0;
        var latitude = (digits[0] * 10 + digits[1]) + latMinutes / 60.0;
        var north = dest[3] >= 'P';
        if (!north) latitude = -latitude;

        var longitudeOffset = dest[4] >= 'P'; // adds 100 degrees to the longitude degrees
        var west = dest[5] >= 'P';

        var lonDegrees = DecodeLongitudeDegrees(info[1], longitudeOffset);
        var lonMinutes = DecodeLongitudeMinutes(info[2]);
        var lonHundredths = DecodeLongitudeHundredths(info[3]);
        if (lonDegrees < 0 || lonMinutes < 0 || lonHundredths < 0)
        {
            errors.Add("MIC-E longitude is invalid.");
            return Unknown(rawPacket, errors);
        }
        var longitude = lonDegrees + (lonMinutes + lonHundredths / 100.0) / 60.0;
        if (west) longitude = -longitude;

        // Speed (knots) and course from the three info bytes, each offset by 28.
        var sp = info[4] - 28;
        var dc = info[5] - 28;
        var se = info[6] - 28;
        var speed = sp * 10 + dc / 10;
        if (speed >= 800) speed -= 800;
        var course = (dc % 10) * 100 + se;
        if (course >= 400) course -= 400;
        int? courseDegrees = course == 0 ? null : (course == 360 ? 0 : course);
        int? speedKnots = speed >= 0 ? speed : null;

        var symbolCode = info[7];
        var symbolTable = info.Length > 8 ? info[8] : '/';

        var comment = info.Length > 9 ? info[9..] : string.Empty;
        var altitudeFeet = ExtractMicEAltitude(ref comment);

        // Message status: both-zero maps to Emergency; else custom table if any custom bit set, else standard.
        string status;
        if (stdMsg == 0 && custMsg == 0) status = "Emergency";
        else if (custMsg != 0) status = CustomMessages[custMsg];
        else status = StandardMessages[stdMsg];

        // Surface the operational status (En Route, Emergency, etc.) - but not the idle "Off Duty"
        // default - by prefixing the comment, since PositionAprsPacket has no dedicated field for it.
        if (status != "Off Duty")
            comment = comment.Length > 0 ? $"[{status}] {comment}" : $"[{status}]";

        return new PositionAprsPacket(
            rawPacket.RawLine,
            rawPacket.SourceCallsign,
            rawPacket.SourceSsid,
            dest,
            rawPacket.Path,
            rawPacket.Information,
            rawPacket.ReceivedAtUtc,
            rawPacket.IsValid && errors.Count == 0,
            errors,
            rawPacket.QConstruct,
            PositionType: info[0],
            Timestamp: null,
            Latitude: latitude,
            Longitude: longitude,
            SymbolTableIdentifier: symbolTable,
            SymbolCode: symbolCode,
            Comment: comment,
            CourseDegrees: courseDegrees,
            SpeedKnots: speedKnots,
            AltitudeFeet: altitudeFeet,
            PositionAmbiguity: ambiguity);
    }

    /// <summary>
    /// Converts one destination character to a latitude digit and, for the first three bytes (via a
    /// non-zero <paramref name="mask"/>), accumulates the standard/custom message bits. K/L/Z encode a
    /// blanked (ambiguous) digit. Returns -1 for an invalid character.
    /// </summary>
    private static int DecodeDigit(char c, int mask, ref int stdMsg, ref int custMsg, ref int ambiguity)
    {
        if (c is >= '0' and <= '9') return c - '0';
        if (c is >= 'A' and <= 'J') { custMsg |= mask; return c - 'A'; }
        if (c is >= 'P' and <= 'Y') { stdMsg |= mask; return c - 'P'; }
        if (c == 'K') { custMsg |= mask; ambiguity++; return 0; }
        if (c == 'L') { ambiguity++; return 0; }
        if (c == 'Z') { stdMsg |= mask; ambiguity++; return 0; }
        return -1;
    }

    private static int DecodeLongitudeDegrees(char ch, bool offset)
    {
        int c = ch;
        if (offset && c is >= 118 and <= 127) return c - 118;         // 0-9 degrees
        if (!offset && c is >= 38 and <= 127) return (c - 38) + 10;   // 10-99 degrees
        if (offset && c is >= 108 and <= 117) return (c - 108) + 100; // 100-109 degrees
        if (offset && c is >= 38 and <= 107) return (c - 38) + 110;   // 110-179 degrees
        return -1;
    }

    private static int DecodeLongitudeMinutes(char ch)
    {
        int c = ch;
        if (c is >= 88 and <= 97) return c - 88;        // 0-9 minutes
        if (c is >= 38 and <= 87) return (c - 38) + 10; // 10-59 minutes
        return -1;
    }

    private static int DecodeLongitudeHundredths(char ch)
    {
        int c = ch;
        return c is >= 28 and <= 127 ? c - 28 : -1;     // 0-99
    }

    /// <summary>
    /// MIC-E altitude is an optional 3-character base-91 value followed by <c>}</c> at the start of the
    /// comment, in metres relative to -10000 m. Strips it from the comment and returns feet, or null.
    /// </summary>
    private static int? ExtractMicEAltitude(ref string comment)
    {
        if (comment.Length < 4 || comment[3] != '}') return null;

        int a0 = comment[0] - 33, a1 = comment[1] - 33, a2 = comment[2] - 33;
        if (a0 is < 0 or > 90 || a1 is < 0 or > 90 || a2 is < 0 or > 90) return null;

        var metres = (a0 * 91 * 91 + a1 * 91 + a2) - 10000;
        comment = comment[4..];
        return (int)Math.Round(metres * 3.280839895);
    }

    private static UnknownAprsPacket Unknown(RawAprsPacket rawPacket, List<string> errors)
        => new(
            rawPacket.RawLine,
            rawPacket.SourceCallsign,
            rawPacket.SourceSsid,
            rawPacket.Destination,
            rawPacket.Path,
            rawPacket.Information,
            rawPacket.ReceivedAtUtc,
            false,
            errors,
            rawPacket.QConstruct);
}
