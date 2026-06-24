namespace Aprs.Transport;

public sealed class KissFrameReceivedEventArgs : EventArgs
{
    public KissFrameReceivedEventArgs(KissFrame frame)
    {
        Frame = frame;
    }

    public KissFrame Frame { get; }
}
