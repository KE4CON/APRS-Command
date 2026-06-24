using Aprs.Services;
using AprsCommand.Contracts;
using ExtensionPermission = AprsCommand.Contracts.ExtensionPermission;

namespace AprsCommand.Api;

public sealed class LocalRestApiService : ILocalRestApiService
{
    private readonly LocalRestApiConfiguration configuration;
    private readonly ILocalRestApiDataProvider dataProvider;
    private readonly IAprsEventBus? eventBus;
    private LocalRestApiState state = LocalRestApiState.Stopped;
    private string? lastError;

    public LocalRestApiService(
        LocalRestApiConfiguration? configuration = null,
        ILocalRestApiDataProvider? dataProvider = null,
        IAprsEventBus? eventBus = null)
    {
        this.configuration = configuration ?? LocalRestApiConfiguration.Default;
        this.dataProvider = dataProvider ?? new InMemoryLocalRestApiDataProvider();
        this.eventBus = eventBus;
    }

    public LocalRestApiHostStatus Status => new(
        state,
        configuration.BindAddress,
        configuration.Port,
        configuration.ApiEnabled,
        configuration.LocalhostOnly,
        lastError);

    public IReadOnlyList<LocalRestApiEndpoint> Endpoints { get; } =
    [
        new("GET", "/api/health"),
        new("GET", "/api/version"),
        new("GET", "/api/stations"),
        new("GET", "/api/stations/{callsign}"),
        new("GET", "/api/objects"),
        new("GET", "/api/weather"),
        new("GET", "/api/messages"),
        new("GET", "/api/gps"),
        new("GET", "/api/ports"),
        new("GET", "/api/alerts"),
        new("GET", "/api/raw-packets"),
        new("GET", "/api/events"),
        new("GET", "/api/rf-diagnostics"),
        new("GET", "/api/replay/status"),
        new("GET", "/api/simulation/status"),
        new("GET", "/api/training/status"),
        new("POST", "/api/external/station", RequiresWritePermission: true),
        new("POST", "/api/external/weather", RequiresWritePermission: true),
        new("POST", "/api/external/object", RequiresWritePermission: true),
        new("POST", "/api/external/gps", RequiresWritePermission: true),
        new("POST", "/api/external/raw-packet", RequiresWritePermission: true),
        new("POST", "/api/transmit/queue", RequiresTransmitPermission: true)
    ];

