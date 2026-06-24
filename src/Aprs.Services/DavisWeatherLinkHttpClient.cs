namespace Aprs.Services;

public sealed class DavisWeatherLinkHttpClient : IDavisWeatherLinkHttpClient
{
    private readonly HttpClient httpClient;

    public DavisWeatherLinkHttpClient(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GetStringAsync(
        Uri requestUri,
        string apiKey,
        string apiSecret,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add("X-Api-Key", apiKey);
        request.Headers.Add("X-Api-Secret", apiSecret);

        using var response = await httpClient.SendAsync(request, timeoutCancellation.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(timeoutCancellation.Token).ConfigureAwait(false);
    }
}
