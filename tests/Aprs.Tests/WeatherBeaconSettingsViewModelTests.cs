using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class WeatherBeaconSettingsViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GeneratePreview_UpdatesPacketPreviewWithoutTransmit()
    {
        var client = new FakeAprsIsClient();
        var viewModel = new WeatherBeaconSettingsViewModel(CreateScheduler(client));

        viewModel.GeneratePreview();

        Assert.Contains("N0CALL>APRS:!3903.50N/08430.50W_", viewModel.PacketPreview, StringComparison.Ordinal);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("Preview", viewModel.SourceStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BlockedTransmit_UpdatesLastResultAndBlockedReason()
    {
        var viewModel = new WeatherBeaconSettingsViewModel(CreateScheduler(new FakeAprsIsClient()));

        viewModel.TransmitNow(WeatherBeaconTransmitTransport.AprsIs);

        Assert.Contains("blocked", viewModel.LastTransmitResult, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("disabled", viewModel.LastBlockedReason, StringComparison.OrdinalIgnoreCase);
    }

    private static WeatherBeaconScheduler CreateScheduler(FakeAprsIsClient client)
    {
        var profileService = new LocalStationProfileService(Now);
        var provider = new FakeWeatherObservationSourceProvider(CreateObservation());
        return new WeatherBeaconScheduler(
            profileService,
            new AprsWeatherFormatter(),
            provider,
            client,
            WeatherBeaconConfiguration.Default with { SelectedWeatherSourceDriverId = "manual-wx" },
            new FakeBeaconSchedulerClock { UtcNow = Now });
    }

    private static CommonWeatherObservation CreateObservation()
    {
        return new CommonWeatherObservation(
            "Manual Weather",
            WeatherObservationSourceType.Manual,
            "manual-station",
            "N0CALL",
            Now,
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
            Diagnostics: new Dictionary<string, string>(),
            RawSourcePayload: "manual payload",
            WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);
    }

    private sealed class FakeWeatherObservationSourceProvider : IWeatherObservationSourceProvider
    {
        private readonly CommonWeatherObservation observation;

        public FakeWeatherObservationSourceProvider(CommonWeatherObservation observation)
        {
            this.observation = observation;
        }

        public CommonWeatherObservation? GetLatestObservation(string driverId)
        {
            return string.Equals(driverId, "manual-wx", StringComparison.OrdinalIgnoreCase) ? observation : null;
        }

        public string? GetSourceName(string driverId)
        {
            return string.Equals(driverId, "manual-wx", StringComparison.OrdinalIgnoreCase) ? observation.SourceName : null;
        }
    }

    private sealed class FakeBeaconSchedulerClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeAprsIsClient : IAprsIsClient
    {
        public event EventHandler<AprsIsRawPacketReceivedEventArgs>? RawPacketReceived
        {
            add { }
            remove { }
        }

        public AprsIsConnectionState State => AprsIsConnectionState.Disconnected;

        public Exception? LastError => null;

        public int SendCallCount { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DisconnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<AprsIsTransmitResult> SendRawPacketAsync(
            string rawPacketLine,
            bool transmitConfirmed,
            CancellationToken cancellationToken)
        {
            SendCallCount++;
            return Task.FromResult(AprsIsTransmitResult.Failed(
                Now,
                rawPacketLine,
                AprsIsConnectionState.Disconnected,
                "APRS-IS client is disconnected."));
        }

        public async IAsyncEnumerable<AprsIsRawPacketReceivedEventArgs> ReadPacketsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
