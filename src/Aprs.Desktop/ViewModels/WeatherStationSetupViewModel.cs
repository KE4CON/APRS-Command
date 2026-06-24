using System.Collections.ObjectModel;
using System.Windows.Input;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WeatherStationSetupViewModel
{
    private readonly IWeatherBeaconScheduler beaconScheduler;
    private readonly IReadOnlyDictionary<string, WeatherInputDriverSnapshot> driverSnapshots;
    private readonly DateTimeOffset now;

    public WeatherStationSetupViewModel(
        IWeatherBeaconScheduler beaconScheduler,
        DateTimeOffset now,
        IEnumerable<WeatherInputDriverSnapshot>? driverSnapshots = null,
        WeatherBeaconSettingsViewModel? beaconSettings = null)
    {
        this.beaconScheduler = beaconScheduler;
        this.now = now;
        this.driverSnapshots = (driverSnapshots ?? []).ToDictionary(snapshot => snapshot.DriverId, StringComparer.OrdinalIgnoreCase);
        AvailableSourceTypes = new ObservableCollection<WeatherSourceSetupOptionViewModel>(CreateSourceOptions());
        SelectedSourceType = AvailableSourceTypes[0];
        BeaconSettings = beaconSettings ?? new WeatherBeaconSettingsViewModel(beaconScheduler);
        ManualEntry = new ManualWeatherEntryViewModel();
        SourceSettings = new WeatherSourceSettingsViewModel(SelectedSourceType);
        SourceStatus = new WeatherSourceStatusViewModel(null);
        WeatherDataPreview = new WeatherObservationPreviewViewModel(null, now);
        Diagnostics = new WeatherDiagnosticsViewModel(WeatherDataPreview, SourceStatus);
        PacketPreview = new WeatherPacketPreviewViewModel(beaconScheduler);
        SafetySummary = "Weather transmit is disabled by default. APRS-IS and RF weather transmit are separate, stale data will not transmit, and missing station/profile safety blocks transmit.";
        RefreshPacketPreviewCommand = new DesktopCommand(RefreshPacketPreview);
        ValidateManualEntryCommand = new DesktopCommand(() => ValidateManualEntry());
    }

    public ObservableCollection<WeatherSourceSetupOptionViewModel> AvailableSourceTypes { get; }

    public WeatherSourceSetupOptionViewModel SelectedSourceType { get; private set; }

    public WeatherSourceSettingsViewModel SourceSettings { get; private set; }

    public WeatherSourceStatusViewModel SourceStatus { get; private set; }

    public WeatherObservationPreviewViewModel WeatherDataPreview { get; private set; }

    public WeatherDiagnosticsViewModel Diagnostics { get; private set; }

    public WeatherPacketPreviewViewModel PacketPreview { get; private set; }

    public WeatherBeaconSettingsViewModel BeaconSettings { get; }

    public ManualWeatherEntryViewModel ManualEntry { get; }

    public string SafetySummary { get; }

    public ICommand RefreshPacketPreviewCommand { get; }

    public ICommand ValidateManualEntryCommand { get; }

    public bool WeatherTransmitDisabledByDefault => !BeaconSettings.WeatherBeaconEnabled
        && !BeaconSettings.AprsIsWeatherTransmitEnabled
        && !BeaconSettings.RfWeatherTransmitEnabled;

    public bool AprsIsAndRfTransmitAreSeparate => BeaconSettings.AprsIsWeatherTransmitEnabled != BeaconSettings.RfWeatherTransmitEnabled
        || (!BeaconSettings.AprsIsWeatherTransmitEnabled && !BeaconSettings.RfWeatherTransmitEnabled);

    public void SelectSource(string sourceKey)
    {
        var selected = AvailableSourceTypes.FirstOrDefault(source =>
            string.Equals(source.Key, sourceKey, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return;
        }

        SelectedSourceType = selected;
        SourceSettings = new WeatherSourceSettingsViewModel(selected);
        var snapshot = ResolveSnapshot(selected);
        SourceStatus = new WeatherSourceStatusViewModel(snapshot);
        WeatherDataPreview = new WeatherObservationPreviewViewModel(snapshot?.LastObservation, now);
        Diagnostics = new WeatherDiagnosticsViewModel(WeatherDataPreview, SourceStatus);
    }

    public void RefreshPacketPreview()
    {
        PacketPreview.Refresh(beaconScheduler.GeneratePreview());
        BeaconSettings.GeneratePreview();
    }

    public bool ValidateManualEntry()
    {
        return ManualEntry.Validate();
    }

    public static WeatherStationSetupViewModel CreateDesignTime()
    {
        var now = DateTimeOffset.UtcNow;
        var observation = CreateSampleObservation(now);
        var snapshot = new WeatherInputDriverSnapshot(
            "manual-weather",
            "Manual Weather",
            WeatherInputDriverType.Manual,
            Enabled: false,
            WeatherInputDriverStatus.Stopped,
            observation,
            now.AddMinutes(-4),
            LastError: null,
            new WeatherObservationValidationResult(true, [], []),
            WeatherInputDriverConfiguration.CreateDefault("manual-weather", "Manual Weather"));
        var scheduler = CreateDesignTimeScheduler(now, observation);
        var beaconSettings = new WeatherBeaconSettingsViewModel(scheduler);
        return new WeatherStationSetupViewModel(scheduler, now, [snapshot], beaconSettings);
    }

    private WeatherInputDriverSnapshot? ResolveSnapshot(WeatherSourceSetupOptionViewModel selected)
    {
        var driverId = selected.Key switch
        {
            "manual" => "manual-weather",
            "tempest-udp" => TempestUdpConfiguration.Default.DriverId,
            "tempest-cloud" => TempestCloudConfiguration.Default.DriverId,
            "peet-bros" => PeetBrosConfiguration.Default.DriverId,
            "davis" => DavisWeatherConfiguration.Default.DriverId,
            "ambient" => AmbientWeatherConfiguration.Default.DriverId,
            "ecowitt" => EcowittWeatherConfiguration.Default.DriverId,
            _ when selected.Key.StartsWith("software-", StringComparison.Ordinal) => WeatherSoftwareImportConfiguration.Default.DriverId,
            "simulation" => "simulation-weather",
            _ => selected.Key
        };

        return driverSnapshots.TryGetValue(driverId, out var snapshot) ? snapshot : null;
    }

    private static IReadOnlyList<WeatherSourceSetupOptionViewModel> CreateSourceOptions()
    {
        return
        [
            new("manual", "Manual weather entry", "Manual"),
            new("tempest-udp", "WeatherFlow Tempest local UDP", "WeatherFlow"),
            new("tempest-cloud", "WeatherFlow Tempest Cloud API", "WeatherFlow"),
            new("peet-bros", "Peet Bros / ULTIMETER", "Serial"),
            new("davis", "Davis WeatherLink / Davis weather station", "Davis"),
            new("ambient", "Ambient Weather", "Cloud"),
            new("ecowitt", "Ecowitt / Fine Offset / GW1000", "Local gateway"),
            new("software-cumulus", "Cumulus MX", "Weather software"),
            new("software-weewx", "WeeWX", "Weather software"),
            new("software-weather-display", "Weather Display", "Weather software"),
            new("software-realtime", "Generic realtime.txt", "Weather software"),
            new("software-json", "Generic JSON file", "Weather software"),
            new("software-csv", "Generic CSV file", "Weather software"),
            new("software-key-value", "Generic key-value text file", "Weather software"),
            new("software-http", "Local HTTP endpoint placeholder", "Weather software"),
            new("simulation", "Simulation/test source", "Simulation")
        ];
    }

    private static CommonWeatherObservation CreateSampleObservation(DateTimeOffset now)
    {
        return new CommonWeatherObservation(
            "Manual Weather",
            WeatherObservationSourceType.Manual,
            "manual-weather",
            "N0CALL",
            now.AddMinutes(-4),
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
            420,
            2.1,
            null,
            null,
            null,
            new Dictionary<string, string> { ["battery"] = "ok", ["source"] = "demo" },
            "{\"source\":\"demo-weather\"}",
            WeatherDataState.Current,
            [],
            []);
    }

    private static WeatherBeaconScheduler CreateDesignTimeScheduler(DateTimeOffset now, CommonWeatherObservation observation)
    {
        var profileService = new LocalStationProfileService(now);
        profileService.UpdateProfile(LocalStationProfile.CreateDefault(now) with
        {
            Callsign = "N0CALL",
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333
        }, now);
        return new WeatherBeaconScheduler(
            profileService,
            new AprsWeatherFormatter(),
            new DesignWeatherObservationSourceProvider("manual-weather", observation),
            new DesignAprsIsClient(),
            WeatherBeaconConfiguration.Default with { SelectedWeatherSourceDriverId = "manual-weather" },
            new DesignBeaconClock(now));
    }

    private sealed class DesignWeatherObservationSourceProvider : IWeatherObservationSourceProvider
    {
        private readonly string driverId;
        private readonly CommonWeatherObservation observation;

        public DesignWeatherObservationSourceProvider(string driverId, CommonWeatherObservation observation)
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
