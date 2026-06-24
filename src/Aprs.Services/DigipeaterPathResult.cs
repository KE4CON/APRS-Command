namespace Aprs.Services;

public sealed record DigipeaterPathResult(
    bool Matched,
    IReadOnlyList<string> ModifiedPath,
    string? ModifiedPacket,
    string Reason,
    IReadOnlyList<string> ValidationWarnings,
    IReadOnlyList<string> ValidationErrors);
