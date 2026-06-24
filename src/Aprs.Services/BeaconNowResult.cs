using Aprs.Transport;

namespace Aprs.Services;

public sealed record BeaconNowResult(
    bool PacketGenerated,
    bool TransmitAttempted,
    bool Transmitted,
    bool Blocked,
    string? Packet,
    string? Message,
    AprsIsTransmitResult? TransmitResult,
    IReadOnlyList<string> ValidationErrors);
