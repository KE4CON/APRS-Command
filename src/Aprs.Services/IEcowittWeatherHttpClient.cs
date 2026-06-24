namespace Aprs.Services;

public interface IEcowittWeatherHttpClient
{
    Task<string> GetStringAsync(Uri requestUri, TimeSpan timeout, CancellationToken cancellationToken = default);
}
