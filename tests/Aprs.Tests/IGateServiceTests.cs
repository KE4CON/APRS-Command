using Aprs.Core;
using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class IGateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
    private readonly AprsParser parser = new();

    [Fact]
    public async Task DefaultConfiguration_BlocksGatingAndDoesNotTransmit()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var service = new IGateService(client);

        var decision = await service.EvaluateAndGateAsync(CreateCandidate("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"));

        Assert.Equal(IGateDecision.TransmitDisabled, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("disabled", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RfToAprsIsDisabled_BlocksBeforeTransmit()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var service = CreateService(client, EnabledConfiguration() with { RfToAprsIsGatingEnabled = false });

        var decision = await service.EvaluateAndGateAsync(CreateCandidate("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"));

        Assert.Equal(IGateDecision.TransmitDisabled, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, client.SendCallCount);
    }

    [Fact]
    public async Task NonRfCandidate_IsBlocked()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var service = CreateService(client);
        var candidate = CreateCandidate(
            "MOBILE1>APRS:!3903.50N/08430.50W>Mobile",
            packetSource: AprsPacketSource.AprsIs,
            sourcePort: "APRS-IS");

        var decision = await service.EvaluateAndGateAsync(candidate);

        Assert.Equal(IGateDecision.Blocked, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("RF source", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MalformedCandidate_IsInvalidAndDoesNotTransmit()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var service = CreateService(client);

        var decision = await service.EvaluateAndGateAsync(CreateCandidate("BADPACKETWITHOUTSEPARATOR"));

        Assert.Equal(IGateDecision.Invalid, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.NotEmpty(decision.ValidationErrors);
        Assert.Equal(0, client.SendCallCount);
    }

    [Fact]
    public async Task DuplicateCandidate_IsBlocked()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var service = CreateService(client);
        var candidate = CreateCandidate("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile") with
        {
            CandidateState = IGateCandidateState.Duplicate,
            WasAlsoSeenOnAprsIs = true
        };

        var decision = await service.EvaluateAndGateAsync(candidate);

        Assert.Equal(IGateDecision.Duplicate, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, client.SendCallCount);
    }

    [Fact]
    public async Task DisconnectedAprsIsClient_BlocksGating()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Disconnected };
        var service = CreateService(client);

        var decision = await service.EvaluateAndGateAsync(CreateCandidate("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"));

        Assert.Equal(IGateDecision.AprsIsDisconnected, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, client.SendCallCount);
    }

    [Fact]
    public async Task ValidRfCandidate_GatesOnlyWhenSafetyChecksPass()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var service = CreateService(client);

        var decision = await service.EvaluateAndGateAsync(CreateCandidate("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"));

        Assert.Equal(IGateDecision.Allowed, decision.Decision);
        Assert.True(decision.TransmitAttempted);
        Assert.True(decision.TransmitResult?.IsSuccess);
        Assert.Equal(1, client.SendCallCount);
        Assert.Equal("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", client.LastPacket);
        Assert.True(client.LastTransmitConfirmed);
    }

    [Fact]
    public async Task AprsIsTransmitRejection_IsRecordedAsError()
    {
        var client = new FakeAprsIsClient
        {
            State = AprsIsConnectionState.Connected,
            SendResultFactory = packet => AprsIsTransmitResult.Failed(Now, packet, AprsIsConnectionState.Connected, "APRS-IS transmit disabled.")
        };
        var service = CreateService(client);

        var decision = await service.EvaluateAndGateAsync(CreateCandidate("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"));

        Assert.Equal(IGateDecision.Error, decision.Decision);
        Assert.True(decision.TransmitAttempted);
        Assert.Equal(1, client.SendCallCount);
        Assert.Contains("transmit disabled", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AprsIsTransmitDisabled_BlocksBeforeTransportCall()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var service = CreateService(client, EnabledConfiguration() with { AprsIsTransmitEnabled = false });

        var decision = await service.EvaluateAndGateAsync(CreateCandidate("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"));

        Assert.Equal(IGateDecision.TransmitDisabled, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("APRS-IS transmit", decision.Reason);
    }

    [Fact]
    public async Task BlockedPathPattern_PreventsTransmit()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var service = CreateService(client, EnabledConfiguration() with { BlockedPathPatterns = ["WIDE1-1"] });

        var decision = await service.EvaluateAndGateAsync(CreateCandidate("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"));

        Assert.Equal(IGateDecision.Blocked, decision.Decision);
        Assert.False(decision.TransmitAttempted);
        Assert.Equal(0, client.SendCallCount);
        Assert.Contains("path", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RateLimit_PreventsExcessiveGating()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var service = CreateService(client, EnabledConfiguration() with
        {
            MaximumGateRatePerMinute = 1,
            MaximumGateRatePerStationPerMinute = 1
        });

        var first = await service.EvaluateAndGateAsync(CreateCandidate("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", Now));
        var second = await service.EvaluateAndGateAsync(CreateCandidate("MOBILE2>APRS,WIDE1-1:!3904.50N/08431.50W>Mobile", Now.AddSeconds(20)));

        Assert.Equal(IGateDecision.Allowed, first.Decision);
        Assert.Equal(IGateDecision.RateLimited, second.Decision);
        Assert.False(second.TransmitAttempted);
        Assert.Equal(1, client.SendCallCount);
    }

    [Fact]
    public async Task DecisionsAreLoggedAndCanBeCleared()
    {
        var client = new FakeAprsIsClient { State = AprsIsConnectionState.Connected };
        var service = CreateService(client);

        await service.EvaluateAndGateAsync(CreateCandidate("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"));
        var summary = service.GetStatusSummary();

        Assert.Single(service.GetRecentDecisions());
        Assert.Equal(1, summary.AllowedCount);
        Assert.Equal("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", summary.LastGatedPacket);

        service.ClearDecisionHistory();

        Assert.Empty(service.GetRecentDecisions());
        Assert.Equal(0, service.GetStatusSummary().AllowedCount);
    }

    private IGateService CreateService(FakeAprsIsClient client, IGateConfiguration? configuration = null)
    {
        return new IGateService(client, configuration ?? EnabledConfiguration(), new FakeBeaconSchedulerClock { UtcNow = Now });
    }

    private IGateCandidatePacket CreateCandidate(
        string rawLine,
        DateTimeOffset? receivedAtUtc = null,
        AprsPacketSource packetSource = AprsPacketSource.Rf,
        string sourcePort = "RF")
    {
        var timestamp = receivedAtUtc ?? Now;
        var monitor = new IGateMonitorService(EnabledMonitorConfiguration());
        return monitor.AcceptPacket(parser.Parse(rawLine, timestamp), packetSource, sourcePort, timestamp);
    }

    private static IGateConfiguration EnabledConfiguration()
    {
        return IGateConfiguration.Default with
        {
            IGateEnabled = true,
            RfToAprsIsGatingEnabled = true,
            AprsIsTransmitEnabled = true,
            AllowedRfSourcePorts = ["RF"],
            BlockedPathPatterns = [],
            RequireExplicitConfirmationBeforeEnabling = true
        };
    }

    private static IGateMonitorConfiguration EnabledMonitorConfiguration()
    {
        return IGateMonitorConfiguration.Default with
        {
            MonitorEnabled = true,
            RfToAprsIsCandidateDetectionEnabled = true,
            LocalRfPortsToMonitor = ["RF"]
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
                ?? AprsIsTransmitResult.Succeeded(Now, rawPacketLine, State);
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
