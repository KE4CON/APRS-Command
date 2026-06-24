using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class SmartBeaconingDecisionServiceTests
{
    private static readonly DateTimeOffset StartTime = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultConfiguration_DisablesSmartBeaconingAndRf()
    {
        var configuration = SmartBeaconingConfiguration.Default;
        var service = new SmartBeaconingDecisionService();

        var decision = service.Evaluate(CreatePosition(StartTime));

        Assert.False(configuration.Enabled);
        Assert.False(configuration.EnabledForAprsIs);
        Assert.False(configuration.EnabledForRf);
        Assert.False(decision.ShouldBeacon);
        Assert.Contains("disabled", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_InvalidGpsFixBlocksBeacon()
    {
        var service = new SmartBeaconingDecisionService(CreateEnabledConfiguration());

        var decision = service.Evaluate(CreatePosition(StartTime, fixValid: false));

        Assert.False(decision.ShouldBeacon);
        Assert.Contains("not valid", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(decision.ValidationMessages, message => message.Contains("GPS fix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_FirstValidPositionCreatesInitialBeaconDecision()
    {
        var service = new SmartBeaconingDecisionService(CreateEnabledConfiguration());

        var decision = service.Evaluate(CreatePosition(StartTime));

        Assert.True(decision.ShouldBeacon);
        Assert.Equal("Initial valid mobile position.", decision.Reason);
        Assert.Equal(StartTime, decision.NextRecommendedBeaconTimeUtc);
        Assert.Null(decision.ElapsedSinceLastBeacon);
    }

    [Fact]
    public void Evaluate_LowSpeedUsesSlowerInterval()
    {
        var service = new SmartBeaconingDecisionService(CreateEnabledConfiguration());
        service.RecordBeacon(CreatePosition(StartTime, speedKnots: 2, courseDegrees: 0));

        var early = service.Evaluate(CreatePosition(StartTime.AddMinutes(20), speedKnots: 2, courseDegrees: 0));
        var due = service.Evaluate(CreatePosition(StartTime.AddMinutes(30), speedKnots: 2, courseDegrees: 0));

        Assert.False(early.ShouldBeacon);
        Assert.Equal(StartTime.AddMinutes(30), early.NextRecommendedBeaconTimeUtc);
        Assert.True(due.ShouldBeacon);
        Assert.Contains("interval", due.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_HighSpeedUsesFasterInterval()
    {
        var service = new SmartBeaconingDecisionService(CreateEnabledConfiguration());
        service.RecordBeacon(CreatePosition(StartTime, speedKnots: 70, courseDegrees: 0));

        var decision = service.Evaluate(CreatePosition(StartTime.AddMinutes(5), speedKnots: 70, courseDegrees: 0));

        Assert.True(decision.ShouldBeacon);
        Assert.Contains("Speed-based", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TimeSpan.FromMinutes(5), decision.ElapsedSinceLastBeacon);
    }

    [Fact]
    public void Evaluate_HeadingChangeCanTriggerBeacon()
    {
        var service = new SmartBeaconingDecisionService(CreateEnabledConfiguration());
        service.RecordBeacon(CreatePosition(StartTime, speedKnots: 30, courseDegrees: 0));

        var decision = service.Evaluate(CreatePosition(StartTime.AddMinutes(5), speedKnots: 30, courseDegrees: 90));

        Assert.True(decision.ShouldBeacon);
        Assert.Contains("Heading change", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(90, decision.TurnAngleDegrees);
    }

    [Fact]
    public void Evaluate_MinimumIntervalPreventsExcessiveBeaconing()
    {
        var service = new SmartBeaconingDecisionService(CreateEnabledConfiguration());
        service.RecordBeacon(CreatePosition(StartTime, speedKnots: 80, courseDegrees: 0));

        var decision = service.Evaluate(CreatePosition(StartTime.AddMinutes(1), speedKnots: 80, courseDegrees: 180));

        Assert.False(decision.ShouldBeacon);
        Assert.Contains("Minimum beacon interval", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(StartTime.AddMinutes(5), decision.NextRecommendedBeaconTimeUtc);
    }

    [Fact]
    public void Evaluate_MaximumIntervalEventuallyAllowsBeaconing()
    {
        var service = new SmartBeaconingDecisionService(CreateEnabledConfiguration());
        service.RecordBeacon(CreatePosition(StartTime, speedKnots: 1, courseDegrees: 0));

        var decision = service.Evaluate(CreatePosition(StartTime.AddMinutes(31), speedKnots: 1, courseDegrees: 0));

        Assert.True(decision.ShouldBeacon);
        Assert.Contains("Maximum beacon interval", decision.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(TimeSpan.FromMinutes(31), decision.ElapsedSinceLastBeacon);
    }

    [Fact]
    public void Evaluate_ResultIncludesClearReasonAndCurrentMotion()
    {
        var service = new SmartBeaconingDecisionService(CreateEnabledConfiguration());
        service.RecordBeacon(CreatePosition(StartTime, speedKnots: 10, courseDegrees: 45));

        var decision = service.Evaluate(CreatePosition(StartTime.AddMinutes(10), speedKnots: 12, courseDegrees: 50));

        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
        Assert.Equal(12, decision.CurrentSpeedKnots);
        Assert.Equal(50, decision.CurrentCourseDegrees);
        Assert.NotNull(decision.ElapsedSinceLastBeacon);
    }

    [Fact]
    public void BeaconScheduler_CanEvaluateSmartBeaconingWithoutTransmit()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var scheduler = new BeaconScheduler(
            new LocalStationProfileService(StartTime),
            new AprsBeaconFormatter(),
            client);

        var decision = scheduler.EvaluateSmartBeaconing(CreatePosition(StartTime));

        Assert.False(decision.ShouldBeacon);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("disabled", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private static SmartBeaconingConfiguration CreateEnabledConfiguration()
    {
        return SmartBeaconingConfiguration.Default with
        {
            Enabled = true,
            EnabledForAprsIs = true,
            EnabledForRf = false,
            LowSpeedThresholdKnots = 5,
            HighSpeedThresholdKnots = 60,
            SlowRateInterval = TimeSpan.FromMinutes(30),
            FastRateInterval = TimeSpan.FromMinutes(5),
            MinimumTurnAngleDegrees = 30,
            TurnSlope = 240,
            TurnTimeMinimum = TimeSpan.FromSeconds(30),
            TurnTimeMaximum = TimeSpan.FromMinutes(5),
            MinimumBeaconInterval = TimeSpan.FromMinutes(5),
            MaximumBeaconInterval = TimeSpan.FromMinutes(30)
        };
    }

    private static MobilePositionInput CreatePosition(
        DateTimeOffset timestampUtc,
        double latitude = 39.058333,
        double longitude = -84.508333,
        double? speedKnots = 10,
        double? courseDegrees = 0,
        int? altitudeFeet = null,
        bool fixValid = true,
        MobilePositionSource source = MobilePositionSource.Simulation)
    {
        return new MobilePositionInput(
            latitude,
            longitude,
            timestampUtc,
            speedKnots,
            courseDegrees,
            altitudeFeet,
            fixValid,
            source);
    }

    private sealed class FakeAprsIsClient : IAprsIsClient
    {
        public event EventHandler<AprsIsRawPacketReceivedEventArgs>? RawPacketReceived
        {
            add { }
            remove { }
        }

        public AprsIsConnectionState State { get; init; } = AprsIsConnectionState.Disconnected;

        public Exception? LastError => null;

        public int SendCallCount { get; private set; }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<AprsIsTransmitResult> SendRawPacketAsync(
            string rawPacketLine,
            bool transmitConfirmed,
            CancellationToken cancellationToken)
        {
            SendCallCount++;
            return Task.FromResult(AprsIsTransmitResult.Succeeded(StartTime, rawPacketLine, State));
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
