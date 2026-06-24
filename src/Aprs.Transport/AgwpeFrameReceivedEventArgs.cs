namespace Aprs.Transport;

public sealed class AgwpeFrameReceivedEventArgs : EventArgs
{
    public AgwpeFrameReceivedEventArgs(AgwpeFrame frame)
    {
        Frame = frame;
    }

    public AgwpeFrame Frame { get; }
}
