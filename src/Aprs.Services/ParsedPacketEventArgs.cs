namespace Aprs.Services;

/// <summary>Event args for <see cref="Runtime.AprsIngestionService.PacketParsed"/>.</summary>
public sealed class ParsedPacketEventArgs : EventArgs
{
    public ParsedPacketEventArgs(Aprs.Core.AprsPacket? packet, AprsPacketSource source)
    {
        Packet = packet;
        Source = source;
    }

    /// <summary>The parsed packet, or null if the line could not be parsed.</summary>
    public Aprs.Core.AprsPacket? Packet { get; }

    /// <summary>The source the packet arrived on.</summary>
    public AprsPacketSource Source { get; }
}
