using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aprs.Desktop.Services;

/// <summary>
/// Cross-platform text-to-speech service using OS-native TTS engines.
///
/// Platform strategy:
///   macOS   — uses the built-in 'say' command (always available, no install).
///   Windows — uses PowerShell + System.Speech.Synthesis.SpeechSynthesizer
///             (built into Windows, no install required).
///   Linux / Raspberry Pi — uses espeak-ng if available, falls back to
///             spd-say, then fails gracefully with a console log.
///
/// All speech is fire-and-forget on a background thread — never blocks the UI
/// or the packet receive loop. Only one utterance runs at a time; a small
/// queue (max 3) absorbs brief bursts. If the queue is full the oldest pending
/// item is dropped to prevent a backlog building up during busy activations.
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
                await SpeakCoreAsync(text, cts.Token).ConfigureAwait(false);
            }
        }
        finally { semaphore.Release(); }
    }

    private static async Task SpeakCoreAsync(string text, CancellationToken ct)
    {
        try
        {
            ProcessStartInfo psi;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                psi = new ProcessStartInfo("say", $"\"{text}\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var escaped = text.Replace("'", "''");
                var ps = $"Add-Type -AssemblyName System.Speech; " +
                         $"(New-Object System.Speech.Synthesis.SpeechSynthesizer).Speak('{escaped}')";
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
                // Linux / Raspberry Pi
                var engine = FindLinuxEngine();
                if (engine is null)
                {
                    Console.Error.WriteLine("[VoiceAlert] No TTS engine found. Install espeak-ng: sudo apt install espeak-ng");
                    return;
                }
                psi = new ProcessStartInfo(engine, $"\"{text}\"")
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
