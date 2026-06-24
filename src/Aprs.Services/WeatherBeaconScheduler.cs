using Aprs.Transport;

namespace Aprs.Services;

public sealed class WeatherBeaconScheduler : IWeatherBeaconScheduler
{
    private readonly ILocalStationProfileService profileService;
    private readonly IAprsWeatherFormatter weatherFormatter;
    private readonly IWeatherObservationSourceProvider observationSourceProvider;
    private readonly IAprsIsClient aprsIsClient;
    private readonly IRfBeaconTransmitClient? rfTransmitClient;
    private readonly IBeaconSchedulerClock clock;
    private WeatherBeaconConfiguration configuration;
    private WeatherBeaconSchedulerState state;

    public WeatherBeaconScheduler(
        ILocalStationProfileService profileService,
        IAprsWeatherFormatter weatherFormatter,
        IWeatherObservationSourceProvider observationSourceProvider,
        IAprsIsClient aprsIsClient,
        WeatherBeaconConfiguration? configuration = null,
        IBeaconSchedulerClock? clock = null,
        IRfBeaconTransmitClient? rfTransmitClient = null)
    {
        this.profileService = profileService;
        this.weatherFormatter = weatherFormatter;
        this.observationSourceProvider = observationSourceProvider;
        this.aprsIsClient = aprsIsClient;
        this.configuration = StampDefaults(configuration ?? WeatherBeaconConfiguration.Default, DateTimeOffset.UtcNow);
        this.clock = clock ?? new SystemBeaconSchedulerClock();
        this.rfTransmitClient = rfTransmitClient;

        state = new WeatherBeaconSchedulerState(
            SchedulerEnabled: this.configuration.WeatherBeaconEnabled,
            AprsIsTransmitEnabled: this.configuration.AprsIsWeatherTransmitEnabled,
            RfTransmitEnabled: this.configuration.RfWeatherTransmitEnabled,
            SelectedWeatherSourceDriverId: this.configuration.SelectedWeatherSourceDriverId,
            LastWeatherObservationTimeUtc: null,
            LastWeatherObservationSource: null,
            LastGeneratedWeatherPacket: null,
            LastTransmitResult: null,
            LastBlockedReason: null,
            LastErrorOrWarning: null,
            NextScheduledTransmitTimeUtc: this.configuration.WeatherBeaconEnabled
                ? this.clock.UtcNow.Add(this.configuration.WeatherTransmitInterval)
                : null,
            LastScheduledTransmitTimeUtc: null,
            TransmitCount: 0,
            BlockedTransmitCount: 0);
    }

    public WeatherBeaconSchedulerState GetState()
    {
        return state;
    }

    public WeatherBeaconSchedulerState Start()
    {
        configuration = configuration with { WeatherBeaconEnabled = true, UpdatedTimestampUtc = clock.UtcNow };
        state = state with
        {
            SchedulerEnabled = true,
            AprsIsTransmitEnabled = configuration.AprsIsWeatherTransmitEnabled,
            RfTransmitEnabled = configuration.RfWeatherTransmitEnabled,
            SelectedWeatherSourceDriverId = configuration.SelectedWeatherSourceDriverId,
            NextScheduledTransmitTimeUtc = clock.UtcNow.Add(configuration.WeatherTransmitInterval),
            LastBlockedReason = null,
            LastErrorOrWarning = null
        };

        return state;
    }

    public WeatherBeaconSchedulerState Stop()
    {
        configuration = configuration with { WeatherBeaconEnabled = false, UpdatedTimestampUtc = clock.UtcNow };
        state = state with
        {
            SchedulerEnabled = false,
            NextScheduledTransmitTimeUtc = null,
            LastErrorOrWarning = "Weather beacon scheduler is stopped."
        };

        return state;
    }

    public WeatherBeaconSchedulerState SelectWeatherSource(string driverId)
    {
        configuration = configuration with
        {
            SelectedWeatherSourceDriverId = string.IsNullOrWhiteSpace(driverId) ? null : driverId.Trim(),
            UpdatedTimestampUtc = clock.UtcNow
        };
        state = state with
        {
            SelectedWeatherSourceDriverId = configuration.SelectedWeatherSourceDriverId,
            LastBlockedReason = null,
            LastErrorOrWarning = null
        };

        return state;
    }

