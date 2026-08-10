using Aprs.Services;
using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Owns the live beacon pipeline: a <see cref="LocalStationProfileService"/> populated from
/// persisted settings, a <see cref="BeaconScheduler"/> wired to an APRS-IS client that can
/// actually transmit, and a background tick loop that fires scheduled beacons on time.
///
/// <para>Call <see cref="ApplySettings"/> whenever the operator saves their station profile
/// so changes take effect immediately without a restart. Call <see cref="BeaconNowAsync"/> when
/// the operator clicks the Beacon Now sidebar button.</para>
/// </summary>
public sealed class BeaconService : IAsyncDisposable
{
    private readonly LocalStationProfileService profileService;
    private readonly BeaconScheduler scheduler;
    private readonly ITransmitInhibitGate? inhibitGate;
    private readonly ILogService? log;
    // Guards aprsIsClient + lastConnectionSignature. ApplySettings runs from the UI thread (settings save)
    // AND the GPS background thread (position write-back), which previously rebuilt the socket every fix.
    private readonly object clientLock = new();
    private string? lastConnectionSignature;
    private IAprsIsClient? aprsIsClient;

    /// <summary>The transmit-capable APRS-IS client, if one is configured. Used by the message ACK coordinator.</summary>
    public IAprsIsClient? AprsIsClient => aprsIsClient;
    private readonly CancellationTokenSource cts = new();
    private Task? tickLoop;

    public BeaconService(
        LocalStationProfileService profileService,
        BeaconScheduler scheduler,
        IAprsIsClient? aprsIsClient,
        ITransmitInhibitGate? inhibitGate = null,
        ILogService? log = null)
    {
        this.profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        this.aprsIsClient = aprsIsClient;
        this.inhibitGate = inhibitGate;
        this.log = log;
    }

    /// <summary>Creates a fully wired BeaconService from the persisted station settings.</summary>
    public static BeaconService CreateFromSettings(
        Configuration.AppSettings settings,
        Aprs.Services.IRfBeaconTransmitClient? rfBeaconClient = null,
        ITransmitInhibitGate? inhibitGate = null,
        Aprs.Services.ExerciseMarking? marking = null,
        ILogService? log = null)
    {
        var station = settings.Station;
        var profileService = new LocalStationProfileService();

        // Push the persisted station profile into the service layer.
        var profile = ToLocalProfile(station);
        profileService.UpdateProfile(profile, DateTimeOffset.UtcNow);

        // Build an APRS-IS client that can transmit when a real passcode is configured.
        var aprsIsClient = BuildAprsIsClient(settings, inhibitGate);

        var schedulerConfig = new BeaconSchedulerConfiguration(
            SchedulerEnabled:        station.TransmitEnabled,
            AprsIsBeaconEnabled:     station.AprsIsTransmitEnabled,
            RfBeaconEnabled:         station.RfTransmitEnabled,
            MinimumBeaconInterval:   TimeSpan.FromMinutes(5),
            Destination:             Aprs.Core.AprsConstants.ToCall,
            RequireTransmitConfirmation: false,
            SmartBeaconing:          settings.SmartBeaconing.ToServiceConfig());

        var beaconFormatter = new AprsBeaconFormatter(marking);
        IAprsIsClient clientForScheduler = aprsIsClient ?? (IAprsIsClient)new NullAprsIsClient();
        var scheduler = new BeaconScheduler(
            profileService,
            beaconFormatter,
            clientForScheduler,
            schedulerConfig,
            rfBeaconClient: rfBeaconClient);

        var service = new BeaconService(profileService, scheduler, aprsIsClient, inhibitGate, log);
        // Seed the signature so the FIRST ApplySettings (e.g. a GPS position write-back) doesn't needlessly
        // rebuild the client that CreateFromSettings just built for the same connection config.
        service.lastConnectionSignature = ComputeConnectionSignature(settings);
        return service;
    }

