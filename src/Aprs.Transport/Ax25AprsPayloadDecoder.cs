using System.Text;

namespace Aprs.Transport;

public sealed class Ax25AprsPayloadDecoder : IAx25AprsPayloadDecoder
{
    public static Ax25AprsPayloadDecoder Default { get; } = new();

    public Ax25AprsPayloadDecodeResult Decode(IReadOnlyList<byte> payload)
    {
        var errors = new List<string>();
        if (payload.Count == 0)
        {
            return new Ax25AprsPayloadDecodeResult(null, ["AX.25 payload is empty."]);
        }

        // Some test/fake TNC streams carry APRS text directly; accept that as a safe fallback.
        if (LooksLikeAprsText(payload))
        {
            return new Ax25AprsPayloadDecodeResult(Encoding.ASCII.GetString(payload.ToArray()), []);
        }

        if (payload.Count < 16)
        {
            return new Ax25AprsPayloadDecodeResult(null, ["AX.25 UI frame is too short."]);
        }

        var destination = DecodeAddress(payload, 0, errors);
        var source = DecodeAddress(payload, 7, errors);
        var addressIndex = 14;
        while (addressIndex - 7 >= 0 && (payload[addressIndex - 1] & 0x01) == 0)
        {
            addressIndex += 7;
            if (addressIndex > payload.Count)
            {
                errors.Add("AX.25 address field is incomplete.");
                return new Ax25AprsPayloadDecodeResult(null, errors);
            }
        }

        if (addressIndex + 2 > payload.Count)
        {
            errors.Add("AX.25 UI frame is missing control or PID bytes.");
            return new Ax25AprsPayloadDecodeResult(null, errors);
        }

        var control = payload[addressIndex];
        var pid = payload[addressIndex + 1];
        if (control != 0x03)
        {
            errors.Add("AX.25 frame is not a UI frame.");
        }

        if (pid != 0xF0)
        {
            errors.Add("AX.25 frame PID is not no-layer-3 APRS payload.");
        }

        var informationStart = addressIndex + 2;
        if (informationStart >= payload.Count)
        {
            errors.Add("AX.25 UI frame information field is empty.");
            return new Ax25AprsPayloadDecodeResult(null, errors);
        }

        var information = Encoding.ASCII.GetString(payload.Skip(informationStart).ToArray());
        var packet = $"{source}>{destination}:{information}";
        return new Ax25AprsPayloadDecodeResult(errors.Count == 0 ? packet : null, errors);
    }

    private static string DecodeAddress(IReadOnlyList<byte> payload, int offset, List<string> errors)
    {
        if (offset + 7 > payload.Count)
        {
            errors.Add("AX.25 address field is incomplete.");
            return string.Empty;
        }

        var callsign = new string(payload.Skip(offset).Take(6).Select(value => (char)(value >> 1)).ToArray()).Trim();
        var ssid = (payload[offset + 6] >> 1) & 0x0F;
        return ssid == 0 ? callsign : $"{callsign}-{ssid}";
    }

    private static bool LooksLikeAprsText(IReadOnlyList<byte> payload)
    {
        if (payload.Any(value => value < 0x20 || value > 0x7E))
        {
            return false;
        }

        var text = Encoding.ASCII.GetString(payload.ToArray());
        return text.Contains('>') && text.Contains(':');
    }
}
