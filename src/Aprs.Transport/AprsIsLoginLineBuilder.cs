namespace Aprs.Transport;

public static class AprsIsLoginLineBuilder
{
    public static string Build(AprsIsClientConfiguration configuration)
    {
        Validate(configuration);

        var loginLine = $"user {configuration.Callsign.Trim()} pass {configuration.Passcode.Trim()} vers {configuration.ApplicationName.Trim()} {configuration.ApplicationVersion.Trim()}";
        if (!string.IsNullOrWhiteSpace(configuration.Filter))
        {
            loginLine += $" filter {configuration.Filter.Trim()}";
        }

        return loginLine;
    }

    public static void Validate(AprsIsClientConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Callsign))
        {
            throw new ArgumentException("APRS-IS callsign is required.", nameof(configuration));
        }

        if (configuration.ServerPort is <= 0 or > 65535)
        {
            throw new ArgumentException("APRS-IS server port must be between 1 and 65535.", nameof(configuration));
        }

        if (string.IsNullOrWhiteSpace(configuration.Passcode))
        {
            throw new ArgumentException("APRS-IS passcode is required. Use -1 for receive-only connections.", nameof(configuration));
        }

        if (string.IsNullOrWhiteSpace(configuration.ApplicationName))
        {
            throw new ArgumentException("APRS-IS application name is required.", nameof(configuration));
        }

        if (string.IsNullOrWhiteSpace(configuration.ApplicationVersion))
        {
            throw new ArgumentException("APRS-IS application version is required.", nameof(configuration));
        }
    }
}
