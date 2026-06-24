namespace Aprs.Services;

public interface IDavisWeatherLinkHttpClient
{
    Task<string> GetStringAsync(
        Uri requestUri,
        string apiKey,
        string apiSecret,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