    public WeatherBeaconPreviewResult GeneratePreview(WeatherBeaconTransmitTransport? preferredTransport = null)
    {
        var source = ResolveObservation();
        if (source.Result is not null)
        {
            state = state with
            {
                LastBlockedReason = string.Join("; ", source.Result.ValidationErrors),
                LastErrorOrWarning = string.Join("; ", source.Result.ValidationErrors)
            };
            return source.Result;
        }

        var observation = source.Observation!;
        var profile = profileService.GetCurrentProfile();
        var validation = ValidateObservation(observation);
        var errors = validation.Errors.ToList();
        var warnings = validation.Warnings.ToList();
        var isStale = IsStale(observation);

        if (configuration.RejectStaleData && isStale)
        {
            errors.Add("Stale weather data cannot be transmitted.");
        }

        var pathErrors = ValidateRfPath(configuration.RfPath);
        if (preferredTransport == WeatherBeaconTransmitTransport.Rf && pathErrors.Count > 0)
        {
            errors.AddRange(pathErrors);
        }

        if (errors.Count > 0)
        {
            var failed = WeatherBeaconPreviewResult.Failed(errors.Distinct().ToArray(), warnings, isStale, source.SourceName);
            state = UpdateObservationState(observation, source.SourceName) with
            {
                LastBlockedReason = string.Join("; ", failed.ValidationErrors),
                LastErrorOrWarning = string.Join("; ", failed.ValidationErrors)
            };
            return failed;
        }

        var localProfile = ResolveFormatterProfile(profile, observation, errors);
        if (errors.Count > 0)
        {
            var failed = WeatherBeaconPreviewResult.Failed(errors.Distinct().ToArray(), warnings, isStale, source.SourceName);
            state = UpdateObservationState(observation, source.SourceName) with
            {
                LastBlockedReason = string.Join("; ", failed.ValidationErrors),
                LastErrorOrWarning = string.Join("; ", failed.ValidationErrors)
            };
            return failed;
        }

        var transport = preferredTransport
            ?? (configuration.AprsIsWeatherTransmitEnabled
                ? WeatherBeaconTransmitTransport.AprsIs
                : WeatherBeaconTransmitTransport.Rf);
        var formatResult = weatherFormatter.FormatPreview(
            observation,
            localProfile,
            CreateFormatterOptions(transport));

        var allErrors = formatResult.ValidationErrors.ToList();
        var allWarnings = warnings.Concat(formatResult.ValidationWarnings).Distinct().ToArray();
        if (!formatResult.IsSuccess || formatResult.Packet is null)
        {
            var failed = WeatherBeaconPreviewResult.Failed(allErrors, allWarnings, isStale, source.SourceName);
            state = UpdateObservationState(observation, source.SourceName) with
            {
                LastBlockedReason = string.Join("; ", failed.ValidationErrors),
                LastErrorOrWarning = string.Join("; ", failed.ValidationErrors)
            };
            return failed;
        }

        var preview = new WeatherBeaconPreviewResult(
            IsSuccess: true,
            Packet: formatResult.Packet,
            ValidationErrors: [],
            ValidationWarnings: allWarnings,
            AprsIsEligible: IsAprsIsEligible(profile, formatResult.Packet).IsEligible,
            RfEligible: IsRfEligible(profile, formatResult.Packet).IsEligible,
            IsStale: isStale,
            SelectedSourceName: source.SourceName);

        state = UpdateObservationState(observation, source.SourceName) with
        {
            LastGeneratedWeatherPacket = formatResult.Packet,
            LastBlockedReason = null,
            LastErrorOrWarning = preview.ValidationWarnings.FirstOrDefault()
        };

        return preview;
    }

    public async Task<WeatherBeaconTransmitResult> TransmitWeatherNowAsync(
        WeatherBeaconTransmitTransport destinationTransport,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.WeatherBeaconEnabled)
        {
            return Block(destinationTransport, null, "Weather beaconing is disabled.");
        }

        var intervalError = ValidateInterval();
        if (intervalError is not null)
        {
            return Block(destinationTransport, null, intervalError);
        }

        var preview = GeneratePreview(destinationTransport);
        if (!preview.IsSuccess || preview.Packet is null)
        {
            return Block(destinationTransport, preview.Packet, preview.ValidationErrors.FirstOrDefault() ?? "Weather packet preview failed validation.");
        }

        var profile = profileService.GetCurrentProfile();
        var eligibility = destinationTransport == WeatherBeaconTransmitTransport.AprsIs
            ? IsAprsIsEligible(profile, preview.Packet)
            : IsRfEligible(profile, preview.Packet);
        if (!eligibility.IsEligible)
        {
            return Block(destinationTransport, preview.Packet, eligibility.BlockReason ?? "Weather transmit is not eligible.");
        }

