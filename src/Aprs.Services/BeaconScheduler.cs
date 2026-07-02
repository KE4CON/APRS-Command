using Aprs.Transport;

namespace Aprs.Services;

public sealed class BeaconScheduler : IBeaconScheduler
{
    private readonly ILocalStationProfileService profileService;
    private readonly IAprsBeaconFormatter beaconFormatter;
    private IAprsIsClient aprsIsClient;
    private IRfBeaconTransmitClient? rfBeaconClient;
    private readonly ISmartBeaconingDecisionService smartBeaconingDecisionService;
    private readonly IBeaconSchedulerClock clock;
    private BeaconSchedulerConfiguration configuration;
    private BeaconSchedulerState state;

    public BeaconScheduler(
        ILocalStationProfileService profileService,
        IAprsBeaconFormatter beaconFormatter,
        IAprsIsClient aprsIsClient,
        BeaconSchedulerConfiguration? configuration = null,
        IBeaconSchedulerClock? clock = null,
        ISmartBeaconingDecisionService? smartBeaconingDecisionService = null,
        IRfBeaconTransmitClient? rfBeaconClient = null)
    {
        this.profileService = profileService;
        this.beaconFormatter = beaconFormatter;
        this.aprsIsClient = aprsIsClient;
        this.rfBeaconClient = rfBeaconClient;
        this.configuration = configuration ?? BeaconSchedulerConfiguration.Default;
        this.clock = clock ?? new SystemBeaconSchedulerClock();
        this.smartBeaconingDecisionService = smartBeaconingDecisionService
            ?? new SmartBeaconingDecisionService(this.configuration.SmartBeaconing);

        state = new BeaconSchedulerState(
            this.configuration.SchedulerEnabled,
            this.configuration.AprsIsBeaconEnabled,
            this.configuration.RfBeaconEnabled,
            NextAprsIsBeaconTimeUtc: null,
            NextRfBeaconTimeUtc: null,
            LastAprsIsBeaconTimeUtc: null,
            LastRfBeaconTimeUtc: null,
            LastGeneratedBeaconPacket: null,
            LastTransmitResult: null,
            LastErrorOrWarning: null,
            profileService.GetCurrentProfile());

        if (state.SchedulerEnabled)
        {
            state = CalculateNextBeaconTimes(state, this.clock.UtcNow);
        }
    }

    public BeaconSchedulerState GetState()
    {
        return state with { CurrentStationProfile = profileService.GetCurrentProfile() };
    }

    /// <summary>
    /// Replaces the APRS-IS transmit client. Call this when the operator saves new
    /// connection settings (e.g. enters a passcode for the first time).
    /// </summary>
    public void ReplaceAprsIsClient(IAprsIsClient newClient)
    {
        aprsIsClient = newClient;
    }

    /// <summary>
    /// Updates the scheduler configuration from freshly-saved settings.
    /// Call this whenever station settings change so that AprsIsBeaconEnabled,
    /// RfBeaconEnabled, and SmartBeaconing reflect the current configuration.
    /// </summary>
    public void UpdateConfiguration(BeaconSchedulerConfiguration newConfiguration)
    {
        configuration = newConfiguration;
        state = state with
        {
            AprsIsBeaconEnabled = newConfiguration.AprsIsBeaconEnabled,
            RfBeaconEnabled     = newConfiguration.RfBeaconEnabled,
            SchedulerEnabled    = newConfiguration.SchedulerEnabled,
        };
    }

    public BeaconSchedulerState Start()
    {
        configuration = configuration with { SchedulerEnabled = true };
        state = CalculateNextBeaconTimes(
            state with
            {
                SchedulerEnabled = true,
                AprsIsBeaconEnabled = configuration.AprsIsBeaconEnabled,
                RfBeaconEnabled = configuration.RfBeaconEnabled,
                CurrentStationProfile = profileService.GetCurrentProfile(),
                LastErrorOrWarning = null
            },
            clock.UtcNow);

        return state;
    }

