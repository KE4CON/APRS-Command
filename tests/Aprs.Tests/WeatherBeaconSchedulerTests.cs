using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class WeatherBeaconSchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultConfiguration_DisablesAllWeatherTransmit()
    {
        var (_, _, _, scheduler, _) = CreateScheduler();

        var state = scheduler.GetState();

        Assert.False(state.SchedulerEnabled);
        Assert.False(state.AprsIsTransmitEnabled);
        Assert.False(state.RfTransmitEnabled);
        Assert.Null(state.NextScheduledTransmitTimeUtc);
    }

    [Fact]
    public void ValidObservation_GeneratesAprsWeatherPacketPreviewWithoutTransmit()
    {
        var (_, client, _, scheduler, _) = CreateScheduler(
            WeatherBeaconConfiguration.Default with { SelectedWeatherSourceDriverId = "manual-wx" });

        var preview = scheduler.GeneratePreview();

        Assert.True(preview.IsSuccess);
        Assert.Equal("N0CALL>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", preview.Packet);
        Assert.Equal(0, client.SendCallCount);
        Assert.False(preview.AprsIsEligible);
        Assert.False(preview.RfEligible);
    }

    [Fact]
    public void StaleObservation_IsRejected()
    {
        var provider = new FakeWeatherObservationSourceProvider(
            "manual-wx",
            CreateObservation(timestamp: Now.AddMinutes(-30)));
        var (_, _, _, scheduler, _) = CreateScheduler(
            WeatherBeaconConfiguration.Default with { SelectedWeatherSourceDriverId = "manual-wx" },
            provider);

        var preview = scheduler.GeneratePreview();

        Assert.False(preview.IsSuccess);
        Assert.True(preview.IsStale);
        Assert.Contains(preview.ValidationErrors, error => error.Contains("Stale weather data", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidWeatherValues_AreRejected()
    {
        var provider = new FakeWeatherObservationSourceProvider(
            "manual-wx",
            CreateObservation(humidity: 150));
        var (_, _, _, scheduler, _) = CreateScheduler(
            WeatherBeaconConfiguration.Default with { SelectedWeatherSourceDriverId = "manual-wx" },
            provider);

        var preview = scheduler.GeneratePreview();

        Assert.False(preview.IsSuccess);
        Assert.Contains(preview.ValidationErrors, error => error.Contains("Humidity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MissingLocalStationCallsign_BlocksTransmit()
    {
        var (profileService, client, _, scheduler, _) = CreateScheduler(CreateAprsIsConfig());
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: true, aprsIsTransmitEnabled: true) with
        {
            Callsign = string.Empty
        }, Now);
        client.State = AprsIsConnectionState.Connected;
        scheduler.Start();

        var result = await scheduler.TransmitWeatherNowAsync(WeatherBeaconTransmitTransport.AprsIs);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("profile", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IntervalShorterThanMinimum_BlocksTransmit()
    {
        var (_, client, _, scheduler, _) = CreateScheduler(CreateAprsIsConfig() with
        {
            WeatherTransmitInterval = TimeSpan.FromMinutes(1)
        });
        scheduler.Start();

        var result = await scheduler.TransmitWeatherNowAsync(WeatherBeaconTransmitTransport.AprsIs);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("minimum", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AprsIsTransmitDisabled_BlocksAprsIsTransmit()
    {
        var (profileService, client, _, scheduler, _) = CreateScheduler(WeatherBeaconConfiguration.Default with
        {
            WeatherBeaconEnabled = true,
            SelectedWeatherSourceDriverId = "manual-wx"
        });
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: true, aprsIsTransmitEnabled: true), Now);
        client.State = AprsIsConnectionState.Connected;
        scheduler.Start();

        var result = await scheduler.TransmitWeatherNowAsync(WeatherBeaconTransmitTransport.AprsIs);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("APRS-IS weather transmit is disabled", result.FailureReason);
    }

    [Fact]
    public async Task RfTransmitDisabled_BlocksRfTransmit()
    {
        var (_, _, rfClient, scheduler, _) = CreateScheduler(WeatherBeaconConfiguration.Default with
        {
            WeatherBeaconEnabled = true,
            SelectedWeatherSourceDriverId = "manual-wx",
            RfPath = ["WIDE1-1"]
        });
        scheduler.Start();

        var result = await scheduler.TransmitWeatherNowAsync(WeatherBeaconTransmitTransport.Rf);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, rfClient.SendCallCount);
        Assert.Contains("RF weather transmit is disabled", result.FailureReason);
    }

    [Fact]
    public void AprsIsAndRfTransmitFlagsRemainSeparate()
    {
        var (_, _, _, aprsIsScheduler, _) = CreateScheduler(WeatherBeaconConfiguration.Default with
        {
            WeatherBeaconEnabled = true,
            AprsIsWeatherTransmitEnabled = true,
            RfWeatherTransmitEnabled = false,
            SelectedWeatherSourceDriverId = "manual-wx"
        });
        var (_, _, _, rfScheduler, _) = CreateScheduler(WeatherBeaconConfiguration.Default with
        {
            WeatherBeaconEnabled = true,
            AprsIsWeatherTransmitEnabled = false,
            RfWeatherTransmitEnabled = true,
            SelectedWeatherSourceDriverId = "manual-wx"
        });

        Assert.True(aprsIsScheduler.GetState().AprsIsTransmitEnabled);
        Assert.False(aprsIsScheduler.GetState().RfTransmitEnabled);
        Assert.False(rfScheduler.GetState().AprsIsTransmitEnabled);
        Assert.True(rfScheduler.GetState().RfTransmitEnabled);
    }

    [Fact]
    public async Task AprsIsTransmit_CallsClientOnlyWhenSafetyChecksPass()
    {
        var (profileService, client, _, scheduler, _) = CreateScheduler(CreateAprsIsConfig());
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: true, aprsIsTransmitEnabled: true), Now);
        client.State = AprsIsConnectionState.Connected;
        scheduler.Start();

        var result = await scheduler.TransmitWeatherNowAsync(WeatherBeaconTransmitTransport.AprsIs);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, client.SendCallCount);
        Assert.True(client.LastTransmitConfirmed);
        Assert.Equal("N0CALL>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", client.LastPacket);
        Assert.Equal(1, scheduler.GetState().TransmitCount);
    }

    [Fact]
    public async Task RfTransmit_CallsRfClientOnlyWhenSafetyChecksPass()
    {
        var (profileService, _, rfClient, scheduler, _) = CreateScheduler(WeatherBeaconConfiguration.Default with
        {
            WeatherBeaconEnabled = true,
            RfWeatherTransmitEnabled = true,
            SelectedWeatherSourceDriverId = "manual-wx",
            RfPath = ["WIDE1-1"]
        });
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: true, rfTransmitEnabled: true), Now);
        scheduler.Start();

        var result = await scheduler.TransmitWeatherNowAsync(WeatherBeaconTransmitTransport.Rf);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, rfClient.SendCallCount);
        Assert.Equal("N0CALL>APRS,WIDE1-1:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", rfClient.LastPacket);
        Assert.Equal(1, scheduler.GetState().TransmitCount);
    }

    [Fact]
    public async Task BlockedAndSuccessfulAttempts_AreLogged()
    {
        var (profileService, client, _, scheduler, _) = CreateScheduler(CreateAprsIsConfig());
        scheduler.Start();

        var blocked = await scheduler.TransmitWeatherNowAsync(WeatherBeaconTransmitTransport.AprsIs);

        Assert.False(blocked.IsSuccess);
        Assert.Equal(1, scheduler.GetState().BlockedTransmitCount);
        Assert.NotNull(scheduler.GetState().LastBlockedReason);

        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: true, aprsIsTransmitEnabled: true), Now);
        client.State = AprsIsConnectionState.Connected;

        var success = await scheduler.TransmitWeatherNowAsync(WeatherBeaconTransmitTransport.AprsIs);

        Assert.True(success.IsSuccess);
        Assert.Equal(1, scheduler.GetState().TransmitCount);
        Assert.NotNull(scheduler.GetState().LastTransmitResult);
    }

    private static WeatherBeaconConfiguration CreateAprsIsConfig()
    {
        return WeatherBeaconConfiguration.Default with
        {
            WeatherBeaconEnabled = true,
            AprsIsWeatherTransmitEnabled = true,
            SelectedWeatherSourceDriverId = "manual-wx"
        };
    }

    private static (
        LocalStationProfileService ProfileService,
        FakeAprsIsClient AprsIsClient,
        FakeRfBeaconTransmitClient RfClient,
        WeatherBeaconScheduler Scheduler,
        FakeBeaconSchedulerClock Clock) CreateScheduler(
            WeatherBeaconConfiguration? configuration = null,
            FakeWeatherObservationSourceProvider? provider = null)
    {
        var profileService = new LocalStationProfileService(Now);
        var aprsIsClient = new FakeAprsIsClient();
        var rfClient = new FakeRfBeaconTransmitClient();
        var clock = new FakeBeaconSchedulerClock { UtcNow = Now };
        provider ??= new FakeWeatherObservationSourceProvider("manual-wx", CreateObservation());
        var scheduler = new WeatherBeaconScheduler(
            profileService,
            new AprsWeatherFormatter(new WeatherObservationValidator(TimeSpan.FromMinutes(15))),
            provider,
            aprsIsClient,
            configuration,
            clock,
            rfClient);

        return (profileService, aprsIsClient, rfClient, scheduler, clock);
    }

    private static LocalStationProfile CreateValidProfile(
        bool transmitEnabled = false,
        bool aprsIsTransmitEnabled = false,
        bool rfTransmitEnabled = false)
    {
        return LocalStationProfile.CreateDefault(Now) with
        {
            Callsign = "KD8ABC",
            Ssid = 7,
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333,
            SymbolTableIdentifier = '/',
            SymbolCode = '-',
            BeaconPath = "WIDE1-1",
            TransmitEnabled = transmitEnabled,
            AprsIsTransmitEnabled = aprsIsTransmitEnabled,
            RfTransmitEnabled = rfTransmitEnabled
        };
    }

    private static CommonWeatherObservation CreateObservation(
        DateTimeOffset? timestamp = null,
        int? humidity = 50)
    {
        return new CommonWeatherObservation(
            "Manual Weather",
            WeatherObservationSourceType.Manual,
            "manual-station",
            "N0CALL",
            timestamp ?? Now,
            39.058333,
            -84.508333,
            180,
            5,
            10,
            72,
            0,
            0,
            0,
            humidity,
            1013.2,
            LuminosityWattsPerSquareMeter: null,
            UvIndex: null,
            SnowInches: null,
            LightningCount: null,
            LightningDistanceMiles: null,
            Diagnostics: new Dictionary<string, string> { ["driver"] = "manual-wx" },
            RawSourcePayload: "manual payload",
            WeatherDataState.Current,
            ValidationErrors: [],
            ValidationWarnings: []);
    }

    private sealed class FakeWeatherObservationSourceProvider : IWeatherObservationSourceProvider
    {
        private readonly string driverId;
        private readonly CommonWeatherObservation observation;

        public FakeWeatherObservationSourceProvider(string driverId, CommonWeatherObservation observation)
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

    private sealed class FakeBeaconSchedulerClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeRfBeaconTransmitClient : IRfBeaconTransmitClient
    {
        public int SendCallCount { get; private set; }

        public string? LastPacket { get; private set; }

        public Task<BeaconNowResult> SendBeaconAsync(string rawPacket, CancellationToken cancellationToken)
        {
            SendCallCount++;
            LastPacket = rawPacket;
            return Task.FromResult(new BeaconNowResult(
                PacketGenerated: true,
                TransmitAttempted: true,
                Transmitted: true,
                Blocked: false,
                Packet: rawPacket,
                Message: "RF beacon transmitted.",
                TransmitResult: null,
                ValidationErrors: []));
        }
    }

    private sealed class FakeAprsIsClient : IAprsIsClient
    {
        public event EventHandler<AprsIsRawPacketReceivedEventArgs>? RawPacketReceived
        {
            add { }
            remove { }
        }

        public AprsIsConnectionState State { get; set; } = AprsIsConnectionState.Disconnected;

        public Exception? LastError => null;

        public int SendCallCount { get; private set; }

        public string? LastPacket { get; private set; }

        public bool? LastTransmitConfirmed { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            State = AprsIsConnectionState.Connected;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            State = AprsIsConnectionState.Disconnected;
            return Task.CompletedTask;
        }

        public Task<AprsIsTransmitResult> SendRawPacketAsync(
            string rawPacketLine,
            bool transmitConfirmed,
            CancellationToken cancellationToken)
        {
            SendCallCount++;
            LastPacket = rawPacketLine;
            LastTransmitConfirmed = transmitConfirmed;
            return Task.FromResult(AprsIsTransmitResult.Succeeded(Now, rawPacketLine, State));
        }

        public async IAsyncEnumerable<AprsIsRawPacketReceivedEventArgs> ReadPacketsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
