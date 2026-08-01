namespace Aprs.Services;

/// <summary>
/// The device or software identified from an APRS destination "tocall", per the APRS Foundation's
/// device-ID database.
/// </summary>
/// <param name="Vendor">Maker / author, e.g. "Kenwood" or "James Rospopo, KE4CON".</param>
/// <param name="Model">Model or product name, e.g. "TM-D710" or "APRS Command".</param>
/// <param name="DeviceClass">Raw class code, e.g. "software", "tracker", "rig", "ht".</param>
/// <param name="DeviceClassLabel">Human-readable class label, e.g. "Desktop software".</param>
/// <param name="Os">Operating system, when applicable (e.g. "Windows", "Linux"), else null.</param>
public sealed record DeviceIdentity(
    string Vendor,
    string Model,
    string DeviceClass,
    string DeviceClassLabel,
    string? Os)
{
    /// <summary>A compact one-line description, e.g. "APRS Command (Desktop software)".</summary>
    public string Display => string.IsNullOrEmpty(DeviceClassLabel)
        ? Model
        : $"{Model} ({DeviceClassLabel})";
}

/// <summary>
/// Identifies the device/software behind an APRS station from its destination tocall, using a bundled
/// snapshot of the APRS Foundation device-ID database (aprsorg/aprs-deviceid, CC BY-SA 2.0).
/// </summary>
public interface IDeviceIdentificationService
{
    /// <summary>
    /// Resolves the device/software for a destination tocall (e.g. "APCMD0", "APDR16"), or null if no
    /// pattern matches. Matching uses the database's <c>?</c> wildcards, most-specific pattern winning.
    /// </summary>
    DeviceIdentity? Identify(string? destinationTocall);

    /// <summary>
    /// Resolves the radio behind a MIC-E packet from its decoded comment, or null if it carries no
    /// recognised device code. MIC-E encodes the position in the destination field, so the model marker
    /// lives in the comment instead: modern radios end the comment with a two-character code (the
    /// <c>mice</c> table, e.g. <c>_"</c> = Yaesu FTM-350); legacy Kenwoods bracket it with a prefix and an
    /// optional suffix (the <c>micelegacy</c> table, e.g. <c>]</c> = TM-D700, <c>]=</c> = TM-D710).
    /// </summary>
    DeviceIdentity? IdentifyMicE(string? micEComment);

    /// <summary>
    /// Convenience resolver that tries the destination tocall first and falls back to MIC-E comment
    /// matching, so a caller can identify any station without knowing which encoding produced it.
    /// </summary>
    DeviceIdentity? Identify(string? destinationTocall, string? micEComment);
}
