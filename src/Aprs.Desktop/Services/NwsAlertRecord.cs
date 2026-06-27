namespace Aprs.Desktop.Services;

/// <summary>Severity level of an NWS alert, ordered from least to most severe.</summary>
public enum AlertSeverity
{
    Unknown,
    Minor,
    Moderate,
    Severe,
    Extreme
}

/// <summary>A single active NWS weather alert.</summary>
public sealed record NwsAlertRecord(
    string Id,
    string Event,
    string Headline,
    string Description,
    string Severity,
    string Urgency,
    string Certainty,
    string AreaDescription,
    DateTimeOffset Effective,
    DateTimeOffset? Expires,
    string Sender)
{
    public AlertSeverity SeverityLevel => Severity switch
    {
        "Extreme"  => AlertSeverity.Extreme,
        "Severe"   => AlertSeverity.Severe,
        "Moderate" => AlertSeverity.Moderate,
        "Minor"    => AlertSeverity.Minor,
        _          => AlertSeverity.Unknown
    };

    public string SeverityColor => SeverityLevel switch
    {
        AlertSeverity.Extreme  => "#7C0000",
        AlertSeverity.Severe   => "#CC0000",
        AlertSeverity.Moderate => "#FF6600",
        AlertSeverity.Minor    => "#FFCC00",
        _                      => "#888888"
    };

    public string SeverityBg => SeverityLevel switch
    {
        AlertSeverity.Extreme  => "#FFE0E0",
        AlertSeverity.Severe   => "#FFF0F0",
        AlertSeverity.Moderate => "#FFF5E0",
        AlertSeverity.Minor    => "#FFFDE0",
        _                      => "#F8F8F8"
    };

    public bool IsExpired => Expires.HasValue && DateTimeOffset.UtcNow > Expires.Value;

    public string ExpiresText => Expires.HasValue
        ? $"Until {Expires.Value.ToLocalTime():ddd MMM d h:mm tt}"
        : "No expiration";
}
