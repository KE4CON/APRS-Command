using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class TrainingEventRowViewModel
{
    public TrainingEventRowViewModel(TrainingEventRecord trainingEvent)
    {
        Time = trainingEvent.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        State = trainingEvent.State.ToString();
        Summary = trainingEvent.Summary;
        Details = string.IsNullOrWhiteSpace(trainingEvent.Details) ? "-" : trainingEvent.Details;
    }

    public string Time { get; }

    public string State { get; }

    public string Summary { get; }

    public string Details { get; }
}
