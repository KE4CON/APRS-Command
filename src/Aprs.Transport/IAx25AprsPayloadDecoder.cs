namespace Aprs.Transport;

public interface IAx25AprsPayloadDecoder
{
    Ax25AprsPayloadDecodeResult Decode(IReadOnlyList<byte> payload);
}
