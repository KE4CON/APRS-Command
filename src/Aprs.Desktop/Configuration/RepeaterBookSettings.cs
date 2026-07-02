namespace Aprs.Desktop.Configuration;

/// <summary>
/// Settings for the RepeaterBook API integration.
/// An approved API token is required from:
/// https://www.repeaterbook.com/api/token_request.php
///
/// Until a token is configured the repeater directory panel shows
/// instructions for obtaining one.
/// </summary>
public sealed record RepeaterBookSettings(
    string? ApiToken,
    int     SearchRadiusMiles)
{
    public static RepeaterBookSettings Default { get; } = new(
        ApiToken:         null,
        SearchRadiusMiles: 25);
}
