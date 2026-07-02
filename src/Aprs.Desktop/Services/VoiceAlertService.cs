using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aprs.Desktop.Services;

/// <summary>
/// Cross-platform text-to-speech service using OS-native TTS engines.
///
/// Platform strategy:
///   macOS   — uses the built-in 'say' command. Voice selected via -v flag.
///             Voices enumerated via 'say -v ?' (name + language on each line).
///   Windows — uses PowerShell + System.Speech.Synthesis.SpeechSynthesizer.
///             Voices enumerated via GetInstalledVoices().
///   Linux / Raspberry Pi — uses espeak-ng. Voices enumerated via
///             'espeak-ng --voices=en' (or all languages if none found).
///
/// All speech is fire-and-forget on a background thread. A queue of max 3
/// absorbs brief bursts; oldest item dropped if full.
/// </summary>
public sealed class VoiceAlertService : IDisposable
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly CancellationTokenSource cts = new();
    private readonly Queue<string> pending = new();
    private const int MaxQueue = 3;

    // ── Toggles ───────────────────────────────────────────────────────────────
    public bool IsEnabled                { get; set; } = false;
    public bool SpeakIncomingMessages    { get; set; } = true;
    public bool SpeakNetCheckIns         { get; set; } = true;
    public bool SpeakWeatherAlerts       { get; set; } = true;
    public bool SpeakStationAlerts       { get; set; } = false;
    public bool SpeakConnectionEvents    { get; set; } = false;
    public bool SpeakBeaconConfirmations { get; set; } = false;

    /// <summary>
    /// The OS voice name to use. Null = system default.
    /// macOS example: "Samantha", "Alex", "Ava".
    /// Windows example: "Microsoft David Desktop", "Microsoft Zira Desktop".
    /// Linux/Pi: espeak-ng voice name e.g. "en", "en-us", "en+f3".
    /// </summary>
    public string? PreferredVoiceName { get; set; } = null;

    // ── Voice enumeration ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the list of available voices on this platform.
    /// Each entry is (DisplayName, VoiceName) where VoiceName is passed to Speak.
    /// The first entry is always ("System Default", null).
    /// Returns immediately from cache after first call.
    /// </summary>
    public static IReadOnlyList<VoiceOption> GetAvailableVoices()
    {
        if (_cachedVoices is not null) return _cachedVoices;

        var list = new List<VoiceOption>
        {
            new("System Default", null)
        };

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                list.AddRange(GetMacVoices());
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                list.AddRange(GetWindowsVoices());
            else
                list.AddRange(GetLinuxVoices());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[VoiceAlert] Voice enumeration failed: {ex.Message}");
        }

        _cachedVoices = list;
        return list;
    }

    private static IReadOnlyList<VoiceOption>? _cachedVoices;

    private static IEnumerable<VoiceOption> GetMacVoices()
    {
        // 'say -v ?' outputs lines like:
        //   Alex                en_US    # Most people recognize me by my voice.
        //   Samantha            en_US    # ...
        using var proc = Process.Start(new ProcessStartInfo("say", "-v ?")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        });
        if (proc is null) yield break;

        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Name is left-padded, language code follows
            var parts = line.Trim().Split(new[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            var name = parts[0].Trim();
            var lang = parts[1].Trim();
            if (string.IsNullOrEmpty(name)) continue;
            // Show English voices first with language tag, others with full tag
            var display = lang.StartsWith("en", StringComparison.OrdinalIgnoreCase)
                ? $"{name}  ({lang})"
                : $"{name}  [{lang}]";
            yield return new VoiceOption(display, name);
        }
    }

    private static IEnumerable<VoiceOption> GetWindowsVoices()
    {
        // Use PowerShell to enumerate installed voices
        var ps = "Add-Type -AssemblyName System.Speech; " +
                 "(New-Object System.Speech.Synthesis.SpeechSynthesizer)" +
                 ".GetInstalledVoices() | ForEach-Object { $_.VoiceInfo.Name }";
        using var proc = Process.Start(new ProcessStartInfo("powershell",
            $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{ps}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        });
        if (proc is null) yield break;

        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = line.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            // Windows voice names look like "Microsoft David Desktop"
            // Show a cleaner display name by stripping "Microsoft" and "Desktop"
            var display = name
                .Replace("Microsoft ", "")
                .Replace(" Desktop", "")
                .Trim();
            yield return new VoiceOption($"{display}  (Windows)", name);
        }
    }

    private static IEnumerable<VoiceOption> GetLinuxVoices()
    {
        var engine = FindLinuxEngine();
        if (engine is null) yield break;

        if (engine != "espeak-ng") yield break; // spd-say doesn't support voice listing easily

        // espeak-ng --voices outputs a table; first column is language, fifth is voice name
        using var proc = Process.Start(new ProcessStartInfo("espeak-ng", "--voices")
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        });
        if (proc is null) yield break;

        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.TrimStart().StartsWith("Pty")) continue; // header line
            var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) continue;
            var lang      = parts[1];
            var voiceName = parts[4];
            if (string.IsNullOrEmpty(voiceName)) continue;
            yield return new VoiceOption($"{voiceName}  ({lang})", voiceName);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Speak(string text, VoiceAlertType alertType)
    {
        if (!IsEnabled) return;
        if (!IsTypeEnabled(alertType)) return;
        if (string.IsNullOrWhiteSpace(text)) return;

        lock (pending)
        {
            if (pending.Count >= MaxQueue)
                pending.Dequeue();
            pending.Enqueue(Sanitize(text));
        }

        _ = Task.Run(DrainAsync, cts.Token);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private bool IsTypeEnabled(VoiceAlertType t) => t switch
    {
        VoiceAlertType.IncomingMessage    => SpeakIncomingMessages,
        VoiceAlertType.NetCheckIn         => SpeakNetCheckIns,
        VoiceAlertType.WeatherAlert       => SpeakWeatherAlerts,
        VoiceAlertType.StationAlert       => SpeakStationAlerts,
        VoiceAlertType.ConnectionEvent    => SpeakConnectionEvents,
        VoiceAlertType.BeaconConfirmation => SpeakBeaconConfirmations,
        _                                 => true
    };

    private async Task DrainAsync()
    {
        if (!await semaphore.WaitAsync(0)) return;
        try
        {
            while (true)
            {
                string? text;
                lock (pending)
                {
                    if (pending.Count == 0) break;
                    text = pending.Dequeue();
                }
                await SpeakCoreAsync(text, PreferredVoiceName, cts.Token).ConfigureAwait(false);
            }
        }
        finally { semaphore.Release(); }
    }

    private static async Task SpeakCoreAsync(string text, string? voiceName, CancellationToken ct)
    {
        try
        {
            ProcessStartInfo psi;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var voiceArg = string.IsNullOrEmpty(voiceName) ? "" : $"-v \"{voiceName}\" ";
                psi = new ProcessStartInfo("say", $"{voiceArg}\"{text}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var escaped      = text.Replace("'", "''");
                var voiceClause  = string.IsNullOrEmpty(voiceName)
                    ? ""
                    : $"$s.SelectVoice('{voiceName.Replace("'", "''")}'); ";
                var ps = $"Add-Type -AssemblyName System.Speech; " +
                         $"$s = New-Object System.Speech.Synthesis.SpeechSynthesizer; " +
                         $"{voiceClause}$s.Speak('{escaped}')";
                psi = new ProcessStartInfo("powershell",
                    $"-NoProfile -NonInteractive -WindowStyle Hidden -Command \"{ps}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };
            }
            else
            {
                var engine = FindLinuxEngine();
                if (engine is null)
                {
                    Console.Error.WriteLine("[VoiceAlert] No TTS engine found. Install espeak-ng: sudo apt install espeak-ng");
                    return;
                }
                var voiceArg = (engine == "espeak-ng" && !string.IsNullOrEmpty(voiceName))
                    ? $"-v \"{voiceName}\" " : "";
                psi = new ProcessStartInfo(engine, $"{voiceArg}\"{text}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };
            }

            using var proc = Process.Start(psi);
            if (proc is null) return;
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[VoiceAlert] TTS error: {ex.Message}");
        }
    }

    private static string? _linuxEngine;
    private static string? FindLinuxEngine()
    {
        if (_linuxEngine is not null)
            return _linuxEngine.Length == 0 ? null : _linuxEngine;

        foreach (var cmd in new[] { "espeak-ng", "spd-say", "espeak" })
        {
            try
            {
                using var w = Process.Start(new ProcessStartInfo("which", cmd)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                });
                w?.WaitForExit();
                if (w?.ExitCode == 0) { _linuxEngine = cmd; return cmd; }
            }
            catch { }
        }

        _linuxEngine = string.Empty;
        return null;
    }

    private static string Sanitize(string s)
        => s.Replace("\"", "").Replace("'", "").Replace("`", "").Replace("\\", "").Trim();

    public void Dispose()
    {
        cts.Cancel();
        cts.Dispose();
        semaphore.Dispose();
    }
}

public enum VoiceAlertType
{
    IncomingMessage,
    NetCheckIn,
    WeatherAlert,
    StationAlert,
    ConnectionEvent,
    BeaconConfirmation
}

/// <summary>A selectable voice option shown in the voice picker dropdown.</summary>
public sealed record VoiceOption(string DisplayName, string? VoiceName)
{
    public override string ToString() => DisplayName;
}
