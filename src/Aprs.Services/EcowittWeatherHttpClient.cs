namespace Aprs.Services;

public sealed class EcowittWeatherHttpClient : IEcowittWeatherHttpClient
{
    private readonly HttpClient httpClient;

    public EcowittWeatherHttpClient(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GetStringAsync(Uri requestUri, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        using var response = await httpClient.GetAsync(requestUri, timeoutCancellation.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(timeoutCancellation.Token).ConfigureAwait(false);
    }
}
