namespace Aprs.Services;

public sealed class WeatherInputDriverManager : IWeatherInputDriverManager
{
    private readonly Dictionary<string, DriverRegistration> drivers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IWeatherDisplayService weatherDisplayService;

    public WeatherInputDriverManager(IWeatherDisplayService weatherDisplayService)
    {
        this.weatherDisplayService = weatherDisplayService;
    }

    public void RegisterDriver(IWeatherInputDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        var driverId = NormalizeDriverId(driver.DriverId);

        if (drivers.ContainsKey(driverId))
        {
            throw new InvalidOperationException($"Weather driver '{driver.DriverId}' is already registered.");
        }

        driver.ObservationReceived += OnObservationReceived;
        drivers.Add(driverId, new DriverRegistration(driver));
    }

    public bool UnregisterDriver(string driverId)
    {
        var normalized = NormalizeDriverId(driverId);
        if (!drivers.Remove(normalized, out var registration))
        {
            return false;
        }

        registration.Driver.ObservationReceived -= OnObservationReceived;
        return true;
    }

    public IReadOnlyCollection<WeatherInputDriverSnapshot> GetAllDrivers()
    {
        return drivers.Values.Select(CreateSnapshot).OrderBy(driver => driver.DriverName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyCollection<WeatherInputDriverSnapshot> GetEnabledDrivers()
    {
        return GetAllDrivers().Where(driver => driver.Enabled).ToArray();
    }

    public WeatherInputDriverSnapshot? GetDriver(string driverId)
    {
        return drivers.TryGetValue(NormalizeDriverId(driverId), out var registration)
            ? CreateSnapshot(registration)
            : null;
    }

    public async Task<bool> StartDriverAsync(string driverId, CancellationToken cancellationToken = default)
    {
        if (!drivers.TryGetValue(NormalizeDriverId(driverId), out var registration) || !registration.Driver.Enabled)
        {
            return false;
        }

        await registration.Driver.StartAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> StopDriverAsync(string driverId, CancellationToken cancellationToken = default)
    {
        if (!drivers.TryGetValue(NormalizeDriverId(driverId), out var registration))
        {
            return false;
        }

        await registration.Driver.StopAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task StartEnabledDriversAsync(CancellationToken cancellationToken = default)
    {
        foreach (var registration in drivers.Values.Where(driver => driver.Driver.Enabled))
        {
            await registration.Driver.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAllDriversAsync(CancellationToken cancellationToken = default)
    {
        foreach (var registration in drivers.Values)
        {
            await registration.Driver.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnObservationReceived(object? sender, WeatherObservationReceivedEventArgs args)
    {
        if (!drivers.TryGetValue(NormalizeDriverId(args.DriverId), out var registration))
        {
            return;
        }

        var observation = ApplyStaleState(args.Observation, args.ReceivedAtUtc, registration.Driver.Configuration.StaleDataThreshold);
        var validator = new WeatherObservationValidator(registration.Driver.Configuration.StaleDataThreshold);
        var validationResult = validator.Validate(observation, args.ReceivedAtUtc);
        registration.LastObservation = observation;
        registration.LastObservationTimeUtc = args.ReceivedAtUtc;
        registration.LastValidationResult = validationResult;

        if (registration.Driver is IWeatherInputDriverStatusSink statusSink)
        {
            statusSink.SetValidationResult(validationResult);
            statusSink.SetStatus(validationResult.Warnings.Any(IsStaleWarning)
                ? WeatherInputDriverStatus.Stale
                : registration.Driver.Status is WeatherInputDriverStatus.Disabled ? WeatherInputDriverStatus.Disabled : WeatherInputDriverStatus.Running);
        }

        if (!validationResult.IsValid)
        {
            return;
        }

        weatherDisplayService.UpsertWeatherStation(ToDisplayRecord(observation, args.DriverId, args.ReceivedAtUtc));
    }

    private static WeatherStationDisplayRecord ToDisplayRecord(
        CommonWeatherObservation observation,
        string driverId,
        DateTimeOffset receivedAtUtc)
    {
        var stationId = FirstNonWhiteSpace(observation.StationDeviceId, observation.Callsign, observation.SourceName, driverId);
        var displayName = FirstNonWhiteSpace(observation.Callsign, observation.SourceName, stationId);
        var age = receivedAtUtc - observation.TimestampUtc;
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        return new WeatherStationDisplayRecord(
            stationId,
            displayName,
            MapSourceType(observation.SourceType),
            observation.Latitude,
            observation.Longitude,
            observation.WindDirectionDegrees,
            RoundNullable(observation.WindSpeedMph),
            RoundNullable(observation.WindGustMph),
            RoundNullable(observation.TemperatureFahrenheit),
            ToHundredths(observation.RainLastHourInches),
            ToHundredths(observation.RainLast24HoursInches),
            ToHundredths(observation.RainSinceMidnightInches),
            observation.HumidityPercent,
            observation.BarometricPressureMillibars,
            observation.LuminosityWattsPerSquareMeter,
            observation.UvIndex,
            ToHundredths(observation.SnowInches),
            FormatLightning(observation.LightningCount, observation.LightningDistanceMiles),
            observation.TimestampUtc,
            age,
            observation.StaleDataState,
            observation.RawSourcePayload,
            WeatherStationOrigin.LocalDriver);
    }

    private static WeatherStationSourceType MapSourceType(WeatherObservationSourceType sourceType)
    {
        return sourceType switch
        {
            WeatherObservationSourceType.AprsWeatherStation => WeatherStationSourceType.AprsWeatherStation,
            WeatherObservationSourceType.WeatherFlowTempest => WeatherStationSourceType.Tempest,
            WeatherObservationSourceType.PeetBrosUltimeter => WeatherStationSourceType.PeetBros,
            WeatherObservationSourceType.DavisWeatherLink => WeatherStationSourceType.Davis,
            WeatherObservationSourceType.AmbientWeather => WeatherStationSourceType.AmbientWeather,
            WeatherObservationSourceType.EcowittFineOffsetGw1000 => WeatherStationSourceType.EcowittFineOffsetGw1000,
            WeatherObservationSourceType.CumulusMx
                or WeatherObservationSourceType.WeeWx
                or WeatherObservationSourceType.WeatherDisplay
                or WeatherObservationSourceType.FileImport => WeatherStationSourceType.WeatherSoftwareFileImport,
            WeatherObservationSourceType.Manual => WeatherStationSourceType.Manual,
            WeatherObservationSourceType.Simulation => WeatherStationSourceType.LocalWeatherStation,
            _ => WeatherStationSourceType.Unknown
        };
    }

    private static CommonWeatherObservation ApplyStaleState(
        CommonWeatherObservation observation,
        DateTimeOffset receivedAtUtc,
        TimeSpan staleThreshold)
    {
        if (observation.TimestampUtc == default)
        {
            return observation;
        }

        var state = receivedAtUtc - observation.TimestampUtc > staleThreshold ? WeatherDataState.Stale : observation.StaleDataState;
        return observation with { StaleDataState = state };
    }

    private static WeatherInputDriverSnapshot CreateSnapshot(DriverRegistration registration)
    {
        return new WeatherInputDriverSnapshot(
            registration.Driver.DriverId,
            registration.Driver.DriverName,
            registration.Driver.DriverType,
            registration.Driver.Enabled,
            registration.Driver.Status,
            registration.LastObservation ?? registration.Driver.LastObservation,
            registration.LastObservationTimeUtc,
            registration.Driver.LastError,
            registration.LastValidationResult ?? registration.Driver.LastValidationResult,
            registration.Driver.Configuration);
    }

    private static string NormalizeDriverId(string driverId)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            throw new ArgumentException("Weather driver ID is required.", nameof(driverId));
        }

        return driverId.Trim().ToUpperInvariant();
    }

    private static string FirstNonWhiteSpace(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "UNKNOWN";
    }

    private static int? RoundNullable(double? value)
    {
        return value is null ? null : (int)Math.Round(value.Value, MidpointRounding.AwayFromZero);
    }

    private static int? ToHundredths(double? inches)
    {
        return inches is null ? null : (int)Math.Round(inches.Value * 100, MidpointRounding.AwayFromZero);
    }

    private static string? FormatLightning(int? count, double? distanceMiles)
    {
        return (count, distanceMiles) switch
        {
            (null, null) => null,
            (not null, null) => $"{count} strikes",
            (null, not null) => $"{distanceMiles:0.0} mi",
            _ => $"{count} strikes, {distanceMiles:0.0} mi"
        };
    }

    private static bool IsStaleWarning(string warning)
    {
        return warning.Contains("stale", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DriverRegistration
    {
        public DriverRegistration(IWeatherInputDriver driver)
        {
            Driver = driver;
        }

        public IWeatherInputDriver Driver { get; }

        public CommonWeatherObservation? LastObservation { get; set; }

        public DateTimeOffset? LastObservationTimeUtc { get; set; }

        public WeatherObservationValidationResult? LastValidationResult { get; set; }
    }
}