    public BeaconSchedulerState Stop()
    {
        configuration = configuration with { SchedulerEnabled = false };
        state = state with
        {
            SchedulerEnabled = false,
            NextAprsIsBeaconTimeUtc = null,
            NextRfBeaconTimeUtc = null,
            CurrentStationProfile = profileService.GetCurrentProfile(),
            LastErrorOrWarning = "Beacon scheduler is stopped."
        };

        return state;
    }

    public async Task<BeaconNowResult> BeaconNowAsync(CancellationToken cancellationToken)
    {
        var profile = profileService.GetCurrentProfile();
        if (!state.SchedulerEnabled)
        {
            return Block("Beacon scheduler is disabled.", profile);
        }

        if (!configuration.AprsIsBeaconEnabled)
        {
            return Block("APRS-IS beaconing is disabled.", profile);
        }

        var intervalError = ValidateBeaconInterval(profile.AprsIsBeaconInterval, "APRS-IS");
        if (intervalError is not null)
        {
            return Block(intervalError, profile);
        }

        var validation = profileService.ValidateProfile(
            profile,
            new StationProfileValidationOptions(
                AprsIsTransmitConfigured: true,
                RfTransmitConfigured: false));
        if (!validation.IsValid)
        {
            return Block("Local station profile is invalid.", profile, validation.Errors);
        }

        if (string.IsNullOrWhiteSpace(profile.Callsign))
        {
            return Block("Local station profile is invalid.", profile, ["Beaconing requires a valid callsign."]);
        }

        var formatResult = beaconFormatter.FormatFixedPositionBeacon(
            beaconFormatter.CreateInputFromProfile(profile, configuration.Destination, rfPathRequired: false));
        if (!formatResult.IsSuccess || formatResult.Packet is null)
        {
            return Block("Beacon formatter failed validation.", profile, formatResult.ValidationErrors);
        }

        state = state with
        {
            LastGeneratedBeaconPacket = formatResult.Packet,
            CurrentStationProfile = profile,
            LastErrorOrWarning = null
        };

        if (!profile.TransmitEnabled)
        {
            return BlockWithPacket("Transmit is disabled.", profile, formatResult.Packet);
        }

        if (!profile.AprsIsTransmitEnabled)
        {
            return BlockWithPacket("APRS-IS transmit is disabled.", profile, formatResult.Packet);
        }

        if (aprsIsClient.State != AprsIsConnectionState.Connected)
        {
            return BlockWithPacket("APRS-IS client is not connected.", profile, formatResult.Packet);
        }

        var transmitResult = await aprsIsClient.SendRawPacketAsync(
            formatResult.Packet,
            configuration.RequireTransmitConfirmation,
            cancellationToken);

        var transmitted = transmitResult.IsSuccess;
        var message = transmitted
            ? "APRS-IS beacon transmitted."
            : transmitResult.FailureReason ?? "APRS-IS beacon transmit failed.";

        state = CalculateNextBeaconTimes(
            state with
            {
                LastAprsIsBeaconTimeUtc = transmitted ? transmitResult.TimestampUtc : state.LastAprsIsBeaconTimeUtc,
                LastTransmitResult = transmitResult,
                LastErrorOrWarning = transmitted ? null : message,
                CurrentStationProfile = profile
            },
            clock.UtcNow);

        return new BeaconNowResult(
            PacketGenerated: true,
            TransmitAttempted: true,
            Transmitted: transmitted,
            Blocked: !transmitted,
            Packet: formatResult.Packet,
            Message: message,
            TransmitResult: transmitResult,
            ValidationErrors: transmitted ? [] : [message]);
    }

