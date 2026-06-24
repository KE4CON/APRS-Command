namespace Aprs.Desktop;

public static class StartupDiagnostics
{
    public static string WriteFatalStartupError(Exception exception, string? logDirectoryOverride = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var logDirectory = string.IsNullOrWhiteSpace(logDirectoryOverride)
            ? ResolveStartupLogDirectory()
            : logDirectoryOverride;
        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch
        {
            logDirectory = Path.GetTempPath();
        }

        var logPath = Path.Combine(logDirectory, "startup-error.log");
        var message = $"""
                       APRS Command startup failure
                       TimestampUtc: {DateTimeOffset.UtcNow:O}
                       AppBaseDirectory: {AppContext.BaseDirectory}
                       CurrentDirectory: {Environment.CurrentDirectory}
                       Exception:
                       {exception}

                       """;

        try
        {
            File.AppendAllText(logPath, message);
            return logPath;
        }
        catch
        {
            var fallbackPath = Path.Combine(Path.GetTempPath(), "aprs-command-startup-error.log");
            File.AppendAllText(fallbackPath, message);
            return fallbackPath;
        }
    }

    public static string ResolveStartupLogDirectory()
    {
        var baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (string.IsNullOrWhiteSpace(baseFolder))
        {
            baseFolder = Path.GetTempPath();
        }

        return Path.Combine(baseFolder, "APRS Command", "logs");
    }
}
