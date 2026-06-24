namespace Aprs.Services;

public abstract record AprsEventBase(AprsEventMetadata Metadata) : IAprsEvent;
