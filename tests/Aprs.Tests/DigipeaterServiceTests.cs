using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class DigipeaterServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
    private readonly AprsParser parser = new();

    [Fact]
    public async Task DefaultConfiguration_BlocksDigipeaterAndRfTransmit()
    {
        var service = new DigipeaterService(CreatePortManager(connectedTransmitPort: true), new FakeRfBeaconTransmitClient());

        var decision = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.TransmitDisabled, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Contains("disabled", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RfTransmitDisabled_BlocksBeforeFakeRfTransmit()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter, EnabledConfiguration() with { RfTransmitEnabled = false });

        var decision = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.TransmitDisabled, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, transmitter.SendCallCount);
    }

    [Fact]
    public async Task NonRfPacket_IsBlocked()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter);

        var decision = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.AprsIs, "APRS-IS");

        Assert.Equal(DigipeaterDecision.Blocked, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, transmitter.SendCallCount);
    }

    [Fact]
    public async Task MalformedPacket_IsInvalid()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter);

        var decision = await service.EvaluateAndDigipeatAsync(Parse("BADPACKETWITHOUTSEPARATOR"), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.Invalid, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.NotEmpty(decision.ValidationErrors);
    }

    [Fact]
    public async Task PacketWithoutMatchingAlias_IsBlocked()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter);

        var decision = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,NOGATE:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.NoMatchingAlias, decision.Decision);
        Assert.False(decision.TransmitAttempted);
    }

    [Fact]
    public async Task MatchingWideOneAlias_IsAllowedOnlyAfterSafetyChecksPass()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter);

        var decision = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.Allowed, decision.Decision);
        Assert.True(decision.TransmitAttempted);
        Assert.Equal(1, transmitter.SendCallCount);
        Assert.Equal("MOBILE1>APRS,WIDE1-1*:!3903.50N/08430.50W>Mobile", transmitter.LastPacket);
        Assert.Equal(["WIDE1-1*"], decision.ModifiedPath);
    }

    [Fact]
    public async Task WideTwoPath_DecrementsWhenFullDigipeaterModeEnabled()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter, EnabledConfiguration() with
        {
            FullDigipeaterMode = true,
            SupportedAliases = ["WIDE1-1", "WIDE2-1", "MYDIGI"]
        });

        var decision = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE2-2:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.Allowed, decision.Decision);
        Assert.Equal(["MYDIGI*", "WIDE2-1"], decision.ModifiedPath);
        Assert.Equal("MOBILE1>APRS,MYDIGI*,WIDE2-1:!3903.50N/08430.50W>Mobile", decision.ModifiedPacket);
    }

    [Fact]
    public async Task DuplicatePacket_IsBlocked()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter);

        var first = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", Now), AprsPacketSource.Rf, "RF");
        var second = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", Now.AddMinutes(1)), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.Allowed, first.Decision);
        Assert.Equal(DigipeaterDecision.Duplicate, second.Decision);
        Assert.False(second.TransmitAttempted);
        Assert.Equal(1, transmitter.SendCallCount);
    }

    [Fact]
    public async Task DisconnectedRfTransmitPort_BlocksDigipeating()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = new DigipeaterService(
            CreatePortManager(connectedTransmitPort: false),
            transmitter,
            EnabledConfiguration(),
            new FakeBeaconSchedulerClock { UtcNow = Now });

        var decision = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.TransmitDisabled, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, transmitter.SendCallCount);
        Assert.Contains("not connected", decision.Reason);
    }

    [Fact]
    public async Task RateLimit_BlocksExcessivePackets()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter, EnabledConfiguration() with
        {
            MaximumDigipeatsPerMinute = 1,
            MaximumDigipeatsPerStationPerMinute = 1
        });

        var first = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>One", Now), AprsPacketSource.Rf, "RF");
        var second = await service.EvaluateAndDigipeatAsync(Parse("MOBILE2>APRS,WIDE1-1:!3904.50N/08431.50W>Two", Now.AddSeconds(20)), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.Allowed, first.Decision);
        Assert.Equal(DigipeaterDecision.RateLimited, second.Decision);
        Assert.False(second.TransmitAttempted);
        Assert.Equal(1, transmitter.SendCallCount);
    }

    [Fact]
    public async Task BlockedCallsign_IsBlocked()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter, EnabledConfiguration() with { BlockedCallsigns = ["MOBILE1"] });

        var decision = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.Blocked, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, transmitter.SendCallCount);
    }

    [Fact]
    public async Task BlockedPathPattern_IsBlocked()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter, EnabledConfiguration() with { BlockedPathPatterns = ["WIDE1-1"] });

        var decision = await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.Blocked, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, transmitter.SendCallCount);
    }

    [Fact]
    public async Task DecisionsAreLoggedAndCanBeCleared()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = CreateService(transmitter);

        await service.EvaluateAndDigipeatAsync(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF");

        Assert.Single(service.GetRecentDecisions());
        Assert.Equal(1, service.GetStatusSummary().AllowedCount);

        service.ClearDecisionHistory();

        Assert.Empty(service.GetRecentDecisions());
        Assert.Equal(0, service.GetStatusSummary().AllowedCount);
    }

    [Fact]
    public async Task TransmitAuthorityInhibited_BlocksDigipeatEvenWhenPortIsReady()
    {
        var transmitter = new FakeRfBeaconTransmitClient();
        var service = new DigipeaterService(
            CreatePortManager(connectedTransmitPort: true),
            transmitter,
            EnabledConfiguration(),
            new FakeBeaconSchedulerClock { UtcNow = Now },
            transmitSafety: new StubInhibitedAuthority());

        var decision = await service.EvaluateAndDigipeatAsync(
            Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), AprsPacketSource.Rf, "RF");

        Assert.Equal(DigipeaterDecision.TransmitDisabled, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, transmitter.SendCallCount);
        Assert.Contains("inhibit", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubInhibitedAuthority : ITransmitSafetyAuthority
    {
        public bool IsInhibited => true;
        public string? InhibitReason => "exercise mode";
        public void Inhibit(string reason) { }
        public void Release() { }
        public TransmitDecision Evaluate(TransmitRequest request)
            => TransmitDecision.Deny(TransmitDenyReason.GlobalInhibit, "Transmit inhibited (exercise mode).");
    }

    private DigipeaterService CreateService(FakeRfBeaconTransmitClient transmitter, DigipeaterConfiguration? configuration = null)
    {
        return new DigipeaterService(
            CreatePortManager(connectedTransmitPort: true),
            transmitter,
            configuration ?? EnabledConfiguration(),
            new FakeBeaconSchedulerClock { UtcNow = Now });
    }

    private AprsPacket Parse(string rawLine, DateTimeOffset? receivedAtUtc = null)
    {
        return parser.Parse(rawLine, receivedAtUtc ?? Now);
    }

    private static DigipeaterConfiguration EnabledConfiguration()
    {
        return DigipeaterConfiguration.Default with
        {
            DigipeaterEnabled = true,
            RfTransmitEnabled = true,
            AllowedRfReceivePorts = ["RF"],
            RfTransmitPort = "RF-TX",
            DigipeaterCallsign = "MYDIGI",
            SupportedAliases = ["WIDE1-1", "WIDE2-1", "MYDIGI"],
            FillInDigipeaterMode = true,
            BlockedPathPatterns = []
        };
    }

    private static AprsPortManager CreatePortManager(bool connectedTransmitPort)
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(AprsPortManager.CreateDefaultPort("RF-TX", "RF TX", AprsPortType.TcpKiss, "Fake RF transmit") with
        {
            Enabled = true,
            ReceiveEnabled = true,
            TransmitEnabled = true,
            ConnectionState = connectedTransmitPort ? AprsPortConnectionState.Connected : AprsPortConnectionState.Disconnected
        });

        return manager;
    }

    private sealed class FakeBeaconSchedulerClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeRfBeaconTransmitClient : IRfBeaconTransmitClient
    {
        public int SendCallCount { get; private set; }

        public string? LastPacket { get; private set; }

        public Func<string, BeaconNowResult>? SendResultFactory { get; set; }

        public Task<BeaconNowResult> SendBeaconAsync(string rawPacket, CancellationToken cancellationToken)
        {
            SendCallCount++;
            LastPacket = rawPacket;

            var result = SendResultFactory?.Invoke(rawPacket)
                ?? new BeaconNowResult(
                    PacketGenerated: true,
                    TransmitAttempted: true,
                    Transmitted: true,
                    Blocked: false,
                    Packet: rawPacket,
                    Message: "Fake RF transmit accepted packet.",
                    TransmitResult: null,
                    ValidationErrors: []);
            return Task.FromResult(result);
        }
    }
}
