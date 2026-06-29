namespace Aprs.Desktop.Configuration;

/// <summary>
/// Settings for the Winlink RMS gateway feature.
///
/// IMPORTANT: The Winlink API (api.winlink.org) requires an access key
/// obtained from a Winlink administrator — it is not a free, self-service
/// API. The key is tied to a specific application and software author.
/// See https://api.winlink.org for details on requesting a key.
///
/// Until an ApiKey is configured, the RMS Gateway feature is disabled
/// and shows guidance for obtaining one.
/// </summary>
public sealed record WinlinkSettings(
    string? ApiKey,
    bool    Enabled)
{
    public static WinlinkSettings Default { get; } = new(
        ApiKey:  null,
        Enabled: false);
}
