namespace Aprs.Transport;

public static class KissFrameCodec
{
    public const byte Fend = 0xC0;
    public const byte Fesc = 0xDB;
    public const byte Tfend = 0xDC;
    public const byte Tfesc = 0xDD;

    public static byte[] Encode(int portNumber, KissCommandType commandType, IReadOnlyList<byte> payload)
    {
        var commandByte = (byte)(((portNumber & 0x0F) << 4) | ((int)commandType & 0x0F));
        var encoded = new List<byte> { Fend, commandByte };

        foreach (var value in payload)
        {
            switch (value)
            {
                case Fend:
                    encoded.Add(Fesc);
                    encoded.Add(Tfend);
                    break;
                case Fesc:
                    encoded.Add(Fesc);
                    encoded.Add(Tfesc);
                    break;
                default:
                    encoded.Add(value);
                    break;
            }
        }

        encoded.Add(Fend);
        return encoded.ToArray();
    }

    public static KissFrame Decode(
        IReadOnlyList<byte> rawFrameBytes,
        DateTimeOffset timestampUtc,
        string sourceName,
        IAx25AprsPayloadDecoder? payloadDecoder = null)
    {
        var errors = new List<string>();
        if (rawFrameBytes.Count < 3)
        {
            errors.Add("KISS frame is too short.");
        }

        if (rawFrameBytes.Count == 0 || rawFrameBytes[0] != Fend)
        {
            errors.Add("KISS frame is missing starting FEND byte.");
        }

        if (rawFrameBytes.Count == 0 || rawFrameBytes[^1] != Fend)
        {
            errors.Add("KISS frame is missing ending FEND byte.");
        }

        if (rawFrameBytes.Count < 2)
        {
            return CreateFrame(rawFrameBytes, 0, KissCommandType.Unknown, [], null, timestampUtc, sourceName, errors);
        }

        var commandByte = rawFrameBytes[1];
        var port = (commandByte >> 4) & 0x0F;
        var command = ToCommandType(commandByte & 0x0F);
        var payload = DecodePayload(rawFrameBytes.Skip(2).Take(Math.Max(0, rawFrameBytes.Count - 3)).ToArray(), errors);
        string? decodedText = null;

        if (command == KissCommandType.DataFrame)
        {
            var decodeResult = (payloadDecoder ?? Ax25AprsPayloadDecoder.Default).Decode(payload);
            decodedText = decodeResult.AprsPacketText;
            errors.AddRange(decodeResult.ValidationErrors);
        }

        return CreateFrame(rawFrameBytes, port, command, payload, decodedText, timestampUtc, sourceName, errors);
    }

    public static IReadOnlyList<KissFrame> DecodeMany(
        IReadOnlyList<byte> buffer,
        DateTimeOffset timestampUtc,
        string sourceName,
        IAx25AprsPayloadDecoder? payloadDecoder = null)
    {
        var frames = new List<KissFrame>();
        var start = -1;
        for (var index = 0; index < buffer.Count; index++)
        {
            if (buffer[index] != Fend)
            {
                continue;
            }

            if (start < 0)
            {
                start = index;
                continue;
            }

            if (index == start)
            {
                continue;
            }

            var raw = buffer.Skip(start).Take(index - start + 1).ToArray();
            frames.Add(Decode(raw, timestampUtc, sourceName, payloadDecoder));
            start = index;
        }

        return frames;
    }

    public static int FindLastCompleteFrameEnd(IReadOnlyList<byte> buffer)
    {
        var start = -1;
        var lastEnd = -1;
        for (var index = 0; index < buffer.Count; index++)
        {
            if (buffer[index] != Fend)
            {
                continue;
            }

            if (start < 0)
            {
                start = index;
            }
            else if (index > start)
            {
                lastEnd = index;
                start = index;
            }
        }

        return lastEnd;
    }

    private static IReadOnlyList<byte> DecodePayload(IReadOnlyList<byte> encodedPayload, List<string> errors)
    {
        var decoded = new List<byte>();
        for (var index = 0; index < encodedPayload.Count; index++)
        {
            var value = encodedPayload[index];
            if (value != Fesc)
            {
                decoded.Add(value);
                continue;
            }

            if (index + 1 >= encodedPayload.Count)
            {
                errors.Add("KISS escape byte is missing escaped value.");
                break;
            }

            var escaped = encodedPayload[++index];
            switch (escaped)
            {
                case Tfend:
                    decoded.Add(Fend);
                    break;
                case Tfesc:
                    decoded.Add(Fesc);
                    break;
                default:
                    errors.Add("KISS frame contains an invalid escape sequence.");
                    decoded.Add(escaped);
                    break;
            }
        }

        return decoded;
    }

    private static KissCommandType ToCommandType(int value)
    {
        return Enum.IsDefined(typeof(KissCommandType), value)
            ? (KissCommandType)value
            : KissCommandType.Unknown;
    }

    private static KissFrame CreateFrame(
        IReadOnlyList<byte> rawFrameBytes,
        int port,
        KissCommandType command,
        IReadOnlyList<byte> payload,
        string? decodedText,
        DateTimeOffset timestampUtc,
        string sourceName,
        IReadOnlyList<string> errors)
    {
        return new KissFrame(rawFrameBytes.ToArray(), port, command, payload.ToArray(), decodedText, timestampUtc, sourceName, errors.ToArray());
    }
}
