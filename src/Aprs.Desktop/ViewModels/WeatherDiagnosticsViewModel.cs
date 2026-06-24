namespace Aprs.Desktop.ViewModels;

public sealed class WeatherDiagnosticsViewModel
{
    public WeatherDiagnosticsViewModel(WeatherObservationPreviewViewModel observation, WeatherSourceStatusViewModel status)
    {
        RawSourcePayload = observation.RawPayload;
        LastError = status.LastError;
        DriverStatus = status.DriverStatus;
        LastSuccessfulUpdate = status.LastSuccessfulUpdate;
        LastFailedUpdate = status.LastFailedUpdate;
        ValidationWarnings = status.ValidationWarnings;
        ValidationErrors = status.ValidationErrors;
        ConnectionState = status.ConnectionState;
    }

    public string RawSourcePayload { get; }

    public string LastError { get; }

    public string DriverStatus { get; }

    public string LastSuccessfulUpdate { get; }

    public string LastFailedUpdate { get; }

    public string ValidationWarnings { get; }

    public string ValidationErrors { get; }

    public string ConnectionState { get; }
}
