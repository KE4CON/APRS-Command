using System.IO.Ports;
using System.Runtime.InteropServices;

namespace Aprs.Transport;

/// <summary>
/// Real cross-platform serial port discovery using <see cref="SerialPort.GetPortNames"/>.
/// Returns only ports likely to be a TNC or radio accessory, filtered per platform:
/// <list type="bullet">
/// <item><b>macOS</b> — /dev/tty.* (excludes /dev/cu.* to avoid duplicates)</item>
/// <item><b>Linux / Raspberry Pi</b> — /dev/ttyUSB*, /dev/ttyACM*, /dev/ttyS*, /dev/serial/by-id/*</item>
/// <item><b>Windows</b> — COM* ports</item>
/// </list>
/// Falls back to returning all port names when the platform is not recognised.
/// </summary>
public sealed class SerialPortDiscovery : ISerialPortDiscovery
{
    public IReadOnlyList<string> GetAvailablePortNames()
    {
        try
        {
            var all = SerialPort.GetPortNames();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS exposes each port as both /dev/tty.* and /dev/cu.*
                // The tty.* variant is the correct one for outbound connections (TNC, radio).
                return all
                    .Where(p => p.StartsWith("/dev/tty.", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // On Linux/Pi, USB serial adapters appear as /dev/ttyUSB* or /dev/ttyACM*.
                // On-board UART ports are /dev/ttyS* (usually ttyS0 on Pi).
                // Exclude Bluetooth (/dev/rfcomm*) and virtual (/dev/pts/*) by only keeping
                // the known-useful prefixes.
                return all
                    .Where(p =>
                        p.StartsWith("/dev/ttyUSB", StringComparison.OrdinalIgnoreCase) ||
                        p.StartsWith("/dev/ttyACM", StringComparison.OrdinalIgnoreCase) ||
                        p.StartsWith("/dev/ttyS", StringComparison.OrdinalIgnoreCase) ||
                        p.StartsWith("/dev/serial/", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return all
                    .Where(p => p.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p, NaturalStringComparer.Instance)
                    .ToArray();
            }

            // Unknown platform — return everything sorted.
            return all.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch
        {
            // If discovery fails (permissions, unavailable API) return empty rather than crashing.
            return [];
        }
    }

    /// <summary>Sorts COM1 before COM10 on Windows (natural sort order for port names).</summary>
    private sealed class NaturalStringComparer : IComparer<string>
    {
        public static NaturalStringComparer Instance { get; } = new();

        public int Compare(string? x, string? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            // Extract trailing number for natural sort: COM1 < COM2 < COM10
            var xNum = ExtractTrailingNumber(x);
            var yNum = ExtractTrailingNumber(y);
            if (xNum.HasValue && yNum.HasValue)
            {
                var prefixCmp = string.Compare(
                    x[..^xNum.Value.ToString().Length],
                    y[..^yNum.Value.ToString().Length],
                    StringComparison.OrdinalIgnoreCase);
                return prefixCmp != 0 ? prefixCmp : xNum.Value.CompareTo(yNum.Value);
            }

            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }

        private static int? ExtractTrailingNumber(string s)
        {
            var i = s.Length - 1;
            while (i >= 0 && char.IsDigit(s[i])) i--;
            return i < s.Length - 1 && int.TryParse(s[(i + 1)..], out var n) ? n : null;
        }
    }
}
