namespace Aprs.Desktop.ViewModels;

public sealed record HelpTopic(
    string Id,
    string Title,
    string? RelativePath,
    string Content,
    bool IsAvailable);
