using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class WeatherStationSetupViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LoadsAvailableSourceTypes()
    {
        var viewModel = CreateViewModel();

        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "manual");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "tempest-udp");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "tempest-cloud");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "peet-bros");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "davis");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "ambient");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "ecowitt");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "software-cumulus");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "software-weewx");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "software-weather-display");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "software-realtime");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "software-json");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "software-csv");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "software-key-value");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "software-http");
        Assert.Contains(viewModel.AvailableSourceTypes, source => source.Key == "simulation");
    }

    [Theory]
    [InlineData("tempest-udp", nameof(WeatherSourceSettingsViewModel.ShowsTempestUdpSettings))]
    [InlineData("peet-bros", nameof(WeatherSourceSettingsViewModel.ShowsPeetBrosSettings))]
    [InlineData("davis", nameof(WeatherSourceSettingsViewModel.ShowsDavisSettings))]
    [InlineData("ambient", nameof(WeatherSourceSettingsViewModel.ShowsAmbientSettings))]
    [InlineData("ecowitt", nameof(WeatherSourceSettingsViewModel.ShowsEcowittSettings))]
    [InlineData("software-realtime", nameof(WeatherSourceSettingsViewModel.ShowsWeatherSoftwareSettings))]
    public void SelectingSourceExposesExpectedSettings(string sourceKey, string propertyName)
    {
        var viewModel = CreateViewModel();

        viewModel.SelectSource(sourceKey);

        var property = typeof(WeatherSourceSettingsViewModel).GetProperty(propertyName);
        Assert.NotNull(property);
        Assert.True((bool)property.GetValue(viewModel.SourceSettings)!);
    }

    [Fact]
    public void ManualWeatherEntry_ValidatesRequiredFields()
    {
        var viewModel = CreateViewModel();

        var valid = viewModel.ValidateManualEntry();

        Assert.False(valid);
        Assert.Contains(viewModel.ManualEntry.ValidationErrors, error => error.Contains("Temperature", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(viewModel.ManualEntry.ValidationErrors, error => error.Contains("Timestamp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WeatherDataPreview_HandlesMissingFieldsSafely()
    {
        var preview = new WeatherObservationPreviewViewModel(null, Now);

        Assert.Equal("-", preview.SourceName);
        Assert.Equal("-", preview.Wind);
        Assert.Equal("-", preview.Temperature);
        Assert.Equal("No observation has been received.", preview.StaleWarning);
    }

    [Fact]
    public void StaleDataWarning_AppearsForStaleObservation()
    {
        var viewModel = CreateViewModel(CreateSnapshot(CreateObservation(staleDataState: WeatherDataState.Stale)));

        viewModel.SelectSource("manual");

        Assert.Contains("Stale", viewModel.WeatherDataPreview.StaleWarning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PacketPreview_CanBeGeneratedWithoutTransmitting()
    {
        var scheduler = new FakeWeatherBeaconScheduler();
        var viewModel = CreateViewModel(scheduler: scheduler);

        viewModel.RefreshPacketPreview();

        Assert.Contains("N0CALL>APRS", viewModel.PacketPreview.Packet, StringComparison.Ordinal);
        Assert.Equal(3, scheduler.GeneratePreviewCallCount);
        Assert.Equal(0, scheduler.TransmitCallCount);
    }

    [Fact]
    public void TransmitControls_AreDisabledByDefaultAndSeparate()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.WeatherTransmitDisabledByDefault);
        Assert.True(viewModel.AprsIsAndRfTransmitAreSeparate);
        Assert.False(viewModel.BeaconSettings.AprsIsWeatherTransmitEnabled);
        Assert.False(viewModel.BeaconSettings.RfWeatherTransmitEnabled);
    }

    [Fact]
    public void PacketPreview_ExposesValidationErrors()
    {
        var scheduler = new FakeWeatherBeaconScheduler
        {
            PreviewResult = WeatherBeaconPreviewResult.Failed(["Invalid weather observation."])
        };
        var viewModel = CreateViewModel(scheduler: scheduler);

        viewModel.RefreshPacketPreview();

        Assert.Contains("Invalid weather observation", viewModel.PacketPreview.ValidationErrors);
    }

    private static WeatherStationSetupViewModel CreateViewModel(
        WeatherInputDriverSnapshot? snapshot = null,
        FakeWeatherBeaconScheduler? scheduler = null)
    {
        scheduler ??= new FakeWeatherBeaconScheduler();
        return new WeatherStationSetupViewModel(
            scheduler,
            Now,
            snapshot is null ? [] : [snapshot],
            new WeatherBeaconSettingsViewModel(scheduler));
    }

    private static WeatherInputDriverSnapshot CreateSnapshot(CommonWeatherObservation observation)
    {
        return new WeatherInputDriverSnapshot(
            "manual-weather",
            "Manual Weather",
            WeatherInputDriverType.Manual,
            Enabled: false,
            WeatherInputDriverStatus.Stopped,
            observation,
            observation.TimestampUtc,
            LastError: null,
            new WeatherObservationValidationResult(true, [], []),
            WeatherInputDriverConfiguration.CreateDefault("manual-weather", "Manual Weather"));
    }

    private static CommonWeatherObservation CreateObservation(WeatherDataState staleDataState = WeatherDataState.Current)
    {
        return new CommonWeatherObservation(
            "Manual Weather",
            WeatherObservationSourceType.Manual,
            "manual-weather",
            "N0CALL",
            Now.AddMinutes(-20),
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
            LuminosityWattsPerSquareMeter: null,
            UvIndex: null,
            SnowInches: null,
            LightningCount: null,
            LightningDistanceMiles: null,
            Diagnostics: new Dictionary<string, string> { ["battery"] = "ok" },
            RawSourcePayload: "raw weather payload",
            staleDataState,
            ValidationErrors: [],
            ValidationWarnings: []);
    }

    private sealed class FakeWeatherBeaconScheduler : IWeatherBeaconScheduler
    {
        public int GeneratePreviewCallCount { get; private set; }

        public int TransmitCallCount { get; private set; }

        public WeatherBeaconPreviewResult PreviewResult { get; set; } = new(
            IsSuccess: true,
            Packet: "N0CALL>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132",
            ValidationErrors: [],
            ValidationWarnings: [],
            AprsIsEligible: false,
            RfEligible: false,
            IsStale: false,
            SelectedSourceName: "Manual Weather");

        public WeatherBeaconSchedulerState GetState()
        {
            return new WeatherBeaconSchedulerState(
                SchedulerEnabled: false,
                AprsIsTransmitEnabled: false,
                RfTransmitEnabled: false,
                SelectedWeatherSourceDriverId: "manual-weather",
                LastWeatherObservationTimeUtc: Now,
                LastWeatherObservationSource: "Manual Weather",
                LastGeneratedWeatherPacket: PreviewResult.Packet,
                LastTransmitResult: null,
                LastBlockedReason: null,
                LastErrorOrWarning: null,
                NextScheduledTransmitTimeUtc: null,
                LastScheduledTransmitTimeUtc: null,
                TransmitCount: 0,
                BlockedTransmitCount: 0);
        }

        public WeatherBeaconSchedulerState Start() => GetState();

        public WeatherBeaconSchedulerState Stop() => GetState();

        public WeatherBeaconSchedulerState SelectWeatherSource(string driverId) => GetState();

        public WeatherBeaconPreviewResult GeneratePreview(WeatherBeaconTransmitTransport? preferredTransport = null)
        {
            GeneratePreviewCallCount++;
            return PreviewResult;
        }

        public Task<WeatherBeaconTransmitResult> TransmitWeatherNowAsync(
            WeatherBeaconTransmitTransport destinationTransport,
            CancellationToken cancellationToken = default)
        {
            TransmitCallCount++;
            return Task.FromResult(WeatherBeaconTransmitResult.Failed(
                Now,
                destinationTransport,
                PreviewResult.Packet,
                "Transmit disabled in fake scheduler."));
        }

        public Task<WeatherBeaconTransmitResult?> TickAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WeatherBeaconTransmitResult?>(null);
        }
    }
}
