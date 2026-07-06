namespace Aprs.Core;

/// <summary>
/// Protocol-level constants shared across all APRS Command projects.
/// </summary>
public static class AprsConstants
{
    /// <summary>
    /// AX.25 destination TOCALL identifying APRS Command on the network.
    /// Registered with the APRS Device Identification Database:
    /// github.com/aprsorg/aprs-deviceid
    ///
    /// Format: APCMDx where x is the major version digit.
    /// Update when the major version increments (APCMD1 for v1.x, etc.).
    /// </summary>
    public const string ToCall = "APCMD0";

    /// <summary>
    /// Fallback destination used when receiving/parsing packets from other
    /// software. Not used for outbound APRS Command transmissions.
    /// </summary>
    public const string GenericDestination = "APRS";
}
