namespace Aprs.Services;

public sealed record TrainingModeConfiguration
{
    public bool TrainingModeEnabled { get; init; }

    public string? SelectedScenarioName { get; init; }

    public Guid? SelectedScenarioId { get; init; }

    public bool UseSimulatedAprsSource { get; init; }

    public bool UseReplaySource { get; init; }

    public double ScenarioSpeedMultiplier { get; init; } = 1.0;

    public bool AutoStartSimulation { get; init; }

    public bool AutoStartReplay { get; init; }

    public bool ResetStationDatabaseOnScenarioStart { get; init; }

    public bool ResetObjectDatabaseOnScenarioStart { get; init; }

    public bool ResetMessageStateOnScenarioStart { get; init; }

    public bool ResetAlertHistoryOnScenarioStart { get; init; }

    public bool ResetRawPacketLogOnScenarioStart { get; init; }

    public bool ResetDecodedEventLogOnScenarioStart { get; init; }

    public bool TransmitDisabled => true;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string? Notes { get; init; }

    public static TrainingModeConfiguration Default { get; } = new();
}
