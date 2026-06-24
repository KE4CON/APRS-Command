namespace Aprs.Mapping;

public sealed record MapTileProviderDefinition(
    string Name,
    string UrlTemplate,
    int MinimumZoom,
    int MaximumZoom,
    string AttributionText,
    bool SupportsOfflineCaching,
    bool InternetDownloadAllowed);
