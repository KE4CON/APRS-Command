namespace Aprs.Services;

public sealed class AmbientWeatherHttpClient : IAmbientWeatherHttpClient
{
    private readonly HttpClient httpClient;

    public AmbientWeatherHttpClient(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GetStringAsync(
        Uri requestUri,
        string applicationKey,
        string apiKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        var separator = string.IsNullOrWhiteSpace(requestUri.Query) ? "?" : "&";
        var uri = new Uri($"{requestUri}{separator}applicationKey={Uri.EscapeDataString(applicationKey)}&apiKey={Uri.EscapeDataString(apiKey)}");

        using var response = await httpClient.GetAsync(uri, timeoutCancellation.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(timeoutCancellation.Token).ConfigureAwait(false);
    }
}
