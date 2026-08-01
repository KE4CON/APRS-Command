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
}
