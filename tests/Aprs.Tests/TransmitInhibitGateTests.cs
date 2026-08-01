using Aprs.Desktop.Runtime;
using Aprs.Services;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Verifies the global transmit-inhibit gate (exercise/training mode) is enforced at the transport
/// transmit chokepoints, so <em>every</em> path — beacon, object, message, iGate, weather, RF — is
/// hard-blocked while inhibited, not just the paths that remember to call
/// <see cref="ITransmitSafetyAuthority.Evaluate"/>.
/// </summary>
public sealed class TransmitInhibitGateTests
{
    private sealed class FakeInhibitGate : ITransmitInhibitGate
    {
        public bool IsTransmitInhibited { get; set; }
        public string? InhibitReason { get; set; }
    }

    private static AprsIsClient TransmitReadyClient(ITransmitInhibitGate gate)
    {
        var configuration = AprsIsClientConfiguration.Default with
        {
            Callsign = "N0CALL",
            Passcode = "12345",
            ApplicationVersion = "1.2.3",
            ReconnectEnabled = false,
            ReceiveOnly = false,
            TransmitEnabled = true,
            RequireTransmitConfirmation = false,
            DefaultTransmitSource = "N0CALL"
        };

        // Stream factory is never used: the gate check precedes any connection attempt.
        return new AprsIsClient(configuration, (_, _) => Task.FromResult<Stream>(new MemoryStream()))
        {
            InhibitGate = gate
        };
    }

    [Fact]
    public async Task AprsIsClient_WhenGateInhibited_BlocksTransmitWithReason()
    {
        var gate = new FakeInhibitGate { IsTransmitInhibited = true, InhibitReason = "Exercise mode — all transmit inhibited" };
        var client = TransmitReadyClient(gate);

        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Exercise mode — all transmit inhibited", result.FailureReason);
    }

    [Fact]
    public async Task AprsIsClient_WhenGateNotInhibited_FallsThroughToNormalValidation()
    {
        // Not inhibited but disconnected — the transmit must still be blocked, but by the ordinary
        // "not connected" check rather than the inhibit gate, proving the gate is not over-blocking.
        var gate = new FakeInhibitGate { IsTransmitInhibited = false };
        var client = TransmitReadyClient(gate);

        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not connected", result.FailureReason);
    }

    [Fact]
    public async Task RfBeaconClient_WhenGateInhibited_BlocksTransmit()
    {
        var gate = new FakeInhibitGate { IsTransmitInhibited = true, InhibitReason = "Exercise mode — all transmit inhibited" };
        bool anyClientQueried = false;
        var client = new KissRfBeaconTransmitClient
        {
            InhibitGate = gate,
            GetTcpClients = () => { anyClientQueried = true; return Array.Empty<TcpKissClient>(); },
            GetSerialClients = () => { anyClientQueried = true; return Array.Empty<SerialKissClient>(); }
        };

        var result = await client.SendBeaconAsync("N0CALL>APRS:!0000.00N/00000.00W>test", CancellationToken.None);

        Assert.False(result.Transmitted);
        Assert.Contains("inhibited", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(anyClientQueried); // blocked before touching any RF port
    }

    [Fact]
    public void TransmitSafetyAuthority_IsUsableAsInhibitGate()
    {
        var authority = new TransmitSafetyAuthority(new AprsPortManager(), new FakePolicy());
        var gate = (ITransmitInhibitGate)authority;

        Assert.False(gate.IsTransmitInhibited);

        authority.Inhibit("Exercise mode");
        Assert.True(gate.IsTransmitInhibited);
        Assert.Equal("Exercise mode", gate.InhibitReason);

        authority.Release();
        Assert.False(gate.IsTransmitInhibited);
    }

    private sealed class FakePolicy : ITransmitPolicyContext
    {
        public bool HasValidStationCallsign => true;
        public bool HasValidAprsIsPasscode => true;
    }
}
