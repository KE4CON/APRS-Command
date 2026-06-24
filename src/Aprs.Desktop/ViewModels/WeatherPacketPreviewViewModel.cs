using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WeatherPacketPreviewViewModel
{
    public WeatherPacketPreviewViewModel(IWeatherBeaconScheduler beaconScheduler)
    {
        Refresh(beaconScheduler.GeneratePreview());
    }

    public string Packet { get; private set; } = "-";

    public string ValidationErrors { get; private set; } = "-";

    public string ValidationWarnings { get; private set; } = "-";

    public string SelectedSource { get; private set; } = "-";

    public bool AprsIsEligible { get; private set; }

    public bool RfEligible { get; private set; }

    public string StaleWarning { get; private set; } = "-";

    public string BlockedReason => ValidationErrors;

    public void Refresh(WeatherBeaconPreviewResult preview)
    {
        Packet = preview.Packet ?? "-";
        ValidationErrors = preview.ValidationErrors.Count == 0 ? "-" : string.Join("; ", preview.ValidationErrors);
        ValidationWarnings = preview.ValidationWarnings.Count == 0 ? "-" : string.Join("; ", preview.ValidationWarnings);
        SelectedSource = preview.SelectedSourceName ?? "-";
        AprsIsEligible = preview.AprsIsEligible;
        RfEligible = preview.RfEligible;
        StaleWarning = preview.IsStale ? "Stale weather data will not transmit." : "Current weather data.";
    }
}
