namespace Aprs.Desktop.Configuration;

/// <summary>
/// The unit system the operator prefers for distance display and entry throughout the UI.
/// Internally all distances are stored and transmitted in kilometres (required by APRS-IS),
/// but the UI converts to and from the operator's preferred unit at the boundary.
/// </summary>
public enum DistanceUnit
{
    /// <summary>Miles (default for US operators).</summary>
    Miles,

    /// <summary>Kilometres (default for operators outside the US).</summary>
    Kilometres
}
