namespace Aprs.Services;

/// <summary>Fetches the current device-ID database JSON from its published source.</summary>
public interface IDeviceIdDatabaseDownloader
{
    Task<string> DownloadAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Downloads the APRS Foundation device-ID database (<c>tocalls.dense.json</c>, CC BY-SA 2.0) over HTTPS.
/// The bundled snapshot remains the offline fallback, so a failed or unreachable download is non-fatal —
/// the caller simply keeps the database it already had.
/// </summary>
public sealed class HttpDeviceIdDatabaseDownloader : IDeviceIdDatabaseDownloader
{
    // Canonical machine-readable endpoint (see docs/DEVICE_ID_DESIGN.md and THIRD_PARTY_NOTICES.md).
    private static readonly Uri DatabaseUri = new("https://aprs-deviceid.aprsfoundation.org/tocalls.dense.json");

    private readonly HttpClient httpClient;
    private readonly Uri databaseUri;
    private readonly TimeSpan timeout;

    public HttpDeviceIdDatabaseDownloader(
        HttpClient? httpClient = null, Uri? databaseUri = null, TimeSpan? timeout = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
        this.databaseUri = databaseUri ?? DatabaseUri;
        this.timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    public async Task<string> DownloadAsync(CancellationToken cancellationToken = default)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        using var response = await httpClient.GetAsync(databaseUri, timeoutCancellation.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(timeoutCancellation.Token).ConfigureAwait(false);
    }
}
