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
    private IAprsIsClient? aprsIsClient;

    /// <summary>The transmit-capable APRS-IS client, if one is configured. Used by the message ACK coordinator.</summary>
    public IAprsIsClient? AprsIsClient => aprsIsClient;
    private readonly CancellationTokenSource cts = new();
    private Task? tickLoop;

    public BeaconService(
        LocalStationProfileService profileService,
        BeaconScheduler scheduler,
        IAprsIsClient? aprsIsClient)
    {
        this.profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        this.aprsIsClient = aprsIsClient;
    }

    /// <summary>Creates a fully wired BeaconService from the persisted station settings.</summary>
    public static BeaconService CreateFromSettings(Configuration.AppSettings settings)
    {
        var station = settings.Station;
        var profileService = new LocalStationProfileService();

        // Push the persisted station profile into the service layer.
        var profile = ToLocalProfile(station);
        profileService.UpdateProfile(profile, DateTimeOffset.UtcNow);

        // Build an APRS-IS client that can transmit when a real passcode is configured.
        var aprsIsClient = BuildAprsIsClient(settings);

        var schedulerConfig = new BeaconSchedulerConfiguration(
            SchedulerEnabled:        station.TransmitEnabled,
            AprsIsBeaconEnabled:     station.AprsIsTransmitEnabled,
            RfBeaconEnabled:         station.RfTransmitEnabled,
            MinimumBeaconInterval:   TimeSpan.FromMinutes(5),
            Destination:             "APRS",
            RequireTransmitConfirmation: false,
            SmartBeaconing:          settings.SmartBeaconing.ToServiceConfig());

        var beaconFormatter = new AprsBeaconFormatter();
        IAprsIsClient clientForScheduler = aprsIsClient ?? (IAprsIsClient)new NullAprsIsClient();
        var scheduler = new BeaconScheduler(
            profileService,
            beaconFormatter,
            clientForScheduler,
            schedulerConfig);

        return new BeaconService(profileService, scheduler, aprsIsClient);
    }

    /// <summary>
    /// Updates the live profile service from freshly-saved settings so beacon content
    /// reflects the latest station configuration immediately.
    /// </summary>
    public void ApplySettings(Configuration.AppSettings settings)
    {
        var station = settings.Station;
        profileService.UpdateProfile(ToLocalProfile(station), DateTimeOffset.UtcNow);

        // Rebuild the APRS-IS transmit client if the connection configuration changed
        // (e.g. a passcode was just entered). Disconnect the old client first.
        if (aprsIsClient is not null)
        {
            _ = aprsIsClient.DisconnectAsync(CancellationToken.None);
        }

        var newClient = BuildAprsIsClient(settings);
        aprsIsClient  = newClient;

        // Re-wire the scheduler to use the new client.
        scheduler.ReplaceAprsIsClient(newClient ?? new NullAprsIsClient());

        // If transmit is enabled, connect the new client immediately.
        if (newClient is not null && station.TransmitEnabled && station.AprsIsTransmitEnabled)
        {
            _ = newClient.ConnectAsync(cts.Token);
        }

        // Refresh scheduler configuration for transmit flags and intervals.
        scheduler.UpdateConfiguration(new Aprs.Services.BeaconSchedulerConfiguration(
            SchedulerEnabled:            station.TransmitEnabled,
            AprsIsBeaconEnabled:         station.AprsIsTransmitEnabled,
            RfBeaconEnabled:             station.RfTransmitEnabled,
            MinimumBeaconInterval:       TimeSpan.FromMinutes(5),
            Destination:                 "APRS",
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
                catch
                {
                    // Never crash the tick loop; log it later when we have a logging service.
                    await Task.Delay(TimeSpan.FromSeconds(30), cts.Token).ConfigureAwait(false);
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

    /// <summary>Current scheduler state — for the status display.</summary>
    public BeaconSchedulerState GetState() => scheduler.GetState();

    /// <summary>
    /// Builds a transmit-capable APRS-IS client from settings, or returns null if no
    /// APRS-IS port with a real passcode is configured.
    /// </summary>
    private static IAprsIsClient? BuildAprsIsClient(Configuration.AppSettings settings)
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
            return new AprsIsClient(clientConfig);
        }
        return null;
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
