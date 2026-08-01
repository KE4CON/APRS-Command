namespace Aprs.Core;

/// <summary>
/// Decomposes an APRS status report body (the text after the <c>&gt;</c> data-type indicator) into its
/// optional structured parts per APRS Protocol Reference §16, matching Dire Wolf's decoder:
/// <list type="bullet">
///   <item>a leading DHM-zulu <b>timestamp</b> (<c>DDHHMMz</c>), <b>or</b></item>
///   <item>a leading Maidenhead <b>grid locator</b> (4 or 6 chars) + a symbol (table id + code);</item>
///   <item>a trailing <b>beam heading + ERP</b> extension (<c>^</c> followed by two coded chars).</item>
/// </list>
/// Whatever remains after removing those is the human-readable <see cref="Message"/>. A plain status with
/// none of these round-trips to <c>Message == rawStatusText</c> and all-null extras.
/// </summary>
public readonly record struct AprsStatusReport(
    string Message,
    string? Timestamp,
    string? MaidenheadLocator,
    char? SymbolTableIdentifier,
    char? SymbolCode,
    int? BeamHeadingDegrees,
    int? EffectiveRadiatedPowerWatts)
{
    public static AprsStatusReport Parse(string rawStatusText)
    {
        var work = rawStatusText;
        string? timestamp = null;
        string? grid = null;
        char? symbolTable = null;
        char? symbolCode = null;
        int? beam = null;
        int? erp = null;

        // Trailing beam-heading + ERP: a '^' third-from-last, then two coded characters. Only treated as
        // the extension (and stripped from the message) when both characters decode; otherwise the '^' is
        // just literal text.
        if (work.Length >= 3 && work[^3] == '^')
        {
            var (b, e) = DecodeBeamErp(work[^2], work[^1]);
            if (b is not null && e is not null)
            {
                beam = b;
                erp = e;
                work = work[..^3];
            }
        }

        // Leading DHM-zulu timestamp OR a Maidenhead locator+symbol — the spec allows one or the other at
        // the start of a status, never both.
        if (TryReadZuluTimestamp(work, out var ts))
        {
            timestamp = ts;
            work = work[7..];
        }
        else if (TryReadMaidenhead(work, out grid, out symbolTable, out symbolCode, out var rest))
        {
            work = rest;
        }

        return new AprsStatusReport(work, timestamp, grid, symbolTable, symbolCode, beam, erp);
    }

    // Beam heading: '0'-'9' → 0-90°, 'A'-'Z' → 100-350° (10° steps). ERP: '1'-'K' → (c-'0')²·10 watts.
    private static (int? Beam, int? Erp) DecodeBeamErp(char h, char p)
    {
        int? beam = h switch
        {
            >= '0' and <= '9' => (h - '0') * 10,
            >= 'A' and <= 'Z' => (h - 'A') * 10 + 100,
            _ => null
        };
        int? erp = p is >= '1' and <= 'K' ? (p - '0') * (p - '0') * 10 : null;
        return (beam, erp);
    }

    private static bool TryReadZuluTimestamp(string s, out string? timestamp)
    {
        timestamp = null;
        if (s.Length < 7 || s[6] != 'z') return false;
        for (var i = 0; i < 6; i++)
        {
            if (!char.IsAsciiDigit(s[i])) return false;
        }

        timestamp = s[..7];
        return true;
    }

    private static bool TryReadMaidenhead(
        string s, out string? grid, out char? table, out char? code, out string message)
    {
        grid = null;
        table = null;
        code = null;
        message = s;

        // Prefer the more specific 6-character grid, then the 4-character grid.
        foreach (var len in (ReadOnlySpan<int>)[6, 4])
        {
            if (!IsGrid(s, len) || s.Length < len + 2) continue;

            var t = s[len];
            var c = s[len + 1];
            if (!IsSymbolTable(t)) continue;

            // After the symbol there must be either end-of-string, or a space introducing a comment —
            // otherwise this isn't the locator form (it's ordinary status text that merely starts with
            // grid-like characters).
            var afterIndex = len + 2;
            if (s.Length == afterIndex)
            {
                grid = s[..len];
                table = t;
                code = c;
                message = string.Empty;
                return true;
            }

            if (s[afterIndex] == ' ')
            {
                grid = s[..len];
                table = t;
                code = c;
                message = s[(afterIndex + 1)..];
                return true;
            }
        }

        return false;
    }

    // Maidenhead: field A-R, square 0-9, subsquare A-X / a-x.
    private static bool IsGrid(string s, int len)
    {
        if (s.Length < len) return false;
        if (!IsField(s[0]) || !IsField(s[1])) return false;
        if (!char.IsAsciiDigit(s[2]) || !char.IsAsciiDigit(s[3])) return false;
        if (len == 6 && (!IsSubsquare(s[4]) || !IsSubsquare(s[5]))) return false;
        return true;
    }

    private static bool IsField(char c) => c is >= 'A' and <= 'R';

    private static bool IsSubsquare(char c) => c is (>= 'a' and <= 'x') or (>= 'A' and <= 'X');

    // Symbol table identifier: primary '/', alternate '\', or an overlay (A-Z / 0-9).
    private static bool IsSymbolTable(char c) => c is '/' or '\\' or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
}
