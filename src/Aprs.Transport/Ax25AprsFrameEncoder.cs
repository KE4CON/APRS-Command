using System.Text;

namespace Aprs.Transport;

/// <summary>
/// Encodes TNC2-format APRS strings into AX.25 UI frame byte arrays suitable
/// for wrapping in a KISS frame and sending to a TNC.
///
/// TNC2 format:  SOURCE>DEST,PATH1,PATH2:information
/// AX.25 UI frame layout:
///   [dest 7 bytes][source 7 bytes][digi1 7 bytes]...[digipN 7 bytes][control 1][pid 1][info N]
///
/// Each address field is 7 bytes: 6 bytes of callsign (ASCII, left-justified,
/// space-padded, each byte left-shifted by 1) plus 1 byte for SSID/flags.
/// The last address field has bit 0 of the last byte set to 1 (end-of-address).
/// </summary>
public static class Ax25AprsFrameEncoder
{
    private const byte ControlUi    = 0x03;   // UI frame
    private const byte PidNoLayer3  = 0xF0;   // No layer 3 protocol (standard APRS)

    /// <summary>
    /// Encodes a TNC2-format APRS string into a raw AX.25 UI frame.
    /// Returns null if the packet cannot be parsed.
    /// </summary>
    public static byte[]? Encode(string? tnc2Packet)
    {
        if (string.IsNullOrWhiteSpace(tnc2Packet)) return null;

        var colonIdx = tnc2Packet.IndexOf(':');
        if (colonIdx < 0) return null;

        var header      = tnc2Packet[..colonIdx];
        var information = tnc2Packet[(colonIdx + 1)..];

        var arrowIdx = header.IndexOf('>');
        if (arrowIdx < 0) return null;

        var source      = header[..arrowIdx].Trim();
        var rest        = header[(arrowIdx + 1)..];
        var headerParts = rest.Split(',');
        if (headerParts.Length == 0) return null;

        var destination = headerParts[0].Trim();
        var digipeaters = headerParts[1..].Select(p => p.Trim().TrimEnd('*')).ToArray();

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(destination)) return null;

        var frame = new List<byte>();

        // Address fields: destination, source, then each digipeater
        for (int i = -1; i < digipeaters.Length; i++)
        {
            string addr   = i == -1 ? destination : i == 0 && digipeaters.Length == 0 ? source : digipeaters[i];
            bool   isLast = i == digipeaters.Length - 1;

            if (i == -1) // destination
            {
                frame.AddRange(EncodeAddress(destination, isLast: digipeaters.Length == 0 && false));
            }
            else if (i < digipeaters.Length)
            {
                // This slot is a digi
            }
        }

        // Simpler, clearer construction:
        frame.Clear();

        frame.AddRange(EncodeAddress(destination, isLast: false));
        frame.AddRange(EncodeAddress(source,      isLast: digipeaters.Length == 0));
        for (int i = 0; i < digipeaters.Length; i++)
            frame.AddRange(EncodeAddress(digipeaters[i], isLast: i == digipeaters.Length - 1));

        frame.Add(ControlUi);
        frame.Add(PidNoLayer3);
        frame.AddRange(Encoding.ASCII.GetBytes(information));

        return frame.ToArray();
    }

    private static byte[] EncodeAddress(string callsignWithSsid, bool isLast)
    {
        var clean   = callsignWithSsid.TrimEnd('*').Trim();
        var dashIdx = clean.LastIndexOf('-');
        string callsign;
        int    ssid = 0;

        if (dashIdx >= 0 && int.TryParse(clean[(dashIdx + 1)..], out int parsedSsid))
        {
            callsign = clean[..dashIdx].ToUpperInvariant();
            ssid     = Math.Clamp(parsedSsid, 0, 15);
        }
        else
        {
            callsign = clean.ToUpperInvariant();
        }

        callsign = callsign.Length > 6 ? callsign[..6] : callsign.PadRight(6);

        var result = new byte[7];
        for (int i = 0; i < 6; i++)
            result[i] = (byte)((callsign[i] & 0x7F) << 1);

        // Byte 6: 0x60 (R bits set) | SSID<<1 | end-of-address bit
        result[6] = (byte)(0x60 | ((ssid & 0x0F) << 1) | (isLast ? 0x01 : 0x00));
        return result;
    }
}
