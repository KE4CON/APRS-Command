namespace Aprs.Services;

public sealed record AprsWeatherFormatterOptions(
    string Destination,
    IReadOnlyList<string> Path,
    bool UsePosition,
    string? Comment)
{
    public static AprsWeatherFormatterOptions Default { get; } = new(
        Destination: Aprs.Core.AprsConstants.ToCall,
        Path: [],
        UsePosition: true,
        Comment: null);
}
