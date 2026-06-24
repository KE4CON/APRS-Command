namespace Aprs.Mapping;

public sealed record OfflineMapDownloadArea(
    string AreaName,
    double NorthLatitude,
    double SouthLatitude,
    double EastLongitude,
    double WestLongitude,
    int MinimumZoom,
    int MaximumZoom,
    MapTileProviderDefinition SelectedMapProvider,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? Notes)
{
    public void Validate(bool allowLargeArea = false)
    {
        if (string.IsNullOrWhiteSpace(AreaName))
        {
            throw new ArgumentException("Area name is required.", nameof(AreaName));
        }

        if (NorthLatitude is < -90 or > 90 || SouthLatitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(NorthLatitude), "Latitudes must be between -90 and 90 degrees.");
        }

        if (EastLongitude is < -180 or > 180 || WestLongitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(EastLongitude), "Longitudes must be between -180 and 180 degrees.");
        }

        if (NorthLatitude <= SouthLatitude)
        {
            throw new ArgumentException("North latitude must be greater than south latitude.", nameof(NorthLatitude));
        }

        if (MinimumZoom < SelectedMapProvider.MinimumZoom || MaximumZoom > SelectedMapProvider.MaximumZoom || MinimumZoom > MaximumZoom)
        {
            throw new ArgumentOutOfRangeException(nameof(MinimumZoom), "Zoom range is outside the selected provider range.");
        }

        var approximateDegrees = Math.Abs(NorthLatitude - SouthLatitude) * Math.Abs(EastLongitude - WestLongitude);
        if (!allowLargeArea && approximateDegrees > 100)
        {
            throw new InvalidOperationException("Offline map area is large and requires explicit approval.");
        }
    }
}
