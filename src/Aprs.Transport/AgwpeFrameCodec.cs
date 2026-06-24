using System.Buffers.Binary;
using System.Text;

namespace Aprs.Transport;

public sealed class AgwpeFrameCodec
{
    public const int HeaderLength = 36;

    private readonly IAx25AprsPayloadDecoder payloadDecoder;

    public AgwpeFrameCodec()
        : this(Ax25AprsPayloadDecoder.Default)
    {
    }

    public AgwpeFrameCodec(IAx25AprsPayloadDecoder payloadDecoder)
    {
        this.payloadDecoder = payloadDecoder;
    }

    public AgwpeFrame Decode(IReadOnlyList<byte> frameBytes, DateTimeOffset timestampUtc, string packetSource)
    {
        var errors = new List<string>();
        if (frameBytes.Count < HeaderLength)
        {
            return new AgwpeFrame(
                frameBytes.ToArray(),
                CommandType: '\0',
                RadioPort: -1,
                SourceCallsign: null,
                DestinationCallsign: null,
                Path: [],
                Payload: [],
                DecodedAprsPacketText: null,
                TimestampUtc: timestampUtc,
                PacketSource: packetSource,
                ValidationErrors: ["AGWPE frame is shorter than the 36-byte header."]);
        }

        var raw = frameBytes.ToArray();
        var radioPort = raw[0];
        var commandType = (char)raw[4];
        var source = DecodeCallsign(raw, 8, 10);
        var destination = DecodeCallsign(raw, 18, 10);
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(28, 4));
        if (payloadLength < 0)
        {
            errors.Add("AGWPE frame payload length is invalid.");
            payloadLength = 0;
        }

        var requiredLength = HeaderLength + payloadLength;
        if (raw.Length < requiredLength)
        {
            errors.Add("AGWPE frame is incomplete.");
            payloadLength = Math.Max(0, raw.Length - HeaderLength);
        }

        var payload = raw.Skip(HeaderLength).Take(payloadLength).ToArray();
        string? decodedAprs = null;
        if (IsReceivedDataCommand(commandType) && payload.Length > 0)
        {
            var decoded = payloadDecoder.Decode(payload);
            decodedAprs = decoded.AprsPacketText;
            errors.AddRange(decoded.ValidationErrors);
        }

        if (payload.Length == 0 && IsReceivedDataCommand(commandType))
        {
            errors.Add("AGWPE data frame payload is empty.");
        }

        var path = ExtractPath(decodedAprs);
        return new AgwpeFrame(
            raw,
            commandType,
            radioPort,
            source,
            destination,
            path,
            payload,
            decodedAprs,
            timestampUtc,
            packetSource,
            errors);
    }

    public IReadOnlyList<AgwpeFrame> DecodeMany(IReadOnlyList<byte> bytes, DateTimeOffset timestampUtc, string packetSource)
    {
        var frames = new List<AgwpeFrame>();
        var offset = 0;
        while (offset + HeaderLength <= bytes.Count)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.Skip(offset + 28).Take(4).ToArray());
            if (length < 0)
            {
                frames.Add(Decode(bytes.Skip(offset).Take(HeaderLength).ToArray(), timestampUtc, packetSource));
                break;
            }

            var frameLength = HeaderLength + length;
            if (offset + frameLength > bytes.Count)
            {
                break;
            }

            frames.Add(Decode(bytes.Skip(offset).Take(frameLength).ToArray(), timestampUtc, packetSource));
            offset += frameLength;
        }

        return frames;
    }

    public byte[] Encode(char commandType, int radioPort, string sourceCallsign, string destinationCallsign, IReadOnlyList<byte> payload)
    {
        if (radioPort is < 0 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(radioPort), "AGWPE radio port must be between 0 and 255.");
        }

        var frame = new byte[HeaderLength + payload.Count];
        frame[0] = (byte)radioPort;
        frame[4] = (byte)commandType;
        EncodeCallsign(frame, 8, sourceCallsign);
        EncodeCallsign(frame, 18, destinationCallsign);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(28, 4), payload.Count);
        payload.ToArray().CopyTo(frame, HeaderLength);
        return frame;
    }

    public int FindLastCompleteFrameEnd(IReadOnlyList<byte> bytes)
    {
        var offset = 0;
        var lastEnd = -1;
        while (offset + HeaderLength <= bytes.Count)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.Skip(offset + 28).Take(4).ToArray());
            if (length < 0)
            {
                return offset + HeaderLength - 1;
            }

            var frameLength = HeaderLength + length;
            if (offset + frameLength > bytes.Count)
            {
                break;
            }

            lastEnd = offset + frameLength - 1;
            offset += frameLength;
        }

        return lastEnd;
    }

    private static bool IsReceivedDataCommand(char commandType)
    {
        return commandType is 'K' or 'U' or 'D';
    }

    private static string? DecodeCallsign(byte[] raw, int offset, int length)
    {
        var text = Encoding.ASCII.GetString(raw, offset, length).TrimEnd('\0', ' ');
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static void EncodeCallsign(byte[] frame, int offset, string callsign)
    {
        var bytes = Encoding.ASCII.GetBytes((callsign ?? string.Empty).Trim().ToUpperInvariant());
        Array.Fill<byte>(frame, 0, offset, 10);
        bytes.Take(10).ToArray().CopyTo(frame, offset);
    }

    private static IReadOnlyList<string> ExtractPath(string? packet)
    {
        if (string.IsNullOrWhiteSpace(packet))
        {
            return [];
        }

        var headerEnd = packet.IndexOf(':');
        var sourceEnd = packet.IndexOf('>');
        if (headerEnd < 0 || sourceEnd < 0 || sourceEnd > headerEnd)
        {
            return [];
        }

        var header = packet[(sourceEnd + 1)..headerEnd];
        var components = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return components.Length <= 1 ? [] : components.Skip(1).ToArray();
    }
}
