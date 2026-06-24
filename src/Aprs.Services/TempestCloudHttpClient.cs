namespace Aprs.Services;

public sealed class TempestCloudHttpClient : ITempestCloudHttpClient
{
    private readonly HttpClient httpClient;

    public TempestCloudHttpClient(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> GetStringAsync(Uri requestUri, string accessToken, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await httpClient.SendAsync(request, timeoutCancellation.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(timeoutCancellation.Token).ConfigureAwait(false);
    }
}
