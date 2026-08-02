namespace Aprs.Services;

/// <summary>
/// A thread-safe, hot-swappable <see cref="IDeviceIdentificationService"/>. It delegates every lookup to
/// an inner immutable <see cref="DeviceIdentificationService"/> that can be atomically replaced when a
/// fresher copy of the device-ID database is loaded — from the on-disk cache at startup, or from a
/// background / manual refresh. Starts on the bundled snapshot, so identification always works offline.
/// </summary>
public sealed class RefreshableDeviceIdentificationService : IDeviceIdentificationService
{
    private volatile DeviceIdentificationService inner;

    public RefreshableDeviceIdentificationService()
    {
        inner = new DeviceIdentificationService(); // bundled snapshot
    }

    /// <summary>Number of tocall patterns in the currently-active database.</summary>
    public int PatternCount => inner.PatternCount;

    public DeviceIdentity? Identify(string? destinationTocall) => inner.Identify(destinationTocall);

    public DeviceIdentity? IdentifyMicE(string? micEComment) => inner.IdentifyMicE(micEComment);

    public DeviceIdentity? Identify(string? destinationTocall, string? micEComment)
        => inner.Identify(destinationTocall, micEComment);

    /// <summary>
    /// Validates <paramref name="deviceIdJson"/> and, if usable, swaps it in as the active database. The
    /// swap is atomic (a single reference assignment), so concurrent lookups always see either the old or
    /// the new database, never a half-built one. Throws without changing the active database if the JSON
    /// can't be parsed or carries no tocall patterns — the caller keeps whatever it had.
    /// </summary>
    public void LoadFrom(string deviceIdJson)
    {
        var candidate = new DeviceIdentificationService(deviceIdJson);
        if (candidate.PatternCount == 0)
        {
            throw new InvalidOperationException("Device-ID database contained no tocall patterns.");
        }

        inner = candidate;
    }
}
