namespace Aprs.Services;

/// <summary>
/// Calculates the APRS Aloha circle — the theoretical range within which a station
/// can receive packets at a usable rate, given the observed packet-per-minute load
/// on the shared channel.
///
/// Bob Bruninga WB4APR defined the Aloha circle as the range within which the
/// channel occupancy drops to 1/e (≈37%), meaning roughly 1 in 3 packets from a
/// station at that range will get through. Beyond this range, collision probability
/// makes reliable reception unlikely.
///
/// The calculation is based on observed channel load (packets per minute from all
/// visible stations) and assumes a typical VHF APRS range for normalization.
/// </summary>
public static class AlohaCircleService
{
    /// <summary>
    /// Typical single-hop VHF APRS range in kilometres used for normalization.
    /// This matches Bob Bruninga's original Aloha circle derivation.
    /// </summary>
    private const double ReferenceRangeKm = 80.0;

    /// <summary>
    /// Channel saturation threshold in packets per minute — at this load the channel
    /// is considered fully saturated and the Aloha circle collapses to zero.
    /// </summary>
    private const double SaturationPpm = 100.0;

    /// <summary>
    /// Calculates the Aloha circle radius in kilometres.
    /// </summary>
    /// <param name="uniqueStationCount">Number of unique stations heard this session.</param>
    /// <param name="packetsPerMinute">Current observed packets per minute on the channel.</param>
    /// <returns>Aloha circle radius in kilometres, or null if insufficient data.</returns>
    public static AlohaCircleResult? Calculate(int uniqueStationCount, double packetsPerMinute)
    {
        if (uniqueStationCount < 2 || packetsPerMinute < 0.1)
            return null;

        // Channel load fraction: ratio of observed load to saturation point
        var loadFraction = Math.Min(packetsPerMinute / SaturationPpm, 1.0);

        // Aloha circle radius: scales with 1/sqrt(load) — as load increases the usable range shrinks
        // At 1/e load (≈37% of saturation) the range equals the reference range
        var radiusKm = ReferenceRangeKm / Math.Sqrt(packetsPerMinute);

        // Cap to a sensible maximum
        radiusKm = Math.Min(radiusKm, ReferenceRangeKm * 2.0);

        // Estimate maximum stations we can service: inversely proportional to load
        var maxServiceableStations = (int)Math.Round(uniqueStationCount / Math.Max(loadFraction, 0.01));

        return new AlohaCircleResult(
            RadiusKm:              radiusKm,
            PacketsPerMinute:      packetsPerMinute,
            UniqueStations:        uniqueStationCount,
            ChannelLoadFraction:   loadFraction,
            EstimatedMaxStations:  Math.Min(maxServiceableStations, 1000));
    }
}

/// <summary>Result of an Aloha circle calculation.</summary>
public sealed record AlohaCircleResult(
    double RadiusKm,
    double PacketsPerMinute,
    int    UniqueStations,
    double ChannelLoadFraction,
    int    EstimatedMaxStations)
{
    public string RadiusDisplay    => $"{RadiusKm:F1} km";
    public string LoadDisplay      => $"{ChannelLoadFraction * 100:F0}%";
    public string Summary =>
        $"Aloha radius {RadiusKm:F1} km  ·  {PacketsPerMinute:F1} pkt/min  ·  {UniqueStations} stations  ·  channel load {ChannelLoadFraction * 100:F0}%";
}
