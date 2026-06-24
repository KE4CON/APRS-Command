using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WeatherSourceStatusViewModel
{
    public WeatherSourceStatusViewModel(WeatherInputDriverSnapshot? snapshot)
    {
        DriverStatus = snapshot?.Status.ToString() ?? "Not configured";
        DriverType = snapshot?.DriverType.ToString() ?? "Unknown";
        LastSuccessfulUpdate = FormatTime(snapshot?.LastObservationTimeUtc);
        LastFailedUpdate = "-";
        LastError = snapshot?.LastError?.Message ?? "-";
        ValidationStatus = snapshot?.LastValidationResult.IsValid is false ? "Invalid" : "OK";
        ValidationErrors = FormatList(snapshot?.LastValidationResult.Errors);
        ValidationWarnings = FormatList(snapshot?.LastValidationResult.Warnings);
        ConnectionState = snapshot?.Enabled is true ? "Enabled" : "Disabled";
    }

    public string DriverStatus { get; }

    public string DriverType { get; }

    public string LastSuccessfulUpdate { get; }

    public string LastFailedUpdate { get; }

    public string LastError { get; }

    public string ValidationStatus { get; }

    public string ValidationErrors { get; }

    public string ValidationWarnings { get; }

    public string ConnectionState { get; }

    private static string FormatTime(DateTimeOffset? timestamp)
    {
        return timestamp is null ? "-" : timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
    }

    private static string FormatList(IReadOnlyList<string>? values)
    {
        return values is null || values.Count == 0 ? "-" : string.Join("; ", values);
    }
}
