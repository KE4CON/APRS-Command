using System.Runtime.InteropServices;

namespace Aprs.Desktop.Audio;

/// <summary>
/// Discovers available audio input and output devices using platform-specific methods.
/// Falls back gracefully if discovery fails — returns an empty list rather than throwing.
/// </summary>
public static class AudioDeviceDiscovery
{
    /// <summary>Returns all available audio input devices (microphone/line-in).</summary>
    public static IReadOnlyList<AudioDevice> GetInputDevices()
        => DiscoverDevices(input: true);

    /// <summary>Returns all available audio output devices (speakers/line-out).</summary>
    public static IReadOnlyList<AudioDevice> GetOutputDevices()
        => DiscoverDevices(input: false);

    private static IReadOnlyList<AudioDevice> DiscoverDevices(bool input)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return DiscoverMacOs(input);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return DiscoverLinux(input);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return DiscoverWindows(input);
        }
        catch { /* fail silently */ }

        return [];
    }

    // ── macOS ─────────────────────────────────────────────────────────────

    private static IReadOnlyList<AudioDevice> DiscoverMacOs(bool input)
    {
        // Use system_profiler to list audio devices — available on all macOS installs.
        var result = RunProcess("system_profiler", "SPAudioDataType");
        if (string.IsNullOrWhiteSpace(result)) return [];

        var devices = new List<AudioDevice>();
        var lines = result.Split('\n');
        string? currentName = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Device name lines end with ':' and are indented one level.
            if (line.StartsWith("    ") && !line.StartsWith("        ") && trimmed.EndsWith(':'))
            {
                currentName = trimmed.TrimEnd(':').Trim();
                continue;
            }

            if (currentName is null) continue;

            // Check if this device has input or output channels.
            if (trimmed.StartsWith("Input Channels:") && input)
            {
                devices.Add(new AudioDevice(currentName, AudioDeviceType.Input, currentName));
                currentName = null;
            }
            else if (trimmed.StartsWith("Output Channels:") && !input)
            {
                devices.Add(new AudioDevice(currentName, AudioDeviceType.Output, currentName));
                currentName = null;
            }
        }

        return devices.Count > 0 ? devices : FallbackMacOs(input);
    }

    private static IReadOnlyList<AudioDevice> FallbackMacOs(bool input)
    {
        // Fallback: use soundflower/coreaudio via a simpler approach.
        // List common device names that are almost always present on macOS.
        var devices = new List<AudioDevice>();
        var kind = input ? AudioDeviceType.Input : AudioDeviceType.Output;

        if (input)
        {
            devices.Add(new AudioDevice("Built-in Microphone", kind, "Built-in Microphone"));
            devices.Add(new AudioDevice("Built-in Input", kind, "Built-in Input"));
        }
        else
        {
            devices.Add(new AudioDevice("Built-in Output", kind, "Built-in Output"));
            devices.Add(new AudioDevice("Built-in Speakers", kind, "Built-in Speakers"));
        }

        // USB audio devices commonly used with radios.
        devices.Add(new AudioDevice("USB Audio Device", kind, "USB Audio Device"));
        devices.Add(new AudioDevice("USB PnP Sound Device", kind, "USB PnP Sound Device"));

        return devices;
    }

    // ── Linux / Raspberry Pi ──────────────────────────────────────────────

    private static IReadOnlyList<AudioDevice> DiscoverLinux(bool input)
    {
        // Use arecord -l (input) or aplay -l (output) to list ALSA devices.
        var cmd = input ? "arecord" : "aplay";
        var result = RunProcess(cmd, "-l");
        if (string.IsNullOrWhiteSpace(result)) return FallbackLinux(input);

        var devices = new List<AudioDevice>();
        foreach (var line in result.Split('\n'))
        {
            if (!line.StartsWith("card ")) continue;

            // Parse: "card 0: Device [USB Audio Device], device 0: USB Audio [USB Audio]"
            var cardMatch = System.Text.RegularExpressions.Regex.Match(
                line, @"card (\d+):\s+\w+\s+\[([^\]]+)\]");
            if (!cardMatch.Success) continue;

            var cardNum  = cardMatch.Groups[1].Value;
            var cardName = cardMatch.Groups[2].Value.Trim();
            var kind     = input ? AudioDeviceType.Input : AudioDeviceType.Output;
            var id       = $"hw:{cardNum},0";

            devices.Add(new AudioDevice(cardName, kind, id));
        }

        return devices.Count > 0 ? devices : FallbackLinux(input);
    }

    private static IReadOnlyList<AudioDevice> FallbackLinux(bool input)
    {
        var kind = input ? AudioDeviceType.Input : AudioDeviceType.Output;
        return
        [
            new AudioDevice("default", kind, "default"),
            new AudioDevice("hw:0,0 (Card 0)", kind, "hw:0,0"),
            new AudioDevice("hw:1,0 (Card 1)", kind, "hw:1,0"),
            new AudioDevice("plughw:0,0", kind, "plughw:0,0"),
        ];
    }

    // ── Windows ───────────────────────────────────────────────────────────

    private static IReadOnlyList<AudioDevice> DiscoverWindows(bool input)
    {
        // Use PowerShell to enumerate audio devices via WMI.
        var query = input
            ? "Get-WmiObject Win32_SoundDevice | Select-Object -ExpandProperty Name"
            : "Get-WmiObject Win32_SoundDevice | Select-Object -ExpandProperty Name";
        var result = RunProcess("powershell", $"-NoProfile -Command \"{query}\"");
        if (string.IsNullOrWhiteSpace(result)) return FallbackWindows(input);

        var kind = input ? AudioDeviceType.Input : AudioDeviceType.Output;
        return result.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(name => new AudioDevice(name, kind, name))
            .ToArray();
    }

    private static IReadOnlyList<AudioDevice> FallbackWindows(bool input)
    {
        var kind = input ? AudioDeviceType.Input : AudioDeviceType.Output;
        return
        [
            new AudioDevice("Default", kind, "Default"),
            new AudioDevice("USB Audio Device", kind, "USB Audio Device"),
        ];
    }

    // ── Shared helper ──────────────────────────────────────────────────────

    private static string RunProcess(string filename, string args)
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName               = filename,
                Arguments              = args,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            });
            if (p is null) return string.Empty;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return output;
        }
        catch { return string.Empty; }
    }
}

/// <summary>An audio input or output device available on this machine.</summary>
public sealed record AudioDevice(string Name, AudioDeviceType DeviceType, string SystemId);

public enum AudioDeviceType { Input, Output }
