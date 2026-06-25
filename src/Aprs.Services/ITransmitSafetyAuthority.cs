namespace Aprs.Services;

/// <summary>
/// The single authority every transmit path consults before keying up. It owns the global transmit
/// inhibit (the master switch exercise and training modes flip) and applies the identity,
/// destination, and per-port safety policy in one place, so no caller can transmit by a side path
/// that forgot a check.
/// </summary>
public interface ITransmitSafetyAuthority
{
    /// <summary>True while all transmit is globally inhibited (e.g. exercise mode).</summary>
    bool IsInhibited { get; }

    /// <summary>Human-readable reason for the current inhibit, or null when not inhibited.</summary>
    string? InhibitReason { get; }

    /// <summary>Globally block all transmit until <see cref="Release"/> is called. Idempotent; the latest reason wins.</summary>
    void Inhibit(string reason);

    /// <summary>Lift a global inhibit. Identity, destination, and per-port checks still apply afterwards.</summary>
    void Release();

    /// <summary>Evaluate whether a specific transmit is permitted right now, with a precise reason if not.</summary>
    TransmitDecision Evaluate(TransmitRequest request);
}
