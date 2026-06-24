namespace AprsCommand.Api;

public sealed record LocalRestApiConfiguration
{
    public bool ApiEnabled { get; init; }
    public string BindAddress { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 8765;
    public bool LocalhostOnly { get; init; } = true;
    public bool RequireToken { get; init; } = true;
    public string? ApiTokenReference { get; init; }
    public bool ReadOnlyMode { get; init; } = true;
    public bool AllowExternalDataSubmit { get; init; }
    public bool AllowTransmitRequest { get; init; }
    public int MaximumRequestsPerMinute { get; init; } = 60;
    public DateTimeOffset CreatedTimestamp { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedTimestamp { get; init; } = DateTimeOffset.UtcNow;

    public static LocalRestApiConfiguration Default { get; } = new();
}
