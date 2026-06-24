namespace Aprs.Services;

public sealed class NullWeatherCredentialStore : IWeatherCredentialStore
{
    public static NullWeatherCredentialStore Instance { get; } = new();

    private NullWeatherCredentialStore()
    {
    }

    public ValueTask<string?> GetSecretAsync(string credentialReference, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<string?>(null);
    }
}
