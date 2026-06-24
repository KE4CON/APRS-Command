namespace Aprs.Services;

public interface IWeatherSoftwareHttpClient
{
    Task<string> GetStringAsync(Uri requestUri, TimeSpan timeout, CancellationToken cancellationToken = default);
}
