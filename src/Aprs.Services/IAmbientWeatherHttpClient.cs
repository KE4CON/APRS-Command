namespace Aprs.Services;

public interface IAmbientWeatherHttpClient
{
    Task<string> GetStringAsync(
        Uri requestUri,
        string applicationKey,
        string apiKey,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
