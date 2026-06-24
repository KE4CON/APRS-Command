namespace Aprs.Services;

public sealed class FirstRunSetupService
{
    public FirstRunSetupConfiguration CreateDefault(DateTimeOffset now, string? applicationDataFolderPath = null)
    {
        return FirstRunSetupConfiguration.CreateDefault(now, applicationDataFolderPath);
    }

    public FirstRunSetupConfiguration MarkCompleted(FirstRunSetupConfiguration configuration, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration with
        {
            FirstRunCompleted = true,
            SafetySettingsReviewed = true,
            UpdatedTimestampUtc = now
        };
    }
}
