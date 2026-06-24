namespace Aprs.Services;

/// <summary>
/// Resolves weather-service credentials from a storage layer without requiring drivers to persist secrets directly.
/// </summary>
public interface IWeatherCredentialStore
{
    ValueTask<string?> GetSecretAsync(string credentialReference, CancellationToken cancellationToken = default);
}
