using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class TrainingTaskRowViewModel
{
    public TrainingTaskRowViewModel(TrainingScenarioTask task)
    {
        TaskId = task.TaskId;
        Title = task.Title;
        Description = task.Description;
        Status = task.Status.ToString();
    }

    public Guid TaskId { get; }

    public string Title { get; }

    public string Description { get; }

    public string Status { get; }
}