    /// <summary>
    /// Updates the live profile service from freshly-saved settings so beacon content
    /// reflects the latest station configuration immediately.
    /// </summary>
    public void ApplySettings(Configuration.AppSettings settings)
    {
        var station = settings.Station;
        profileService.UpdateProfile(ToLocalProfile(station), DateTimeOffset.UtcNow);

        // Only tear down and rebuild the APRS-IS client when connection-relevant config actually changed
        // (server/passcode/callsign/filter/transmit flags). ApplySettings is ALSO called on every GPS
        // position write-back (~1 Hz for a mobile station); rebuilding a socket + receive loop each time
        // thrashed the connection and orphaned the client the message coordinator captured (audit deep pass).
        // Position/comment/interval changes must NOT rebuild — they only affect beacon content + scheduler.
        var signature = ComputeConnectionSignature(settings);
        lock (clientLock)
        {
            if (signature != lastConnectionSignature)
            {
                lastConnectionSignature = signature;

                // Disconnect AND dispose the old client so its CTS, receive-loop task and socket are released.
                var oldClient = aprsIsClient;
                if (oldClient is not null)
                {
                    _ = DisposeReplacedClientAsync(oldClient);
                }

                var newClient = BuildAprsIsClient(settings, inhibitGate);
                aprsIsClient = newClient;
                scheduler.ReplaceAprsIsClient(newClient ?? new NullAprsIsClient());

                if (newClient is not null && station.TransmitEnabled && station.AprsIsTransmitEnabled)
                {
                    _ = newClient.ConnectAsync(cts.Token);
                }
            }
        }

        // Refresh scheduler configuration for transmit flags and intervals.
        scheduler.UpdateConfiguration(new Aprs.Services.BeaconSchedulerConfiguration(
            SchedulerEnabled:            station.TransmitEnabled,
            AprsIsBeaconEnabled:         station.AprsIsTransmitEnabled,
            RfBeaconEnabled:             station.RfTransmitEnabled,
            MinimumBeaconInterval:       TimeSpan.FromMinutes(5),
            Destination:                 Aprs.Core.AprsConstants.ToCall,
            RequireTransmitConfirmation: false,
            SmartBeaconing:              settings.SmartBeaconing.ToServiceConfig()));
    }

