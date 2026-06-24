namespace Aprs.Services;

public sealed record AprsMessageRetryConfiguration(
    TimeSpan RetryInterval,
    int MaximumRetries,
    string Destination,
    bool RequireTransmitConfirmation)
{
    public static AprsMessageRetryConfiguration Default { get; } = new(
        RetryInterval: TimeSpan.FromSeconds(30),
        MaximumRetries: 3,
        Destination: "APRS",
        RequireTransmitConfirmation: true);
}
