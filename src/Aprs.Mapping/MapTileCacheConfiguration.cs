namespace Aprs.Mapping;

public sealed record MapTileCacheConfiguration(
    bool CacheEnabled,
    string CacheRootFolder,
    long MaximumCacheSizeBytes,
    TimeSpan? TileExpirationAge,
    string ProviderName,
    bool AllowInternetTileDownload)
{
    public static MapTileCacheConfiguration Default { get; } = new(
        CacheEnabled: true,
        CacheRootFolder: Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "APRS Command",
            "TileCache"),
        MaximumCacheSizeBytes: 1024L * 1024L * 1024L,
        TileExpirationAge: TimeSpan.FromDays(30),
        ProviderName: "SampleGrid",
        AllowInternetTileDownload: false);
}