    /// <summary>Starts the scheduler and the background tick loop.</summary>
    public void Start()
    {
        if (aprsIsClient is not null)
        {
            _ = aprsIsClient.ConnectAsync(cts.Token);
        }

        scheduler.Start();

        tickLoop = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    var tickResult = await scheduler.TickAsync(cts.Token).ConfigureAwait(false);
                    if (tickResult is { Transmitted: true }) LastBeaconAt = DateTimeOffset.UtcNow;
                    await Task.Delay(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Never crash the tick loop, but do not swallow silently either (CLAUDE.md rule).
                    log?.Error("Beacon", "Beacon scheduler tick failed; retrying in 30 s.", ex);
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }, cts.Token);
    }

    /// <summary>The UTC time of the most recent successful beacon transmission, or null if none yet.</summary>
    public DateTimeOffset? LastBeaconAt { get; private set; }

    /// <summary>Transmits a beacon immediately, bypassing the schedule.</summary>
    public async Task<BeaconNowResult> BeaconNowAsync(CancellationToken cancellationToken = default)
    {
        var result = await scheduler.BeaconNowAsync(cancellationToken).ConfigureAwait(false);
        if (result.Transmitted) LastBeaconAt = DateTimeOffset.UtcNow;
        return result;
    }

    /// <summary>Transmits a beacon immediately on all enabled RF paths.</summary>
    public async Task<Aprs.Services.BeaconNowResult> BeaconOnRfNowAsync(CancellationToken cancellationToken = default)
        => await scheduler.BeaconOnRfNowAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Current scheduler state — for the status display.</summary>
    public BeaconSchedulerState GetState() => scheduler.GetState();

    /// <summary>
    /// A string capturing exactly the settings that affect <see cref="BuildAprsIsClient"/>'s output, so
    /// ApplySettings can skip the socket rebuild when only position/comment/interval changed.
    /// </summary>
    private static string ComputeConnectionSignature(Configuration.AppSettings settings)
    {
        var station = settings.Station;
        var parts = new List<string?>
        {
            station.FullCallsign,
            station.TransmitEnabled.ToString(),
            station.AprsIsTransmitEnabled.ToString()
        };
        foreach (var port in settings.Connections.Ports)
        {
            if (port.Type != Configuration.ConnectionPortType.AprsIs) continue;
            var isConfig = port.Configuration.AprsIs;
            if (isConfig is null) continue;
            parts.Add(isConfig.ServerHost);
            parts.Add(isConfig.ServerPort.ToString());
            parts.Add(isConfig.Passcode);
            parts.Add(isConfig.Filter);
        }

        return string.Join("|", parts);
    }

    /// <summary>
    /// Builds a transmit-capable APRS-IS client from settings, or returns null if no
    /// APRS-IS port with a real passcode is configured.
    /// </summary>
    private static IAprsIsClient? BuildAprsIsClient(
        Configuration.AppSettings settings,
        ITransmitInhibitGate? inhibitGate)
    {
        var station = settings.Station;
        foreach (var port in settings.Connections.Ports)
        {
            if (port.Type != Configuration.ConnectionPortType.AprsIs) continue;
            var isConfig = port.Configuration.AprsIs;
            if (isConfig is null) continue;
            var passcode = isConfig.Passcode?.Trim();
            if (string.IsNullOrEmpty(passcode) || passcode == "-1") continue;

            var clientConfig = AprsIsClientConfiguration.Default with
            {
                ServerHost                 = isConfig.ServerHost,
                ServerPort                 = isConfig.ServerPort,
                Callsign                   = station.FullCallsign,
                Passcode                   = passcode,
                Filter                     = string.IsNullOrWhiteSpace(isConfig.Filter) ? null : isConfig.Filter,
                ReceiveOnly                = false,
                TransmitEnabled            = station.AprsIsTransmitEnabled && station.TransmitEnabled,
                RequireTransmitConfirmation = false
            };
            // Every transmit-capable client shares the global inhibit gate so exercise mode
            // blocks it even after a settings-triggered rebuild.
            return new AprsIsClient(clientConfig) { InhibitGate = inhibitGate };
        }
        return null;
    }

    /// <summary>
    /// Disconnects and disposes an APRS-IS client that <see cref="ApplySettings"/> is replacing,
    /// off the caller's thread. Any fault is logged, never thrown into the fire-and-forget task.
    /// </summary>
    private async Task DisposeReplacedClientAsync(IAprsIsClient client)
    {
        try
        {
            await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log?.Warning("Beacon", "Disconnecting the replaced APRS-IS client failed.", ex);
        }

        try
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log?.Warning("Beacon", "Disposing the replaced APRS-IS client failed.", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync().ConfigureAwait(false);

        if (tickLoop is not null)
        {
            try { await tickLoop.ConfigureAwait(false); }
            catch { /* suppress */ }
        }

        scheduler.Stop();

        if (aprsIsClient is not null)
        {
            try { await aprsIsClient.DisconnectAsync(CancellationToken.None).ConfigureAwait(false); }
            catch { /* suppress */ }
            await aprsIsClient.DisposeAsync().ConfigureAwait(false);
        }

        cts.Dispose();
    }

    private static LocalStationProfile ToLocalProfile(Configuration.StationProfile station)
    {
        return new LocalStationProfile(
            Callsign:               station.Callsign ?? string.Empty,
            Ssid:                   station.Ssid > 0 ? station.Ssid : null,
            FixedLatitude:          station.Latitude,
            FixedLongitude:         station.Longitude,
            SymbolTableIdentifier:  station.SymbolTable,
            SymbolCode:             station.SymbolCode,
            Overlay:                null,
            StationComment:         station.StationComment,
            PhgData:                station.PhgData,
            BeaconPath:             station.BeaconPath ?? string.Empty,
            AprsIsBeaconInterval:   TimeSpan.FromMinutes(station.AprsIsBeaconMinutes),
            RfBeaconInterval:       TimeSpan.FromMinutes(station.RfBeaconMinutes),
            FixedStationMode:       station.FixedStationMode,
            MobileStationMode:      !station.FixedStationMode,
            TransmitEnabled:        station.TransmitEnabled,
            AprsIsTransmitEnabled:  station.AprsIsTransmitEnabled,
            RfTransmitEnabled:      station.RfTransmitEnabled,
            CreatedAtUtc:           DateTimeOffset.UtcNow,
            UpdatedAtUtc:           DateTimeOffset.UtcNow);
    }
}
