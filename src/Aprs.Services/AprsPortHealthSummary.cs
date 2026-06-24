namespace Aprs.Services;

public sealed record AprsPortHealthSummary(
    int TotalPorts,
    int EnabledPorts,
    int ConnectedPorts,
    int FaultedPorts,
    int ReceiveEnabledPorts,
    int TransmitEnabledPorts,
    int TotalPacketsReceived,
    int TotalPacketsTransmitted,
    bool HasErrors,
    IReadOnlyList<string> Errors);
