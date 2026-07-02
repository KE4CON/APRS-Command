namespace Aprs.Services;

/// <summary>
/// A single scheduled beacon entry — fires a beacon at a specific time of day,
/// optionally on specific days of the week.
/// </summary>
public sealed record ScheduledBeaconEntry(
    Guid         Id,
    string       Label,
    TimeOnly     FireAt,
    bool         Monday,
    bool         Tuesday,
    bool         Wednesday,
    bool         Thursday,
    bool         Friday,
    bool         Saturday,
    bool         Sunday,
    bool         Enabled,
    string?      CustomComment)
{
    public static ScheduledBeaconEntry CreateDefault() => new(
        Id:            Guid.NewGuid(),
        Label:         "Daily check-in",
        FireAt:        new TimeOnly(12, 0),
        Monday:        true,
        Tuesday:       true,
        Wednesday:     true,
        Thursday:      true,
        Friday:        true,
        Saturday:      false,
        Sunday:        false,
        Enabled:       true,
        CustomComment: null);

    public bool AppliesToDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday    => Monday,
        DayOfWeek.Tuesday   => Tuesday,
        DayOfWeek.Wednesday => Wednesday,
        DayOfWeek.Thursday  => Thursday,
        DayOfWeek.Friday    => Friday,
        DayOfWeek.Saturday  => Saturday,
        DayOfWeek.Sunday    => Sunday,
        _                   => false,
    };

    public string DaysDisplay =>
        new[]
        {
            Monday    ? "Mo" : null,
            Tuesday   ? "Tu" : null,
            Wednesday ? "We" : null,
            Thursday  ? "Th" : null,
            Friday    ? "Fr" : null,
            Saturday  ? "Sa" : null,
            Sunday    ? "Su" : null,
        }
        .Where(d => d is not null)
        .DefaultIfEmpty("None")
        .Aggregate((a, b) => $"{a} {b}")!;
}
