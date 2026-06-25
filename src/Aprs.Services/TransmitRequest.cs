namespace Aprs.Services;

/// <summary>
/// A request to transmit on a specific port to a specific destination, evaluated by
/// <see cref="ITransmitSafetyAuthority"/> before anything is keyed up.
/// </summary>
public sealed record TransmitRequest(string PortId, TransmitDestination Destination);
