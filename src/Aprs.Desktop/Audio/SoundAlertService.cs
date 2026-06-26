using System.Runtime.InteropServices;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.Audio;

/// <summary>
/// The kind of sound to play. Each maps to a distinct synthesized tone.
/// </summary>
public enum AlertSound
{
    /// <summary>Two ascending tones — played when a direct message arrives addressed to your callsign.</summary>
    MessageReceived,

    /// <summary>Three medium pulses — played when a Warning alert triggers.</summary>
    WarningAlert,

    /// <summary>Rapid urgent pulses — played when a Critical alert triggers.</summary>
    CriticalAlert,

    /// <summary>Single soft rising tone — played when APRS-IS connects.</summary>
    Connected,

    /// <summary>Single soft falling tone — played when APRS-IS disconnects.</summary>
    Disconnected
}

/// <summary>
/// Cross-platform sound alert service. Generates WAV tones in memory and plays them using the
/// platform-appropriate method. Fails silently (logs only) if audio is unavailable.
/// </summary>
public sealed class SoundAlertService
{
    private readonly IAppSettingsStore store;
    private readonly HashSet<string> recentlyPlayed = new(StringComparer.Ordinal);
    private readonly object gate = new();

    public SoundAlertService(IAppSettingsStore store)
        => this.store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// Plays <paramref name="sound"/> if the operator's settings permit it, on a background thread.
    /// The <paramref name="dedupeKey"/> prevents the same alert from playing repeatedly in a short
    /// window — pass a unique string per alert trigger (e.g. the trigger ID).
    /// </summary>
    public void Play(AlertSound sound, string? dedupeKey = null)
    {
        var settings = store.Load().Audio;

        if (!IsEnabled(sound, settings)) return;

        if (dedupeKey is not null)
        {
            lock (gate)
            {
                if (!recentlyPlayed.Add(dedupeKey)) return;
            }

            // Remove the key after 10 seconds so the same alert can sound again if it re-fires.
            Task.Delay(TimeSpan.FromSeconds(10))
                .ContinueWith(_ => { lock (gate) { recentlyPlayed.Remove(dedupeKey); } });
        }

        var volume = Math.Clamp(settings.VolumePercent, 0, 100) / 100.0;
        var wav = GenerateWav(sound, volume);

        Task.Run(() => PlayWav(wav));
    }

    // ── Settings gate ─────────────────────────────────────────────────────

    private static bool IsEnabled(AlertSound sound, AudioSettings s) => sound switch
    {
        AlertSound.MessageReceived => s.PlayOnMessageReceived,
        AlertSound.WarningAlert    => s.PlayOnWarningAlert,
        AlertSound.CriticalAlert   => s.PlayOnCriticalAlert,
        AlertSound.Connected       => s.PlayOnConnectionEvents,
        AlertSound.Disconnected    => s.PlayOnConnectionEvents,
        _                          => false
    };

    // ── WAV generation (pure .NET, no external files) ─────────────────────

    private static byte[] GenerateWav(AlertSound sound, double volume)
    {
        (int Freq, double Duration)[] tones = sound switch
        {
            AlertSound.MessageReceived => [(880, 0.12), (1320, 0.12)],
            AlertSound.WarningAlert    => [(660, 0.08), (660, 0.08), (660, 0.08)],
            AlertSound.CriticalAlert   => [(440, 0.06), (440, 0.06), (440, 0.06), (440, 0.06), (440, 0.06)],
            AlertSound.Connected       => [(523, 0.15)],
            AlertSound.Disconnected    => [(392, 0.15)],
            _                          => [(440, 0.10)]
        };

        const int sampleRate = 44100;
        const double silenceBetween = 0.05; // 50ms gap between tones

        var samples = new List<short>();
        foreach (var (freq, duration) in tones)
        {
            int count = (int)(sampleRate * duration);
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / sampleRate;
                // Sine wave with simple ADSR envelope (attack + release to avoid clicks)
                double envelope = Math.Min(i / (sampleRate * 0.01), 1.0)  // 10ms attack
                    * Math.Min((count - i) / (sampleRate * 0.02), 1.0);     // 20ms release
                double sample = Math.Sin(2 * Math.PI * freq * t) * envelope * volume;
                samples.Add((short)(sample * short.MaxValue));
            }

            // Gap between tones
            int silenceSamples = (int)(sampleRate * silenceBetween);
            for (int i = 0; i < silenceSamples; i++) samples.Add(0);
        }

        return BuildWav(samples.ToArray(), sampleRate);
    }

    private static byte[] BuildWav(short[] samples, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        int byteCount = samples.Length * 2;

        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + byteCount);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(sampleRate); w.Write(sampleRate * 2);
        w.Write((short)2); w.Write((short)16);
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(byteCount);
        foreach (var s in samples) w.Write(s);

        return ms.ToArray();
    }

    // ── Platform playback ─────────────────────────────────────────────────

    private static void PlayWav(byte[] wav)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                PlayMacOs(wav);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                PlayLinux(wav);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                PlayWindows(wav);
        }
        catch (Exception ex)
        {
            // Never crash — audio is best-effort.
            Console.Error.WriteLine($"[Audio] Could not play alert sound: {ex.Message}");
        }
    }

    private static void PlayMacOs(byte[] wav)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"aprs-alert-{Guid.NewGuid():N}.wav");
        try
        {
            File.WriteAllBytes(tmp, wav);
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "afplay",
                Arguments = $"\"{tmp}\"",
                UseShellExecute = false,
                RedirectStandardError = true
            });
            p?.WaitForExit(3000);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    private static void PlayLinux(byte[] wav)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"aprs-alert-{Guid.NewGuid():N}.wav");
        try
        {
            File.WriteAllBytes(tmp, wav);
            // Try aplay first (ALSA), fall back to paplay (PulseAudio)
            foreach (var player in new[] { "aplay", "paplay" })
            {
                try
                {
                    using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = player,
                        Arguments = $"\"{tmp}\"",
                        UseShellExecute = false,
                        RedirectStandardError = true
                    });
                    if (p is not null) { p.WaitForExit(3000); return; }
                }
                catch { /* try next */ }
            }
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    private static void PlayWindows(byte[] wav)
    {
        // System.Media.SoundPlayer is Windows-only — access via reflection to keep the
        // project cross-platform without a conditional compile.
        var playerType = Type.GetType("System.Media.SoundPlayer, System.Windows.Extensions");
        if (playerType is null) return;

        using var ms = new MemoryStream(wav);
        using var player = (IDisposable?)Activator.CreateInstance(playerType, ms);
        playerType.GetMethod("PlaySync")?.Invoke(player, null);
    }
}
