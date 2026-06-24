namespace Aprs.Services;

public interface IAprsBeaconFormatter
{
    /// <summary>
    /// Formats an APRS uncompressed fixed-position beacon packet.
    /// </summary>
    AprsBeaconFormatResult FormatFixedPositionBeacon(AprsBeaconInput input);

    /// <summary>
    /// Formats a placeholder APRS mobile-position beacon packet using the same uncompressed position fields.
    /// </summary>
    AprsBeaconFormatResult FormatMobilePositionBeacon(AprsBeaconInput input);

    /// <summary>
    /// Formats an APRS status beacon packet.
    /// </summary>
    AprsBeaconFormatResult FormatStatusBeacon(string sourceStationIdentifier, string destination, IReadOnlyList<string> path, string statusText);

    /// <summary>
    /// Builds beacon input from the local station profile.
    /// </summary>
    AprsBeaconInput CreateInputFromProfile(LocalStationProfile profile, string destination = "APRS", bool rfPathRequired = false);
}