        WeatherBeaconTransmitResult result;
        if (destinationTransport == WeatherBeaconTransmitTransport.AprsIs)
        {
            var aprsIsResult = await aprsIsClient.SendRawPacketAsync(
                preview.Packet,
                configuration.RequireConfirmationBeforeTransmit,
                cancellationToken).ConfigureAwait(false);
            result = aprsIsResult.IsSuccess
                ? WeatherBeaconTransmitResult.Succeeded(aprsIsResult.TimestampUtc, WeatherBeaconTransmitTransport.AprsIs, preview.Packet)
                : WeatherBeaconTransmitResult.Failed(
                    aprsIsResult.TimestampUtc,
                    WeatherBeaconTransmitTransport.AprsIs,
                    preview.Packet,
                    aprsIsResult.FailureReason ?? "APRS-IS weather transmit failed.");
        }
        else
        {
            var rfResult = await rfTransmitClient!.SendBeaconAsync(preview.Packet, cancellationToken).ConfigureAwait(false);
            result = rfResult.Transmitted
                ? WeatherBeaconTransmitResult.Succeeded(clock.UtcNow, WeatherBeaconTransmitTransport.Rf, preview.Packet)
                : WeatherBeaconTransmitResult.Failed(
                    clock.UtcNow,
                    WeatherBeaconTransmitTransport.Rf,
                    preview.Packet,
                    rfResult.Message ?? "RF weather transmit failed.");
        }

        if (result.IsSuccess)
        {
            state = state with
            {
                LastTransmitResult = result,
                LastBlockedReason = null,
                LastErrorOrWarning = null,
                LastScheduledTransmitTimeUtc = result.TimestampUtc,
                NextScheduledTransmitTimeUtc = result.TimestampUtc.Add(configuration.WeatherTransmitInterval),
                TransmitCount = state.TransmitCount + 1
            };
            configuration = configuration with
            {
                LastTransmitTimestampUtc = result.TimestampUtc,
                NextTransmitTimestampUtc = state.NextScheduledTransmitTimeUtc,
                UpdatedTimestampUtc = clock.UtcNow
            };
            return result;
        }

