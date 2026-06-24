namespace Aprs.Transport;

public sealed class TcpKissRawPacketReceivedEventArgs : EventArgs
{
    public TcpKissRawPacketReceivedEventArgs(string rawPacketLine, DateTimeOffset receivedAtUtc, KissFrame sourceFrame)
    {
        RawPacketLine = rawPacketLine;
        ReceivedAtUtc = receivedAtUtc;
        SourceFrame = sourceFrame;
    }

    public string RawPacketLine { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

    public KissFrame SourceFrame { get; }
}
