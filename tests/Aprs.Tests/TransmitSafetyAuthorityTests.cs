using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Tests for the central transmit-safety authority: the global inhibit (exercise/training), the
/// identity gate (no placeholder callsign), the APRS-IS passcode gate, per-port delegation, and the
/// priority order between them.
/// </summary>
public sealed class TransmitSafetyAuthorityTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
    private const string PortId = "rf-tx";

    private sealed class FakePolicy : ITransmitPolicyContext
    {
        public bool HasValidStationCallsign { get; set; } = true;
        public bool HasValidAprsIsPasscode { get; set; } = true;
    }

    /// <summary>A port manager with one fully transmit-ready port (enabled, transmit-enabled, connected).</summary>
    private static AprsPortManager ReadyPortManager()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(AprsPortManager.CreateDefaultPort(PortId, "RF TX", AprsPortType.TcpKiss, "test") with
        {
            Enabled = true,
            ReceiveEnabled = true,
            TransmitEnabled = true,
            ConnectionState = AprsPortConnectionState.Connected
        });
        return manager;
    }

    private static TransmitSafetyAuthority Authority(
        out FakePolicy policy,
        AprsPortManager? portManager = null)
    {
        policy = new FakePolicy();
        return new TransmitSafetyAuthority(portManager ?? ReadyPortManager(), policy);
    }

    [Fact]
    public void AllChecksPass_TransmitAllowed()
    {
        var authority = Authority(out _);

        var decision = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.Rf));

        Assert.True(decision.IsAllowed);
        Assert.Equal(TransmitDenyReason.None, decision.Reason);
    }

    [Fact]
    public void Inhibit_BlocksEverything_EvenWhenEverythingElseIsValid()
    {
        var authority = Authority(out _);

        authority.Inhibit("Exercise mode");
        var decision = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.Rf));

        Assert.False(decision.IsAllowed);
        Assert.Equal(TransmitDenyReason.GlobalInhibit, decision.Reason);
        Assert.True(authority.IsInhibited);
        Assert.Equal("Exercise mode", authority.InhibitReason);
    }

    [Fact]
    public void Inhibit_TakesPriorityOverIdentityAndPort()
    {
        // Even with an invalid callsign and an unregistered port, inhibit is the reported reason.
        var authority = new TransmitSafetyAuthority(new AprsPortManager(), new FakePolicy { HasValidStationCallsign = false });

        authority.Inhibit("Drill in progress");
        var decision = authority.Evaluate(new TransmitRequest("missing-port", TransmitDestination.AprsIs));

        Assert.Equal(TransmitDenyReason.GlobalInhibit, decision.Reason);
    }

    [Fact]
    public void Release_RestoresTransmitAfterInhibit()
    {
        var authority = Authority(out _);

        authority.Inhibit("Exercise mode");
        authority.Release();
        var decision = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.Rf));

        Assert.True(decision.IsAllowed);
        Assert.False(authority.IsInhibited);
        Assert.Null(authority.InhibitReason);
    }

    [Fact]
    public void Inhibit_WithBlankReason_StillReportsAReason()
    {
        var authority = Authority(out _);

        authority.Inhibit("   ");
        var decision = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.Rf));

        Assert.False(decision.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(decision.Explanation));
    }

    [Fact]
    public void PlaceholderCallsign_BlocksTransmit_OnBothDestinations()
    {
        var authority = Authority(out var policy);
        policy.HasValidStationCallsign = false;

        var rf = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.Rf));
        var isgate = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.AprsIs));

        Assert.Equal(TransmitDenyReason.NoValidCallsign, rf.Reason);
        Assert.Equal(TransmitDenyReason.NoValidCallsign, isgate.Reason);
    }

    [Fact]
    public void AprsIs_WithoutValidPasscode_IsBlocked()
    {
        var authority = Authority(out var policy);
        policy.HasValidAprsIsPasscode = false;

        var decision = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.AprsIs));

        Assert.False(decision.IsAllowed);
        Assert.Equal(TransmitDenyReason.AprsIsPasscodeRequired, decision.Reason);
    }

    [Fact]
    public void Rf_DoesNotRequireAprsIsPasscode()
    {
        var authority = Authority(out var policy);
        policy.HasValidAprsIsPasscode = false; // irrelevant for RF

        var decision = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.Rf));

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public void IdentityGate_TakesPriorityOverPasscodeGate()
    {
        var authority = Authority(out var policy);
        policy.HasValidStationCallsign = false;
        policy.HasValidAprsIsPasscode = false;

        var decision = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.AprsIs));

        // Callsign is checked before passcode.
        Assert.Equal(TransmitDenyReason.NoValidCallsign, decision.Reason);
    }

    [Fact]
    public void UnregisteredPort_IsBlockedByPortCheck()
    {
        var authority = Authority(out _);

        var decision = authority.Evaluate(new TransmitRequest("does-not-exist", TransmitDestination.Rf));

        Assert.False(decision.IsAllowed);
        Assert.Equal(TransmitDenyReason.Port, decision.Reason);
    }

    [Fact]
    public void DisconnectedPort_IsBlockedByPortCheck()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(AprsPortManager.CreateDefaultPort(PortId, "RF TX", AprsPortType.TcpKiss, "test") with
        {
            Enabled = true,
            TransmitEnabled = true,
            ConnectionState = AprsPortConnectionState.Disconnected
        });
        var authority = new TransmitSafetyAuthority(manager, new FakePolicy());

        var decision = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.Rf));

        Assert.Equal(TransmitDenyReason.Port, decision.Reason);
    }

    [Fact]
    public void TransmitDisabledPort_IsBlockedByPortCheck()
    {
        var manager = new AprsPortManager();
        manager.RegisterPort(AprsPortManager.CreateDefaultPort(PortId, "RF TX", AprsPortType.TcpKiss, "test") with
        {
            Enabled = true,
            TransmitEnabled = false, // per-port opt-in is off
            ConnectionState = AprsPortConnectionState.Connected
        });
        var authority = new TransmitSafetyAuthority(manager, new FakePolicy());

        var decision = authority.Evaluate(new TransmitRequest(PortId, TransmitDestination.Rf));

        Assert.Equal(TransmitDenyReason.Port, decision.Reason);
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new TransmitSafetyAuthority(null!, new FakePolicy()));
        Assert.Throws<ArgumentNullException>(() => new TransmitSafetyAuthority(new AprsPortManager(), null!));
    }
}