        state = state with
        {
            LastTransmitResult = result,
            LastBlockedReason = result.FailureReason,
            LastErrorOrWarning = result.FailureReason,
            BlockedTransmitCount = state.BlockedTransmitCount + 1
        };
        return result;
    }

    public async Task<WeatherBeaconTransmitResult?> TickAsync(CancellationToken cancellationToken = default)
    {
        if (!state.SchedulerEnabled)
        {
            return null;
        }

        var next = state.NextScheduledTransmitTimeUtc;
        if (next is null || clock.UtcNow < next.Value)
        {
            return null;
        }

        if (configuration.AprsIsWeatherTransmitEnabled)
        {
            return await TransmitWeatherNowAsync(WeatherBeaconTransmitTransport.AprsIs, cancellationToken).ConfigureAwait(false);
        }

        if (configuration.RfWeatherTransmitEnabled)
        {
            return await TransmitWeatherNowAsync(WeatherBeaconTransmitTransport.Rf, cancellationToken).ConfigureAwait(false);
        }

        return Block(WeatherBeaconTransmitTransport.AprsIs, null, "No weather transmit transport is enabled.");
    }

    private static WeatherBeaconConfiguration StampDefaults(WeatherBeaconConfiguration configuration, DateTimeOffset now)
    {
        return configuration with
        {
            CreatedTimestampUtc = configuration.CreatedTimestampUtc == DateTimeOffset.MinValue ? now : configuration.CreatedTimestampUtc,
            UpdatedTimestampUtc = configuration.UpdatedTimestampUtc == DateTimeOffset.MinValue ? now : configuration.UpdatedTimestampUtc
        };
    }

    private (CommonWeatherObservation? Observation, string? SourceName, WeatherBeaconPreviewResult? Result) ResolveObservation()
    {
        if (string.IsNullOrWhiteSpace(configuration.SelectedWeatherSourceDriverId))
        {
            return (null, null, WeatherBeaconPreviewResult.Failed(["Selected weather source is missing."]));
        }

        var sourceName = observationSourceProvider.GetSourceName(configuration.SelectedWeatherSourceDriverId);
        var observation = observationSourceProvider.GetLatestObservation(configuration.SelectedWeatherSourceDriverId);
        if (observation is null)
        {
            return (null, sourceName, WeatherBeaconPreviewResult.Failed(
                ["Selected weather source has no valid observation."],
                selectedSourceName: sourceName));
        }

        return (observation, sourceName ?? observation.SourceName, null);
    }

    private WeatherObservationValidationResult ValidateObservation(CommonWeatherObservation observation)
    {
        var candidate = IsStale(observation) && configuration.RejectStaleData
            ? observation with { StaleDataState = WeatherDataState.Stale }
            : observation;
        return new WeatherObservationValidator(configuration.StaleDataThreshold).Validate(candidate, clock.UtcNow);
    }

    private bool IsStale(CommonWeatherObservation observation)
    {
        return observation.StaleDataState != WeatherDataState.Current
            || (observation.TimestampUtc != default && clock.UtcNow - observation.TimestampUtc > configuration.StaleDataThreshold);
    }

    private LocalStationProfile? ResolveFormatterProfile(
        LocalStationProfile profile,
        CommonWeatherObservation observation,
        List<string> errors)
    {
        if (configuration.IncludePosition
            && !observation.HasPosition
            && !configuration.UseLocalStationProfilePositionWhenMissing)
        {
            errors.Add("Weather source has no position and local station profile fallback is disabled.");
        }

        return profile;
    }

    private AprsWeatherFormatterOptions CreateFormatterOptions(WeatherBeaconTransmitTransport transport)
    {
        return new AprsWeatherFormatterOptions(
            configuration.AprsDestination,
            transport == WeatherBeaconTransmitTransport.Rf ? configuration.RfPath : [],
            configuration.IncludePosition,
            configuration.CommentText);
    }

    private (bool IsEligible, string? BlockReason) IsAprsIsEligible(LocalStationProfile profile, string packet)
    {
        if (!configuration.AprsIsWeatherTransmitEnabled)
        {
            return (false, "APRS-IS weather transmit is disabled.");
        }

        var validation = profileService.ValidateProfile(
            profile,
            new StationProfileValidationOptions(AprsIsTransmitConfigured: true, RfTransmitConfigured: false));
        if (!validation.IsValid)
        {
            return (false, "Local station profile is invalid.");
        }

        if (!profile.TransmitEnabled)
        {
            return (false, "Transmit is disabled.");
        }

        if (!profile.AprsIsTransmitEnabled)
        {
            return (false, "APRS-IS transmit is disabled in the local station profile.");
        }

        if (aprsIsClient.State != AprsIsConnectionState.Connected)
        {
            return (false, "APRS-IS client is disconnected.");
        }

        if (!LooksLikeAprsPacket(packet))
        {
            return (false, "Generated weather packet is malformed.");
        }

        return (true, null);
    }

    private (bool IsEligible, string? BlockReason) IsRfEligible(LocalStationProfile profile, string packet)
    {
        if (!configuration.RfWeatherTransmitEnabled)
        {
            return (false, "RF weather transmit is disabled.");
        }

        var pathErrors = ValidateRfPath(configuration.RfPath);
        if (pathErrors.Count > 0)
        {
            return (false, pathErrors[0]);
        }

        var validation = profileService.ValidateProfile(
            profile,
            new StationProfileValidationOptions(AprsIsTransmitConfigured: false, RfTransmitConfigured: true));
        if (!validation.IsValid)
        {
            return (false, "Local station profile is invalid.");
        }

        if (!profile.TransmitEnabled)
        {
            return (false, "Transmit is disabled.");
        }

        if (!profile.RfTransmitEnabled)
        {
            return (false, "RF transmit is disabled in the local station profile.");
        }

        if (rfTransmitClient is null)
        {
            return (false, "RF weather transmit client is not configured.");
        }

        if (!LooksLikeAprsPacket(packet))
        {
            return (false, "Generated weather packet is malformed.");
        }

        return (true, null);
    }

    private string? ValidateInterval()
    {
        return configuration.WeatherTransmitInterval < configuration.MinimumAllowedTransmitInterval
            ? "Weather transmit interval is shorter than the minimum allowed interval."
            : null;
    }

    private static IReadOnlyList<string> ValidateRfPath(IReadOnlyList<string> path)
    {
        var errors = new List<string>();
        if (path.Count == 0)
        {
            errors.Add("RF weather transmit requires an APRS path.");
        }

        if (path.Any(component => string.IsNullOrWhiteSpace(component)))
        {
            errors.Add("RF path cannot contain blank components.");
        }

        if (path.Any(component => component.Contains('\r') || component.Contains('\n')))
        {
            errors.Add("RF path cannot contain line breaks.");
        }

        return errors;
    }

    private static bool LooksLikeAprsPacket(string packet)
    {
        return !string.IsNullOrWhiteSpace(packet)
            && !packet.Contains('\r')
            && !packet.Contains('\n')
            && packet.Contains('>')
            && packet.Contains(':');
    }

    private WeatherBeaconTransmitResult Block(
        WeatherBeaconTransmitTransport destinationTransport,
        string? packet,
        string message)
    {
        var result = WeatherBeaconTransmitResult.Failed(clock.UtcNow, destinationTransport, packet, message);
        state = state with
        {
            LastTransmitResult = result,
            LastBlockedReason = message,
            LastErrorOrWarning = message,
            BlockedTransmitCount = state.BlockedTransmitCount + 1
        };
        return result;
    }

    private WeatherBeaconSchedulerState UpdateObservationState(
        CommonWeatherObservation observation,
        string? sourceName)
    {
        return state with
        {
            LastWeatherObservationTimeUtc = observation.TimestampUtc,
            LastWeatherObservationSource = sourceName ?? observation.SourceName
        };
    }
}
