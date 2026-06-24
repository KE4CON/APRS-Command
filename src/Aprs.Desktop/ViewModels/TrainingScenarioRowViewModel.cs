using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class TrainingScenarioRowViewModel
{
    public TrainingScenarioRowViewModel(TrainingScenario scenario)
    {
        ScenarioId = scenario.ScenarioId;
        ScenarioName = scenario.ScenarioName;
        Difficulty = scenario.Difficulty.ToString();
        ScenarioType = scenario.ScenarioType.ToString();
        Duration = $"{scenario.EstimatedDuration.TotalMinutes:0} min";
        Description = scenario.Description;
    }

    public Guid ScenarioId { get; }

    public string ScenarioName { get; }

    public string Difficulty { get; }

    public string ScenarioType { get; }

    public string Duration { get; }

    public string Description { get; }
}
