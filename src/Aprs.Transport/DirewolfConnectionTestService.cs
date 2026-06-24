namespace Aprs.Transport;

public sealed class DirewolfConnectionTestService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    private readonly IDirewolfConnectionProbe probe;
    private readonly IDirewolfProfileService profileService;

    public DirewolfConnectionTestService(IDirewolfConnectionProbe probe, IDirewolfProfileService? profileService = null)
    {
        this.probe = probe;
        this.profileService = profileService ?? new DirewolfProfileService();
    }

    public async Task<DirewolfConnectionTestResult> TestAsync(
        DirewolfProfile profile,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var validation = profileService.ValidateProfile(profile);
        if (validation.Errors.Count > 0)
        {
            return DirewolfConnectionTestResult.Failed(
                profile.Host,
                profile.KissPort,
                timestamp,
                string.Join(" ", validation.Errors));
        }

        try
        {
            await probe.ProbeAsync(profile.Host.Trim(), profile.KissPort, timeout ?? DefaultTimeout, cancellationToken).ConfigureAwait(false);
            return DirewolfConnectionTestResult.Succeeded(profile.Host.Trim(), profile.KissPort, timestamp);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DirewolfConnectionTestResult.Failed(profile.Host, profile.KissPort, timestamp, "Direwolf connection test timed out.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return DirewolfConnectionTestResult.Failed(profile.Host, profile.KissPort, timestamp, exception.Message);
        }
    }
}
