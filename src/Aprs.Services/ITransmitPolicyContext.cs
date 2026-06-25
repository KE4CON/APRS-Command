namespace Aprs.Services;

/// <summary>
/// Supplies the current identity / credential facts the transmit authority needs. Implemented in the
/// composition layer over persisted settings, so the engine-side authority stays free of any
/// dependency on where settings live.
/// </summary>
public interface ITransmitPolicyContext
{
    /// <summary>True when a real station callsign is set (not blank, not the N0CALL placeholder).</summary>
    bool HasValidStationCallsign { get; }

    /// <summary>
    /// True when an APRS-IS passcode suitable for transmitting is configured (present, numeric, and
    /// not the "-1" receive-only sentinel).
    /// </summary>
    bool HasValidAprsIsPasscode { get; }
}
