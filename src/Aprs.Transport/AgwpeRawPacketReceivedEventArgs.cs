namespace Aprs.Transport;

public sealed class AgwpeRawPacketReceivedEventArgs : EventArgs
{
    public AgwpeRawPacketReceivedEventArgs(string rawPacketLine, DateTimeOffset receivedAtUtc, AgwpeFrame sourceFrame)
    {
        RawPacketLine = rawPacketLine;
        ReceivedAtUtc = receivedAtUtc;
        SourceFrame = sourceFrame;
    }

    public string RawPacketLine { get; }

    public DateTimeOffset ReceivedAtUtc { get; }

    public AgwpeFrame SourceFrame { get; }
}
