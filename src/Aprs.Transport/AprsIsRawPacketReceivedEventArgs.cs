namespace Aprs.Transport;

public sealed class AprsIsRawPacketReceivedEventArgs : EventArgs
{
    public AprsIsRawPacketReceivedEventArgs(string rawPacketLine, DateTimeOffset receivedAtUtc)
    {
        RawPacketLine = rawPacketLine;
        ReceivedAtUtc = receivedAtUtc;
    }

    public string RawPacketLine { get; }

    public DateTimeOffset ReceivedAtUtc { get; }
}