    public async Task<BeaconNowResult?> TickAsync(CancellationToken cancellationToken)
    {
        BeaconNowResult? result = null;

        if (state.SchedulerEnabled && configuration.AprsIsBeaconEnabled)
        {
            var nextAprsIs = state.NextAprsIsBeaconTimeUtc;
            if (nextAprsIs is not null && clock.UtcNow >= nextAprsIs.Value)
                result = await BeaconNowAsync(cancellationToken);
        }

        if (state.SchedulerEnabled && configuration.RfBeaconEnabled && rfBeaconClient is not null)
        {
            var nextRf = state.NextRfBeaconTimeUtc;
            if (nextRf is not null && clock.UtcNow >= nextRf.Value)
                await BeaconOnRfNowAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Transmits a beacon immediately on all enabled RF paths (Serial KISS and KISS-TCP).
    /// Does not affect the APRS-IS beacon timer.
    /// </summary>
    public async Task<BeaconNowResult> BeaconOnRfNowAsync(CancellationToken cancellationToken)
    {
        if (rfBeaconClient is null)
            return Block("No RF transmit client configured.", profileService.GetCurrentProfile());

        var profile = profileService.GetCurrentProfile();

        if (!state.SchedulerEnabled || !configuration.RfBeaconEnabled)
            return Block("RF beaconing is disabled.", profile);

        if (!profile.TransmitEnabled || !profile.RfTransmitEnabled)
            return Block("RF transmit is disabled.", profile);

        var formatResult = beaconFormatter.FormatFixedPositionBeacon(
            beaconFormatter.CreateInputFromProfile(profile, configuration.Destination, rfPathRequired: true));
        if (!formatResult.IsSuccess || formatResult.Packet is null)
            return Block("RF beacon formatter failed.", profile, formatResult.ValidationErrors);

        var rfResult = await rfBeaconClient.SendBeaconAsync(formatResult.Packet, cancellationToken)
                                           .ConfigureAwait(false);

        // Update RF beacon timer
        state = CalculateNextBeaconTimes(state with
        {
            LastRfBeaconTimeUtc = rfResult.Transmitted ? DateTimeOffset.UtcNow : state.LastRfBeaconTimeUtc,
            LastErrorOrWarning  = rfResult.Transmitted ? null : rfResult.Message,
        }, clock.UtcNow);

        return rfResult;
    }

    /// <summary>Replaces the RF transmit client (called when settings are saved).</summary>
    public void ReplaceRfBeaconClient(IRfBeaconTransmitClient? client)
        => rfBeaconClient = client;

    public SmartBeaconingDecision EvaluateSmartBeaconing(MobilePositionInput currentPosition)
    {
        return smartBeaconingDecisionService.Evaluate(currentPosition);
    }

    private BeaconSchedulerState CalculateNextBeaconTimes(BeaconSchedulerState currentState, DateTimeOffset now)
    {
        var profile = profileService.GetCurrentProfile();
        var nextAprsIs = currentState.SchedulerEnabled && configuration.AprsIsBeaconEnabled
            ? now.Add(profile.AprsIsBeaconInterval)
            : (DateTimeOffset?)null;
        var nextRf = currentState.SchedulerEnabled && configuration.RfBeaconEnabled
            ? now.Add(profile.RfBeaconInterval)
            : (DateTimeOffset?)null;

        return currentState with
        {
            AprsIsBeaconEnabled = configuration.AprsIsBeaconEnabled,
            RfBeaconEnabled = configuration.RfBeaconEnabled,
            NextAprsIsBeaconTimeUtc = nextAprsIs,
            NextRfBeaconTimeUtc = nextRf,
            CurrentStationProfile = profile
        };
    }

    private string? ValidateBeaconInterval(TimeSpan interval, string transportName)
    {
        if (interval < configuration.MinimumBeaconInterval)
        {
            return $"{transportName} beacon interval is shorter than the minimum allowed interval.";
        }

        return null;
    }

    private BeaconNowResult Block(string message, LocalStationProfile profile, IReadOnlyList<string>? validationErrors = null)
    {
        state = state with
        {
            CurrentStationProfile = profile,
            LastErrorOrWarning = message
        };

        return new BeaconNowResult(
            PacketGenerated: false,
            TransmitAttempted: false,
            Transmitted: false,
            Blocked: true,
            Packet: null,
            Message: message,
            TransmitResult: null,
            ValidationErrors: validationErrors ?? [message]);
    }

    private BeaconNowResult BlockWithPacket(string message, LocalStationProfile profile, string packet)
    {
        state = state with
        {
            LastGeneratedBeaconPacket = packet,
            CurrentStationProfile = profile,
            LastErrorOrWarning = message
        };

        return new BeaconNowResult(
            PacketGenerated: true,
            TransmitAttempted: false,
            Transmitted: false,
            Blocked: true,
            Packet: packet,
            Message: message,
            TransmitResult: null,
            ValidationErrors: [message]);
    }
}
