namespace Aprs.Services;

/// <summary>
/// Default <see cref="ITransmitSafetyAuthority"/>. Evaluation order, highest priority first:
/// <list type="number">
/// <item><b>Global inhibit</b> (exercise / training) — blocks everything outright.</item>
/// <item><b>Identity</b> — a real callsign must be set; never transmit as N0CALL / a placeholder.</item>
/// <item><b>Destination</b> — APRS-IS requires a valid passcode (the "-1" sentinel is receive-only).</item>
/// <item><b>Per-port</b> — delegates to the existing <see cref="IAprsPortManager.CheckTransmitSafety"/>
/// (port enabled, transmit-enabled, connected, not receive-only). The per-port transmit-enabled flag
/// is the explicit RF / port opt-in, so no separate RF master flag is needed here.</item>
/// </list>
/// The inhibit state is guarded so a mode toggle on one thread is observed consistently by an
/// evaluation on another.
/// </summary>
public sealed class TransmitSafetyAuthority : ITransmitSafetyAuthority
{
    private readonly IAprsPortManager portManager;
    private readonly ITransmitPolicyContext policy;
    private readonly object gate = new();

    private bool inhibited;
    private string? inhibitReason;

    public TransmitSafetyAuthority(IAprsPortManager portManager, ITransmitPolicyContext policy)
    {
        this.portManager = portManager ?? throw new ArgumentNullException(nameof(portManager));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public bool IsInhibited
    {
        get { lock (gate) { return inhibited; } }
    }

    public string? InhibitReason
    {
        get { lock (gate) { return inhibitReason; } }
    }

    public void Inhibit(string reason)
    {
        lock (gate)
        {
            inhibited = true;
            inhibitReason = string.IsNullOrWhiteSpace(reason) ? "Transmit is inhibited." : reason.Trim();
        }
    }

    public void Release()
    {
        lock (gate)
        {
            inhibited = false;
            inhibitReason = null;
        }
    }

    public TransmitDecision Evaluate(TransmitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1) Master inhibit wins over everything.
        bool isInhibited;
        string? reason;
        lock (gate)
        {
            isInhibited = inhibited;
            reason = inhibitReason;
        }

        if (isInhibited)
        {
            return TransmitDecision.Deny(TransmitDenyReason.GlobalInhibit, reason ?? "Transmit is inhibited.");
        }

        // 2) Identity: never transmit without a real callsign.
        if (!policy.HasValidStationCallsign)
        {
            return TransmitDecision.Deny(
                TransmitDenyReason.NoValidCallsign,
                "No valid station callsign is set. Transmit is blocked until a real callsign replaces the placeholder.");
        }

        // 3) Destination policy: APRS-IS transmit needs a real passcode.
        if (request.Destination == TransmitDestination.AprsIs && !policy.HasValidAprsIsPasscode)
        {
            return TransmitDecision.Deny(
                TransmitDenyReason.AprsIsPasscodeRequired,
                "A valid APRS-IS passcode is required to transmit to the internet (the connection is receive-only).");
        }

        // 4) Per-port checks. The global gate is already satisfied above, so pass true here and let
        //    the port manager apply the per-port rules (enabled / transmit-enabled / connected / not
        //    receive-only). The per-port transmit-enabled flag is the explicit opt-in for that port.
        var portResult = portManager.CheckTransmitSafety(request.PortId, globalTransmitSafetyEnabled: true);
        if (!portResult.IsSafe)
        {
            return TransmitDecision.Deny(
                TransmitDenyReason.Port,
                portResult.FailureReason ?? "The port is not ready to transmit.");
        }

        return TransmitDecision.Allow();
    }
}
