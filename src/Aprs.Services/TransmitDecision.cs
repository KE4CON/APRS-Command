namespace Aprs.Services;

/// <summary>The specific cause when a transmit is denied (or <see cref="None"/> when allowed).</summary>
public enum TransmitDenyReason
{
    /// <summary>Transmit was allowed.</summary>
    None,

    /// <summary>All transmit is globally inhibited (e.g. exercise mode).</summary>
    GlobalInhibit,

    /// <summary>No real station callsign is set (placeholder such as N0CALL).</summary>
    NoValidCallsign,

    /// <summary>APRS-IS transmit attempted without a valid passcode.</summary>
    AprsIsPasscodeRequired,

    /// <summary>A per-port check failed (disabled, not connected, receive-only, etc.).</summary>
    Port
}

/// <summary>The result of a transmit-safety evaluation: whether it is allowed, with a precise reason if not.</summary>
public sealed record TransmitDecision(bool IsAllowed, TransmitDenyReason Reason, string Explanation)
{
    public static TransmitDecision Allow() => new(true, TransmitDenyReason.None, "Transmit allowed.");

    public static TransmitDecision Deny(TransmitDenyReason reason, string explanation) =>
        new(false, reason, explanation);
}
