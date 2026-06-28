using System.Runtime.InteropServices;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Controls PTT via a Raspberry Pi GPIO pin.
/// On platforms where GPIO is not available (macOS, Windows, Linux x64 without
/// GPIO hardware), this class does nothing — APRS Command starts normally.
///
/// <para>Wire the GPIO pin through a transistor or optoisolator to the radio's
/// PTT input. Never connect GPIO directly to radio hardware without isolation.</para>
///
/// <para>Default pin: BCM 17 (physical pin 11). GPIO high = TX on.</para>
/// </summary>
public sealed class GpioPttController : IDisposable
{
    private readonly int pinNumber;
    private bool gpioAvailable;
    private bool disposed;

    // Lazy GPIO objects — null when GPIO is unavailable.
    private System.Device.Gpio.GpioController? controller;

    public GpioPttController(int pinNumber = 17)
    {
        this.pinNumber = pinNumber;
        TryInitialise();
    }

    /// <summary>Whether GPIO hardware is available and initialised on this host.</summary>
    public bool IsAvailable => gpioAvailable;

    /// <summary>Assert PTT (key the transmitter). No-op if GPIO is unavailable.</summary>
    public void AssertPtt()
    {
        if (!gpioAvailable || disposed) return;
        try { controller?.Write(pinNumber, true); }
        catch { /* hardware may not be ready */ }
    }

    /// <summary>Release PTT (unkey). Always safe to call, including on non-Pi platforms.</summary>
    public void ReleasePtt()
    {
        if (!gpioAvailable || disposed) return;
        try { controller?.Write(pinNumber, false); }
        catch { /* swallow */ }
    }

    private void TryInitialise()
    {
        // GPIO hardware only exists on Linux ARM (Raspberry Pi).
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            gpioAvailable = false;
            return;
        }

        try
        {
            controller = new System.Device.Gpio.GpioController();
            controller.OpenPin(pinNumber, System.Device.Gpio.PinMode.Output);
            controller.Write(pinNumber, false); // PTT released at startup
            gpioAvailable = true;
        }
        catch (Exception ex)
        {
            // GPIO not available on this host (Linux x64 server, WSL, etc.).
            controller?.Dispose();
            controller = null;
            gpioAvailable = false;
            System.Diagnostics.Debug.WriteLine(
                $"[GpioPttController] GPIO unavailable on pin {pinNumber}: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ReleasePtt();
        controller?.Dispose();
    }
}
