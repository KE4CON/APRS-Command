namespace Aprs.Desktop.Configuration;

/// <summary>Persisted message templates for the Message Center compose area.</summary>
public sealed record MessageTemplatesSettings(IReadOnlyList<MessageTemplate> Templates)
{
    public static MessageTemplatesSettings Default { get; } = new(
    [
        MessageTemplate.Create("Check In",       string.Empty, "Checking in. All OK."),
        MessageTemplate.Create("At Position",    string.Empty, "At position. Standing by."),
        MessageTemplate.Create("Departing",      string.Empty, "Departing now. ETA TBD."),
        MessageTemplate.Create("Need Assistance",string.Empty, "Need assistance at my position."),
        MessageTemplate.Create("All Clear",      string.Empty, "All clear. No issues to report."),
        MessageTemplate.Create("Copy That",      string.Empty, "Copy that. Acknowledged."),
        MessageTemplate.Create("Say Again",      string.Empty, "Please say again. Did not copy."),
    ]);
}
