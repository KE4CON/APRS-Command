using System.Windows.Input;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WeatherBeaconSettingsViewModel
{
    private readonly IWeatherBeaconScheduler scheduler;

    public WeatherBeaconSettingsViewModel(IWeatherBeaconScheduler scheduler)
    {
        this.scheduler = scheduler;
        PreviewCommand = new DesktopCommand(GeneratePreview);
        TransmitAprsIsNowCommand = new DesktopCommand(() => TransmitNow(WeatherBeaconTransmitTransport.AprsIs));
        TransmitRfNowCommand = new DesktopCommand(() => TransmitNow(WeatherBeaconTransmitTransport.Rf));
        RefreshFromState();
    }

    public string SelectedWeatherSource { get; private set; } = "Not selected";

    public string LastObservationTime { get; private set; } = "-";

    public string SourceStatus { get; private set; } = "Unknown";

    public string StaleWarning { get; private set; } = "No weather source selected.";

    public string PacketPreview { get; private set; } = "Generate a preview to inspect the APRS weather packet.";

    public bool WeatherBeaconEnabled { get; private set; }

    public bool AprsIsWeatherTransmitEnabled { get; private set; }

    public bool RfWeatherTransmitEnabled { get; private set; }

    public string TransmitInterval { get; private set; } = "30 min";

    public string LastTransmitResult { get; private set; } = "-";

    public string NextTransmitTime { get; private set; } = "-";

    public string LastBlockedReason { get; private set; } = "-";

    public ICommand PreviewCommand { get; }

    public ICommand TransmitAprsIsNowCommand { get; }

    public ICommand TransmitRfNowCommand { get; }

    public void GeneratePreview()
    {
        var preview = scheduler.GeneratePreview();
        PacketPreview = preview.Packet ?? string.Join(Environment.NewLine, preview.ValidationErrors);
        RefreshFromState(keepPreview: true);
        SourceStatus = preview.IsSuccess ? "Preview valid" : "Preview blocked";
        StaleWarning = preview.IsStale
            ? "Weather data is stale and will not be transmitted."
            : preview.ValidationWarnings.FirstOrDefault() ?? "Weather data is current.";
        SelectedWeatherSource = preview.SelectedSourceName ?? SelectedWeatherSource;
        LastBlockedReason = preview.ValidationErrors.FirstOrDefault() ?? "-";
    }

    public void TransmitNow(WeatherBeaconTransmitTransport transport)
    {
        var result = scheduler.TransmitWeatherNowAsync(transport).GetAwaiter().GetResult();
        LastTransmitResult = result.IsSuccess
            ? $"{transport}: transmitted at {FormatTime(result.TimestampUtc)}"
            : $"{transport}: blocked - {result.FailureReason}";
        LastBlockedReason = result.FailureReason ?? "-";
        RefreshFromState(keepPreview: true);
    }

    public void RefreshFromState(bool keepPreview = false)
    {
        var state = scheduler.GetState();
        WeatherBeaconEnabled = state.SchedulerEnabled;
        AprsIsWeatherTransmitEnabled = state.AprsIsTransmitEnabled;
        RfWeatherTransmitEnabled = state.RfTransmitEnabled;
        SelectedWeatherSource = state.LastWeatherObservationSource
            ?? state.SelectedWeatherSourceDriverId
            ?? "Not selected";
        LastObservationTime = FormatTime(state.LastWeatherObservationTimeUtc);
        NextTransmitTime = FormatTime(state.NextScheduledTransmitTimeUtc);
        LastBlockedReason = state.LastBlockedReason ?? LastBlockedReason;
        LastTransmitResult = state.LastTransmitResult is null
            ? LastTransmitResult
            : state.LastTransmitResult.IsSuccess
                ? $"{state.LastTransmitResult.DestinationTransport}: transmitted at {FormatTime(state.LastTransmitResult.TimestampUtc)}"
                : $"{state.LastTransmitResult.DestinationTransport}: blocked - {state.LastTransmitResult.FailureReason}";
        SourceStatus = state.LastErrorOrWarning is null ? "Ready" : state.LastErrorOrWarning;

        if (!keepPreview)
        {
            PacketPreview = state.LastGeneratedWeatherPacket ?? PacketPreview;
        }
    }

    public static WeatherBeaconSettingsViewModel CreateDesignTime()
    {
        var now = DateTimeOffset.UtcNow;
        var observation = new CommonWeatherObservation(
            "Demo Weather",
            WeatherObservationSourceType.Simulation,
            "DEMO-WX",
            "N0CALL",
            now.AddMinutes(-3),
            39.058333,
            -84.508333,
            180,
            5,
            10,
            72,
            0,
            0,
            0,
            50,
            1013.2,
            350,
            2.1,
            null,
            null,
            null,
            new Dictionary<string, string> { ["mode"] = "design" },
            "{\"demo\":true}",
            WeatherDataState.Current,
            [],
            []);
        var provider = new DesignWeatherObservationProvider("demo-weather", observation);
        var profileService = new LocalStationProfileService(now);
        profileService.UpdateProfile(LocalStationProfile.CreateDefault(now) with
        {
            Callsign = "N0CALL",
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333
        }, now);
        var scheduler = new WeatherBeaconScheduler(
            profileService,
            new AprsWeatherFormatter(),
            provider,
            new DesignAprsIsClient(),
            WeatherBeaconConfiguration.Default with { SelectedWeatherSourceDriverId = "demo-weather" },
            new DesignBeaconClock(now));

        var viewModel = new WeatherBeaconSettingsViewModel(scheduler);
        viewModel.GeneratePreview();
        return viewModel;
    }

    private static string FormatTime(DateTimeOffset? timestamp)
    {
        return timestamp is null ? "-" : timestamp.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    }

    private sealed class DesignWeatherObservationProvider : IWeatherObservationSourceProvider
    {
        private readonly string driverId;
        private readonly CommonWeatherObservation observation;

        public DesignWeatherObservationProvider(string driverId, CommonWeatherObservation observation)
        {
            this.driverId = driverId;
            this.observation = observation;
        }

        public CommonWeatherObservation? GetLatestObservation(string candidateDriverId)
        {
            return string.Equals(driverId, candidateDriverId, StringComparison.OrdinalIgnoreCase) ? observation : null;
        }

        public string? GetSourceName(string candidateDriverId)
        {
            return string.Equals(driverId, candidateDriverId, StringComparison.OrdinalIgnoreCase) ? observation.SourceName : null;
        }
    }

    private sealed class DesignBeaconClock : IBeaconSchedulerClock
    {
        public DesignBeaconClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class DesignAprsIsClient : Aprs.Transport.IAprsIsClient
    {
        public event EventHandler<Aprs.Transport.AprsIsRawPacketReceivedEventArgs>? RawPacketReceived
        {
            add { }
            remove { }
        }

        public Aprs.Transport.AprsIsConnectionState State => Aprs.Transport.AprsIsConnectionState.Disconnected;

        public Exception? LastError => null;

        public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Aprs.Transport.AprsIsTransmitResult> SendRawPacketAsync(
            string rawPacketLine,
            bool transmitConfirmed,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Aprs.Transport.AprsIsTransmitResult.Failed(
                DateTimeOffset.UtcNow,
                rawPacketLine,
                State,
                "Design-time APRS-IS client is disconnected."));
        }

        public async IAsyncEnumerable<Aprs.Transport.AprsIsRawPacketReceivedEventArgs> ReadPacketsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
