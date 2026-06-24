namespace Aprs.Services;

public sealed record SimulationStatus(
    SimulationState State,
    bool SimulationEnabled,
    bool TransmitDisabled,
    string SimulationSourceName,
    int GeneratedPacketCount,
    DateTimeOffset? LastGeneratedAtUtc,
    string? LastError);
