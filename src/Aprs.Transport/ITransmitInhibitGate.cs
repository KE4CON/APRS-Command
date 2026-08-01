namespace Aprs.Transport;

/// <summary>
/// Minimal transmit-inhibit contract consulted at every transport transmit chokepoint, so a single
/// global inhibit (for example exercise/training mode) hard-blocks <em>every</em> keyed-up transmit
/// regardless of which higher-level path (beacon, object, message, iGate, weather, RF) initiated it.
///
/// <para>Kept in the transport layer so the lowest-level transmit code can consult it without an
/// upward dependency on <c>Aprs.Services</c>. The Services-layer transmit-safety authority
/// implements this interface, and the composition root hands the authority to each transmit client
/// so no caller can transmit by a side path that forgot a check.</para>
/// </summary>
public interface ITransmitInhibitGate
{
    /// <summary>True while all transmit is globally inhibited (for example exercise mode).</summary>
    bool IsTransmitInhibited { get; }

    /// <summary>Human-readable reason for the current inhibit, or <c>null</c> when not inhibited.</summary>
    string? InhibitReason { get; }
}
