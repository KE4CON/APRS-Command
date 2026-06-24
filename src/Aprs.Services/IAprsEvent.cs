namespace Aprs.Services;

public interface IAprsEvent
{
    AprsEventMetadata Metadata { get; }
}
