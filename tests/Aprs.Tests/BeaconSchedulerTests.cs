using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class BeaconSchedulerTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultScheduler_IsDisabled()
    {
        var (_, _, scheduler, _) = CreateScheduler();

        var state = scheduler.GetState();

        Assert.False(state.SchedulerEnabled);
        Assert.False(state.AprsIsBeaconEnabled);
        Assert.False(state.RfBeaconEnabled);
        Assert.Null(state.NextAprsIsBeaconTimeUtc);
        Assert.Null(state.NextRfBeaconTimeUtc);
    }

    [Fact]
    public async Task BeaconNow_GeneratesPacketWhenProfileIsValidButTransmitDisabled()
    {
        var (profileService, _, scheduler, _) = CreateScheduler(aprsIsBeaconEnabled: true);
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: false, aprsIsTransmitEnabled: false), TestNow);
        scheduler.Start();

        var result = await scheduler.BeaconNowAsync(CancellationToken.None);

        Assert.True(result.PacketGenerated);
        Assert.True(result.Blocked);
        Assert.False(result.TransmitAttempted);
        Assert.Equal("KD8ABC-7>APCMD0,WIDE1-1:!3903.50N/08430.50W-Test scheduler beacon", result.Packet);
        Assert.Contains("Transmit is disabled", result.Message);
    }

    [Fact]
    public async Task BeaconNow_WhenTransmitDisabled_BlocksWithoutCallingAprsIsClient()
    {
        var (profileService, client, scheduler, _) = CreateScheduler(aprsIsBeaconEnabled: true);
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: false, aprsIsTransmitEnabled: false), TestNow);
        client.State = AprsIsConnectionState.Connected;
        scheduler.Start();

        var result = await scheduler.BeaconNowAsync(CancellationToken.None);

        Assert.True(result.Blocked);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains(result.ValidationErrors, error => error.Contains("Transmit is disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BeaconNow_WhenReceiveOnlyModeBlocksTransmit_ReturnsBlockedResult()
    {
        var (profileService, client, scheduler, _) = CreateScheduler(aprsIsBeaconEnabled: true);
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: true, aprsIsTransmitEnabled: true), TestNow);
        client.State = AprsIsConnectionState.Connected;
        client.SendResultFactory = packet => AprsIsTransmitResult.Failed(
            TestNow,
            packet,
            AprsIsConnectionState.Connected,
            "APRS-IS client is in receive-only mode.");
        scheduler.Start();

        var result = await scheduler.BeaconNowAsync(CancellationToken.None);

        Assert.True(result.PacketGenerated);
        Assert.True(result.TransmitAttempted);
        Assert.True(result.Blocked);
        Assert.False(result.Transmitted);
        Assert.Equal(1, client.SendCallCount);
        Assert.Contains("receive-only", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BeaconNow_WhenProfileIsInvalid_BlocksBeforeFormattingOrTransmit()
    {
        var (profileService, client, scheduler, _) = CreateScheduler(aprsIsBeaconEnabled: true);
        profileService.UpdateProfile(CreateValidProfile() with { Callsign = string.Empty }, TestNow);
        client.State = AprsIsConnectionState.Connected;
        scheduler.Start();

        var result = await scheduler.BeaconNowAsync(CancellationToken.None);

        Assert.False(result.PacketGenerated);
        Assert.True(result.Blocked);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains(result.ValidationErrors, error => error.Contains("valid callsign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task BeaconNow_WhenIntervalIsShorterThanMinimum_Blocks()
    {
        var (profileService, client, scheduler, _) = CreateScheduler(aprsIsBeaconEnabled: true);
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: true, aprsIsTransmitEnabled: true) with
        {
            AprsIsBeaconInterval = TimeSpan.FromMinutes(1)
        }, TestNow);
        client.State = AprsIsConnectionState.Connected;
        scheduler.Start();

        var result = await scheduler.BeaconNowAsync(CancellationToken.None);

        Assert.True(result.Blocked);
        Assert.False(result.PacketGenerated);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("minimum", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Start_CalculatesNextBeaconTime()
    {
        var (profileService, _, scheduler, _) = CreateScheduler(aprsIsBeaconEnabled: true);
        profileService.UpdateProfile(CreateValidProfile() with
        {
            AprsIsBeaconInterval = TimeSpan.FromMinutes(30)
        }, TestNow);

        var state = scheduler.Start();

        Assert.True(state.SchedulerEnabled);
        Assert.Equal(TestNow.AddMinutes(30), state.NextAprsIsBeaconTimeUtc);
    }

    [Fact]
    public async Task TickAsync_WhenSchedulerStopped_DoesNotTransmit()
    {
        var (profileService, client, scheduler, clock) = CreateScheduler(aprsIsBeaconEnabled: true);
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: true, aprsIsTransmitEnabled: true), TestNow);
        client.State = AprsIsConnectionState.Connected;
        scheduler.Start();
        scheduler.Stop();
        clock.UtcNow = TestNow.AddHours(1);

        var result = await scheduler.TickAsync(CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, client.SendCallCount);
    }

    [Fact]
    public async Task BeaconNow_WhenExplicitlyEnabledAndConnected_CallsAprsIsTransmit()
    {
        var (profileService, client, scheduler, _) = CreateScheduler(aprsIsBeaconEnabled: true);
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: true, aprsIsTransmitEnabled: true), TestNow);
        client.State = AprsIsConnectionState.Connected;
        scheduler.Start();

        var result = await scheduler.BeaconNowAsync(CancellationToken.None);

        Assert.True(result.PacketGenerated);
        Assert.True(result.TransmitAttempted);
        Assert.True(result.Transmitted);
        Assert.False(result.Blocked);
        Assert.Equal(1, client.SendCallCount);
        Assert.True(client.LastTransmitConfirmed);
        Assert.Equal("KD8ABC-7>APCMD0,WIDE1-1:!3903.50N/08430.50W-Test scheduler beacon", client.LastPacket);
    }

    [Fact]
    public async Task BeaconNow_WhenAprsIsBeaconingDisabled_BlocksWithoutTransmit()
    {
        var (profileService, client, scheduler, _) = CreateScheduler(aprsIsBeaconEnabled: false);
        profileService.UpdateProfile(CreateValidProfile(transmitEnabled: true, aprsIsTransmitEnabled: true), TestNow);
        client.State = AprsIsConnectionState.Connected;
        scheduler.Start();

        var result = await scheduler.BeaconNowAsync(CancellationToken.None);

        Assert.True(result.Blocked);
        Assert.False(result.PacketGenerated);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("APRS-IS beaconing is disabled", result.Message);
    }

    private static (LocalStationProfileService ProfileService, FakeAprsIsClient Client, BeaconScheduler Scheduler, FakeBeaconSchedulerClock Clock) CreateScheduler(
        bool aprsIsBeaconEnabled = false,
        bool rfBeaconEnabled = false)
    {
        var profileService = new LocalStationProfileService(TestNow);
        var client = new FakeAprsIsClient();
        var clock = new FakeBeaconSchedulerClock { UtcNow = TestNow };
        var configuration = BeaconSchedulerConfiguration.Default with
        {
            AprsIsBeaconEnabled = aprsIsBeaconEnabled,
            RfBeaconEnabled = rfBeaconEnabled
        };
        var scheduler = new BeaconScheduler(
            profileService,
            new AprsBeaconFormatter(),
            client,
            configuration,
            clock);

        return (profileService, client, scheduler, clock);
    }

    private static LocalStationProfile CreateValidProfile(bool transmitEnabled = false, bool aprsIsTransmitEnabled = false)
    {
        return LocalStationProfile.CreateDefault(TestNow) with
        {
            Callsign = "KD8ABC",
            Ssid = 7,
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333,
            SymbolTableIdentifier = '/',
            SymbolCode = '-',
            StationComment = "Test scheduler beacon",
            BeaconPath = "WIDE1-1",
            AprsIsBeaconInterval = TimeSpan.FromMinutes(30),
            TransmitEnabled = transmitEnabled,
            AprsIsTransmitEnabled = aprsIsTransmitEnabled
        };
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

        public AprsIsConnectionState State { get; set; } = AprsIsConnectionState.Disconnected;

        public Exception? LastError => null;

        public int SendCallCount { get; private set; }

        public string? LastPacket { get; private set; }

        public bool? LastTransmitConfirmed { get; private set; }

        public Func<string, AprsIsTransmitResult>? SendResultFactory { get; set; }

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

            var result = SendResultFactory?.Invoke(rawPacketLine)
                ?? AprsIsTransmitResult.Succeeded(TestNow, rawPacketLine, State);
            return Task.FromResult(result);
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
