using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.Runtime;

/// <summary>The current state of the managed Direwolf process.</summary>
public enum DirewolfProcessState
{
    Idle,
    Starting,
    Running,
    Failed,
    Stopped
}

/// <summary>
/// Launches, monitors, and cleanly shuts down a Direwolf process. Generates a temporary
/// Direwolf config file from the operator's managed modem settings, starts Direwolf,
/// captures its output (forwarded to <see cref="OutputReceived"/>), and restarts if it
/// crashes. Connects via loopback KISS-TCP once running.
/// </summary>
public sealed class DirewolfProcessManager : IAsyncDisposable
{
    private readonly ManagedModemSettings settings;
    private readonly string callsign;
    private Process? process;
    private readonly CancellationTokenSource cts = new();
    private Task? monitorLoop;
    private string? configPath;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<DirewolfProcessState>? StateChanged;

    public DirewolfProcessState State { get; private set; } = DirewolfProcessState.Idle;
    public string? FailureReason { get; private set; }

    public DirewolfProcessManager(ManagedModemSettings settings, string callsign)
    {
        this.settings = settings;
        this.callsign = callsign;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the Direwolf executable. Returns the path if found, null if not installed.
    /// Checks the operator's configured path first, then the system PATH.
    /// </summary>
    public static string? FindDirewolf(string? configuredPath = null)
    {
        // Operator-specified path takes priority.
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
            return configuredPath;

        // Try common binary names on PATH.
        var names = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "direwolf.exe" }
            : new[] { "direwolf" };

        foreach (var name in names)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName               = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which",
                    Arguments              = name,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow         = true
                });
                if (p is null) continue;
                var output = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(2000);
                if (!string.IsNullOrWhiteSpace(output) && File.Exists(output.Split('\n')[0].Trim()))
                    return output.Split('\n')[0].Trim();
            }
            catch { /* try next */ }
        }

        return null;
    }

    /// <summary>Starts Direwolf and the monitor loop.</summary>
    public void Start()
    {
        if (State is DirewolfProcessState.Running or DirewolfProcessState.Starting) return;

        monitorLoop = Task.Run(async () => await RunMonitorLoopAsync().ConfigureAwait(false), cts.Token);
    }

    // ── Monitor loop ──────────────────────────────────────────────────────

    private async Task RunMonitorLoopAsync()
    {
        while (!cts.Token.IsCancellationRequested)
        {
            SetState(DirewolfProcessState.Starting);

            var exePath = FindDirewolf(settings.DirewolfPath);
            if (exePath is null)
            {
                FailureReason = "Direwolf not found. Install Direwolf and ensure it is on your PATH, or set the path in Settings → Connections → Managed Modem.";
                SetState(DirewolfProcessState.Failed);
                Emit(FailureReason);
                return; // Don't retry — Direwolf isn't installed.
            }

            configPath = WriteConfigFile();
            Emit($"[Managed Modem] Starting Direwolf from {exePath}");
            Emit($"[Managed Modem] Config: {configPath}");

            try
            {
                process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName               = exePath,
                        Arguments              = $"-c \"{configPath}\" -p", // -p = no pseudo-terminal
                        UseShellExecute        = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        CreateNoWindow         = true
                    },
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (_, e) => { if (e.Data is not null) Emit(e.Data); };
                process.ErrorDataReceived  += (_, e) => { if (e.Data is not null) Emit(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                SetState(DirewolfProcessState.Running);
                Emit($"[Managed Modem] Direwolf started (PID {process.Id}). KISS TCP on 127.0.0.1:{settings.KissPort}");

                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

                if (cts.Token.IsCancellationRequested) break;

                Emit($"[Managed Modem] Direwolf exited (code {process.ExitCode}). Restarting in 5s…");
                SetState(DirewolfProcessState.Failed);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                FailureReason = ex.Message;
                Emit($"[Managed Modem] Failed to start Direwolf: {ex.Message}");
                SetState(DirewolfProcessState.Failed);
            }
            finally
            {
                TryKillProcess();
            }

            // Wait before restarting.
            try { await Task.Delay(TimeSpan.FromSeconds(5), cts.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }

        SetState(DirewolfProcessState.Stopped);
        TryCleanupConfig();
    }

    // ── Config file generation ────────────────────────────────────────────

    private string WriteConfigFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aprs-command-direwolf-{Guid.NewGuid():N}.conf");
        var sb = new StringBuilder();

        // Callsign
        var ssid = settings.CallsignSsid > 0 ? $"-{settings.CallsignSsid}" : string.Empty;
        sb.AppendLine($"MYCALL {callsign.ToUpperInvariant()}{ssid}");
        sb.AppendLine();

        // Audio input
        if (!string.IsNullOrWhiteSpace(settings.AudioInputDevice))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                sb.AppendLine($"ADEVICE \"{settings.AudioInputDevice}\" \"{settings.AudioOutputDevice}\"");
            else
                sb.AppendLine($"ADEVICE {settings.AudioInputDevice}");
        }

        sb.AppendLine("ACHANNELS 1");
        sb.AppendLine();

        // Channel 0
        sb.AppendLine("CHANNEL 0");
        sb.AppendLine($"MYCALL {callsign.ToUpperInvariant()}{ssid}");
        sb.AppendLine("MODEM 1200");

        // PTT
        switch (settings.PttMethod)
        {
            case PttMethod.Rts when !string.IsNullOrWhiteSpace(settings.PttSerialPort):
                sb.AppendLine($"PTT {settings.PttSerialPort} RTS");
                break;
            case PttMethod.Dtr when !string.IsNullOrWhiteSpace(settings.PttSerialPort):
                sb.AppendLine($"PTT {settings.PttSerialPort} DTR");
                break;
            case PttMethod.GpioPin:
                // Direwolf supports GPIO PTT on Linux via the GPIO keyword.
                sb.AppendLine($"PTT GPIO {settings.PttGpioPin}");
                break;
            case PttMethod.None:
            default:
                sb.AppendLine("# PTT None — VOX or radio handles keying");
                break;
        }

        sb.AppendLine();

        // KISS TCP server
        sb.AppendLine($"KISSPORT {settings.KissPort}");
        sb.AppendLine();

        // Logging
        sb.AppendLine("LOGDIR /tmp");

        File.WriteAllText(path, sb.ToString());
        return path;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetState(DirewolfProcessState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private void Emit(string line)
        => OutputReceived?.Invoke(this, line);

    private void TryKillProcess()
    {
        try { process?.Kill(entireProcessTree: true); } catch { }
        try { process?.Dispose(); } catch { }
        process = null;
    }

    private void TryCleanupConfig()
    {
        if (configPath is not null)
        {
            try { File.Delete(configPath); } catch { }
            configPath = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);
        TryKillProcess();
        if (monitorLoop is not null)
        {
            try { await monitorLoop.ConfigureAwait(false); }
            catch { /* suppress */ }
        }
        TryCleanupConfig();
        cts.Dispose();
    }
}