    public Task<LocalRestApiHostStatus> StartAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.ApiEnabled)
        {
            state = LocalRestApiState.Stopped;
            lastError = "Local REST API is disabled.";
            PublishLifecycleEvent(AprsEventType.ExtensionEvent, AprsEventSeverity.Warning, "Local REST API start rejected because API is disabled.");
            return Task.FromResult(Status);
        }

        if (configuration.LocalhostOnly && !IsLoopback(configuration.BindAddress))
        {
            state = LocalRestApiState.Faulted;
            lastError = "Localhost-only API cannot bind to a non-loopback address.";
            PublishLifecycleEvent(AprsEventType.ExtensionEvent, AprsEventSeverity.Error, lastError);
            return Task.FromResult(Status);
        }

        state = LocalRestApiState.Running;
        lastError = null;
        PublishLifecycleEvent(AprsEventType.ExtensionEvent, AprsEventSeverity.Info, "Local REST API started.");
        return Task.FromResult(Status);
    }

    public Task<LocalRestApiHostStatus> StopAsync(CancellationToken cancellationToken = default)
    {
        state = LocalRestApiState.Stopped;
        lastError = null;
        PublishLifecycleEvent(AprsEventType.ExtensionEvent, AprsEventSeverity.Info, "Local REST API stopped.");
        return Task.FromResult(Status);
    }

    public Task<LocalRestApiResponse> HandleAsync(LocalRestApiRequest request, CancellationToken cancellationToken = default)
    {
        if (!configuration.ApiEnabled || state != LocalRestApiState.Running)
        {
            var disabled = LocalRestApiResponse.ErrorResponse(503, "Local REST API is not running.");
            PublishRequestEvent(AprsEventSeverity.Warning, $"API request rejected: {disabled.Error}", request);
            return Task.FromResult(disabled);
        }

        var auth = Authorize(request);
        if (!auth.Success)
        {
            PublishRequestEvent(AprsEventSeverity.Warning, $"API request rejected: {auth.Error}", request);
            return Task.FromResult(auth);
        }

        var method = request.Method.Trim().ToUpperInvariant();
        var path = NormalizePath(request.Path);

        LocalRestApiResponse response = method switch
        {
            "GET" => HandleGet(path),
            "POST" => HandlePost(path, request),
            _ => LocalRestApiResponse.ErrorResponse(405, "Method not allowed.")
        };

        PublishRequestEvent(
            response.Success ? AprsEventSeverity.Info : AprsEventSeverity.Warning,
            response.Success ? "API request accepted." : $"API request rejected: {response.Error}",
            request);
        return Task.FromResult(response);
    }

    private LocalRestApiResponse HandleGet(string path)
    {
        if (path == "/api/health")
        {
            return LocalRestApiResponse.Ok(new
            {
                status = state == LocalRestApiState.Running ? "healthy" : "stopped",
                apiEnabled = configuration.ApiEnabled,
                state = state.ToString()
            });
        }

        if (path == "/api/version")
        {
            return LocalRestApiResponse.Ok(new
            {
                app = "APRS Command",
                contractsSchemaVersion = ContractSchemaVersion.Current
            });
        }

        if (path == "/api/stations")
        {
            return LocalRestApiResponse.Ok(dataProvider.GetStations());
        }

        if (path.StartsWith("/api/stations/", StringComparison.OrdinalIgnoreCase))
        {
            var callsign = Uri.UnescapeDataString(path["/api/stations/".Length..]);
            var station = dataProvider.GetStation(callsign);
            return station is null
                ? LocalRestApiResponse.ErrorResponse(404, "Station not found.")
                : LocalRestApiResponse.Ok(station);
        }

        return path switch
        {
            "/api/objects" => LocalRestApiResponse.Ok(dataProvider.GetObjects()),
            "/api/weather" => LocalRestApiResponse.Ok(dataProvider.GetWeather()),
            "/api/messages" => LocalRestApiResponse.Ok(dataProvider.GetMessages()),
            "/api/gps" => LocalRestApiResponse.Ok(dataProvider.GetGps()),
            "/api/ports" => LocalRestApiResponse.Ok(dataProvider.GetPorts()),
            "/api/alerts" => LocalRestApiResponse.Ok(dataProvider.GetAlerts()),
            "/api/raw-packets" => LocalRestApiResponse.Ok(dataProvider.GetRawPackets()),
            "/api/events" => LocalRestApiResponse.Ok(dataProvider.GetEvents()),
            "/api/rf-diagnostics" => LocalRestApiResponse.Ok(dataProvider.GetRfDiagnostics()),
            "/api/replay/status" => LocalRestApiResponse.Ok(dataProvider.GetReplayStatus()),
            "/api/simulation/status" => LocalRestApiResponse.Ok(dataProvider.GetSimulationStatus()),
            "/api/training/status" => LocalRestApiResponse.Ok(dataProvider.GetTrainingStatus()),
            _ => LocalRestApiResponse.ErrorResponse(404, "Endpoint not found.")
        };
    }

    private LocalRestApiResponse HandlePost(string path, LocalRestApiRequest request)
    {
        if (path == "/api/transmit/queue")
        {
            PublishRequestEvent(AprsEventSeverity.Warning, "API transmit request blocked by policy.", request, AprsEventType.PacketTransmitBlocked);
            return CheckTransmitRequest(request);
        }

        var submitCheck = CheckExternalSubmit(request, path);
        if (!submitCheck.Success)
        {
            return submitCheck;
        }

        return path switch
        {
            "/api/external/station" => SubmitDto<StationUpdateDto>(request.Body, dataProvider.SubmitStation, AprsEventType.StationUpdated, "External station submitted."),
            "/api/external/weather" => SubmitDto<WeatherObservationDto>(request.Body, dataProvider.SubmitWeather, AprsEventType.WeatherUpdated, "External weather submitted."),
            "/api/external/object" => SubmitDto<AprsObjectDto>(request.Body, dataProvider.SubmitObject, AprsEventType.ObjectUpdated, "External object submitted."),
            "/api/external/gps" => SubmitDto<GpsPositionDto>(request.Body, dataProvider.SubmitGps, AprsEventType.GpsUpdated, "External GPS submitted."),
            "/api/external/raw-packet" => SubmitDto<RawPacketDto>(request.Body, dataProvider.SubmitRawPacket, AprsEventType.RawPacketReceived, "External raw packet submitted."),
            _ => LocalRestApiResponse.ErrorResponse(404, "Endpoint not found.")
        };
    }

    private LocalRestApiResponse SubmitDto<TDto>(
        object? body,
        Action<TDto> submit,
        AprsEventType eventType,
        string summary)
        where TDto : class, IContractDto
    {
        if (body is not TDto dto)
        {
            return LocalRestApiResponse.ErrorResponse(400, $"Request body must be {typeof(TDto).Name}.");
        }

        var validation = ValidateDto(dto);
        if (!validation.Success)
        {
            return validation;
        }

        var tagged = EnsureLocalApiSource(dto);
        submit((TDto)tagged);
        PublishSubmitEvent(eventType, tagged.SourceMetadata, summary);
        return LocalRestApiResponse.Created(tagged);
    }

    private LocalRestApiResponse CheckExternalSubmit(LocalRestApiRequest request, string path)
    {
        if (!configuration.AllowExternalDataSubmit)
        {
            return LocalRestApiResponse.ErrorResponse(403, "External data submit is disabled.");
        }

        if (configuration.ReadOnlyMode)
        {
            return LocalRestApiResponse.ErrorResponse(403, "API is in read-only mode.");
        }

        var requiredPermission = path == "/api/external/object"
            ? ExtensionPermission.CreateLocalObjects
            : ExtensionPermission.SubmitLocalData;

        if (!HasPermission(request, requiredPermission))
        {
            return LocalRestApiResponse.ErrorResponse(403, $"Missing required permission: {requiredPermission}.");
        }

        return LocalRestApiResponse.Ok();
    }

    private LocalRestApiResponse CheckTransmitRequest(LocalRestApiRequest request)
    {
        if (!configuration.AllowTransmitRequest)
        {
            return LocalRestApiResponse.ErrorResponse(403, "Transmit queue endpoint is disabled.");
        }

        if (!HasPermission(request, ExtensionPermission.QueuePackets))
        {
            return LocalRestApiResponse.ErrorResponse(403, "Missing required permission: QueuePackets.");
        }

        if (!HasAnyPermission(request, ExtensionPermission.TransmitAprsIs, ExtensionPermission.TransmitRf, ExtensionPermission.Admin))
        {
            return LocalRestApiResponse.ErrorResponse(403, "Missing explicit transmit permission.");
        }

        return LocalRestApiResponse.ErrorResponse(501, "Transmit queue endpoint is not implemented and remains blocked by policy.");
    }

    private LocalRestApiResponse Authorize(LocalRestApiRequest request)
    {
        if (configuration.LocalhostOnly && !IsLoopback(request.RemoteAddress))
        {
            return LocalRestApiResponse.ErrorResponse(403, "Only localhost clients are allowed.");
        }

        if (configuration.RequireToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return LocalRestApiResponse.ErrorResponse(401, "API token is required.");
            }

            if (!string.IsNullOrWhiteSpace(configuration.ApiTokenReference)
                && !string.Equals(request.Token, configuration.ApiTokenReference, StringComparison.Ordinal))
            {
                return LocalRestApiResponse.ErrorResponse(401, "API token is invalid.");
            }
        }

        return LocalRestApiResponse.Ok();
    }

    private static LocalRestApiResponse ValidateDto(IContractDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SchemaVersion))
        {
            return LocalRestApiResponse.ErrorResponse(400, "schemaVersion is required.");
        }

        if (dto.ValidationErrors.Count > 0)
        {
            return LocalRestApiResponse.ErrorResponse(400, "Submitted DTO contains validation errors.");
        }

        return LocalRestApiResponse.Ok();
    }

    private static IContractDto EnsureLocalApiSource(IContractDto dto)
    {
        var metadata = dto.SourceMetadata;
        var taggedMetadata = metadata with
        {
            SourceType = metadata.SourceType == ExternalSourceType.Unknown
                ? ExternalSourceType.LocalApi
                : metadata.SourceType,
            Origin = ContractDataOrigin.LocalApi,
            TrustLevel = ExternalTrustLevel.External,
            Timestamp = metadata.Timestamp ?? DateTimeOffset.UtcNow
        };

        return dto switch
        {
            StationUpdateDto station => station with { SourceMetadata = taggedMetadata, Timestamp = station.Timestamp ?? taggedMetadata.Timestamp },
            WeatherObservationDto weather => weather with { SourceMetadata = taggedMetadata, Timestamp = weather.Timestamp ?? taggedMetadata.Timestamp },
            AprsObjectDto aprsObject => aprsObject with { SourceMetadata = taggedMetadata, Timestamp = aprsObject.Timestamp ?? taggedMetadata.Timestamp },
            GpsPositionDto gps => gps with { SourceMetadata = taggedMetadata, Timestamp = gps.Timestamp ?? taggedMetadata.Timestamp },
            RawPacketDto raw => raw with { SourceMetadata = taggedMetadata, Timestamp = raw.Timestamp ?? taggedMetadata.Timestamp },
            _ => dto
        };
    }

    private bool HasPermission(LocalRestApiRequest request, ExtensionPermission permission)
    {
        return HasAnyPermission(request, permission, ExtensionPermission.Admin);
    }

    private static bool HasAnyPermission(LocalRestApiRequest request, params ExtensionPermission[] permissions)
    {
        var current = request.Permissions ?? ExtensionPermissionDefaults.DefaultPermissions;
        return current.Contains(ExtensionPermission.Admin) || permissions.Any(current.Contains);
    }

    private static bool IsLoopback(string address)
    {
        return string.Equals(address, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(address, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(address, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        var normalized = string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();
        if (normalized.Length > 1)
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    private void PublishLifecycleEvent(AprsEventType eventType, AprsEventSeverity severity, string summary)
    {
        PublishEvent(eventType, AprsEventCategory.Extension, severity, summary, new ExternalSourceMetadata(
            "Local REST API",
            ExternalSourceType.LocalApi,
            "local-rest-api",
            DateTimeOffset.UtcNow,
            ContractDataOrigin.Generated,
            ExternalTrustLevel.Internal));
    }

    private void PublishRequestEvent(
        AprsEventSeverity severity,
        string summary,
        LocalRestApiRequest request,
        AprsEventType eventType = AprsEventType.ExtensionEvent)
    {
        PublishEvent(eventType, AprsEventCategory.Extension, severity, summary, new ExternalSourceMetadata(
            "Local REST API",
            ExternalSourceType.LocalApi,
            request.RemoteAddress,
            DateTimeOffset.UtcNow,
            ContractDataOrigin.LocalApi,
            ExternalTrustLevel.External));
    }

    private void PublishSubmitEvent(AprsEventType eventType, ExternalSourceMetadata source, string summary)
    {
        PublishEvent(eventType, AprsEventCategory.Extension, AprsEventSeverity.Info, summary, source);
    }

    private void PublishEvent(
        AprsEventType eventType,
        AprsEventCategory category,
        AprsEventSeverity severity,
        string summary,
        ExternalSourceMetadata source)
    {
        if (eventBus is null)
        {
            return;
        }

        var timestamp = source.Timestamp ?? DateTimeOffset.UtcNow;
        var metadata = AprsEventMetadata.Create(
            eventType,
            category,
            timestamp,
            source,
            severity,
            summary: summary);

        eventBus.Publish(new AprsEventEnvelope<string>(metadata, summary));
    }
}
