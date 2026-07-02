namespace Aprs.Services;

/// <summary>
/// APRS area object shape types as defined in the APRS Protocol Reference.
/// Area objects encode drawn shapes in the comment field using the area object overlay.
/// </summary>
public enum AprsAreaObjectShape
{
    /// <summary>Open black circle.</summary>
    OpenBlackCircle = 0,
    /// <summary>Open red line (used for line objects).</summary>
    OpenRedLine = 1,
    /// <summary>Open blue triangle.</summary>
    OpenBlueTriangle = 2,
    /// <summary>Open blue rectangle.</summary>
    OpenBlueRectangle = 3,
    /// <summary>Filled black circle.</summary>
    FilledBlackCircle = 5,
    /// <summary>Filled red line.</summary>
    FilledRedLine = 6,
    /// <summary>Filled blue triangle.</summary>
    FilledBlueTriangle = 7,
    /// <summary>Filled blue rectangle.</summary>
    FilledBlueRectangle = 8,
}
