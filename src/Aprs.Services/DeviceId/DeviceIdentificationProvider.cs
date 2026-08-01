namespace Aprs.Services;

/// <summary>
/// The process-wide device-ID service used by call sites that don't have one injected (chiefly the
/// station marker viewmodel, which is created in many places and can't thread the dependency through).
/// The composition root assigns the shared refreshable instance at startup; until then — and in tests —
/// a lazily-created bundled-snapshot service is used, so identification always works.
/// </summary>
public static class DeviceIdentificationProvider
{
    private static readonly Lazy<IDeviceIdentificationService> Fallback =
        new(() => new DeviceIdentificationService());

    private static IDeviceIdentificationService? current;

    public static IDeviceIdentificationService Current
    {
        get => current ?? Fallback.Value;
        set => current = value;
    }
}
