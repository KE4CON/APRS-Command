namespace Aprs.Services;

public interface ITempestCloudHttpClient
{
    Task<string> GetStringAsync(Uri requestUri, string accessToken, TimeSpan timeout, CancellationToken cancellationToken = default);
}
