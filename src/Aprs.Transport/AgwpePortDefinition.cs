namespace Aprs.Transport;

public sealed record AgwpePortDefinition(
    int PortNumber,
    string Name,
    bool ReceiveEnabled,
    bool TransmitEnabled);
