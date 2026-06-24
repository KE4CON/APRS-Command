using System.Collections.Concurrent;
using Aprs.Services;
using AprsCommand.Contracts;

namespace AprsCommand.Api;

public sealed class WebSocketEventStreamService : IWebSocketEventStreamService
{
    private readonly WebSocketEventStreamConfiguration configuration;
    private readonly IAprsEventBus? eventBus;
    private readonly ConcurrentDictionary<string, IWebSocketEventStreamClient> clients = new(StringComparer.OrdinalIgnoreCase);
    private AprsEventSubscription? subscription;
    private WebSocketEventStreamState state = WebSocketEventStreamState.Stopped;
    private string? lastError;

    public WebSocketEventStreamService(
        WebSocketEventStreamConfiguration? configuration = null,
        IAprsEventBus? eventBus = null)
    {
        this.configuration = configuration ?? WebSocketEventStreamConfiguration.Default;
        this.eventBus = eventBus;
    }

    public WebSocketEventStreamHostStatus Status => new(
        state,
        configuration.BindAddress,
        configuration.Port,
        configuration.WebSocketEnabled,
        configuration.LocalhostOnly,
        clients.Count,
        lastError);

    public IReadOnlyList<WebSocketEventStreamEndpoint> Endpoints { get; } =
    [
        new("/ws/events", "events", "Combined APRS Command event stream."),
        new("/ws/stations", "stations", "Station update event stream."),
        new("/ws/weather", "weather", "Weather update event stream."),
        new("/ws/objects", "objects", "Object and item update event stream."),
        new("/ws/messages", "messages", "Message and bulletin event stream."),
        new("/ws/alerts", "alerts", "Alert event stream."),
        new("/ws/raw-packets", "raw-packets", "Raw APRS packet event stream."),
        new("/ws/diagnostics", "diagnostics", "RF and diagnostics event stream."),
        new("/ws/system", "system", "System and extension event stream.")
    ];

    public IReadOnlyList<IWebSocketEventStreamClient> ConnectedClients => clients.Values.ToArray();

    public Task<WebSocketEventStreamHostStatus> StartAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.WebSocketEnabled)
        {
            state = WebSocketEventStreamState.Stopped;
            lastError = "WebSocket event streams are disabled.";
            return Task.FromResult(Status);
        }

        if (configuration.LocalhostOnly && !IsLoopback(configuration.BindAddress))
        {
            state = WebSocketEventStreamState.Faulted;
            lastError = "Localhost-only WebSocket streams cannot bind to a non-loopback address.";
            return Task.FromResult(Status);
        }

        if (configuration.MaximumConnectedClients < 1)
        {
            state = WebSocketEventStreamState.Faulted;
            lastError = "Maximum connected clients must be at least 1.";
            return Task.FromResult(Status);
        }

        state = WebSocketEventStreamState.Running;
        lastError = null;
        subscription ??= eventBus?.SubscribeAll(async (evt, token) =>
        {
            await BroadcastAsync(evt, token).ConfigureAwait(false);
            return AprsEventHandlerResult.Handled;
        });

        return Task.FromResult(Status);
    }

    public async Task<WebSocketEventStreamHostStatus> StopAsync(CancellationToken cancellationToken = default)
    {
        subscription?.Dispose();
        subscription = null;

        foreach (var client in clients.Values)
        {
            await SafeDisconnectAsync(client, "WebSocket event stream stopped.", cancellationToken).ConfigureAwait(false);
        }

        clients.Clear();
        state = WebSocketEventStreamState.Stopped;
        lastError = null;
        return Status;
    }

    public async Task<WebSocketEventStreamConnectionResult> ConnectClientAsync(
        WebSocketEventStreamClientRequest request,
        IWebSocketEventStreamClient client,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.WebSocketEnabled || state != WebSocketEventStreamState.Running)
        {
            return WebSocketEventStreamConnectionResult.Rejected(503, "WebSocket event streams are not running.");
        }

        if (!Endpoints.Any(endpoint => string.Equals(endpoint.Path, NormalizePath(request.Path), StringComparison.OrdinalIgnoreCase)))
        {
            return WebSocketEventStreamConnectionResult.Rejected(404, "WebSocket endpoint not found.");
        }

        if (configuration.LocalhostOnly && !IsLoopback(request.RemoteAddress))
        {
            return WebSocketEventStreamConnectionResult.Rejected(403, "Only localhost WebSocket clients are allowed.");
        }

        if (configuration.RequireToken)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return WebSocketEventStreamConnectionResult.Rejected(401, "WebSocket token is required.");
            }

            if (!string.IsNullOrWhiteSpace(configuration.ApiTokenReference)
                && !string.Equals(request.Token, configuration.ApiTokenReference, StringComparison.Ordinal))
            {
                return WebSocketEventStreamConnectionResult.Rejected(401, "WebSocket token is invalid.");
            }
        }

        if (clients.Count >= configuration.MaximumConnectedClients)
        {
            return WebSocketEventStreamConnectionResult.Rejected(429, "Maximum WebSocket client count reached.");
        }

        client.Filter = request.Filter ?? FilterForEndpoint(request.Path);
        clients[client.ClientId] = client;
        await Task.CompletedTask.ConfigureAwait(false);
        return WebSocketEventStreamConnectionResult.Accepted(client.ClientId);
    }

    public async Task DisconnectClientAsync(string clientId, string reason, CancellationToken cancellationToken = default)
    {
        if (clients.TryRemove(clientId, out var client))
        {
            await SafeDisconnectAsync(client, reason, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<WebSocketInboundMessageResult> HandleInboundMessageAsync(
        string clientId,
        WebSocketInboundMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!clients.TryGetValue(clientId, out var client))
        {
            return WebSocketInboundMessageResult.Rejected("WebSocket client is not connected.");
        }

        var command = message.Command.Trim().ToLowerInvariant();
        switch (command)
        {
            case "ping":
                return WebSocketInboundMessageResult.Accepted("pong");
            case "subscribe":
            case "filter":
                client.Filter = message.Filter ?? WebSocketEventStreamClientFilter.Default;
                return WebSocketInboundMessageResult.Accepted("subscribed");
            case "unsubscribe":
            case "close":
                await DisconnectClientAsync(clientId, "Client requested close.", cancellationToken).ConfigureAwait(false);
                return WebSocketInboundMessageResult.Accepted("closed");
            default:
                return WebSocketInboundMessageResult.Rejected("Unknown WebSocket command. Allowed commands are ping, subscribe, filter, unsubscribe, and close.");
        }
    }

    public WebSocketEventStreamEnvelope ToEnvelope(IAprsEvent aprsEvent, string streamName = "events")
    {
        var metadata = aprsEvent.Metadata;
        var payload = MapPayload(aprsEvent);
        var dto = payload as IContractDto;

        return new WebSocketEventStreamEnvelope
        {
            SchemaVersion = ContractSchemaVersion.Current,
            Timestamp = metadata.TimestampUtc,
            StreamName = streamName,
            EventType = metadata.EventType.ToString(),
            EventCategory = metadata.EventCategory.ToString(),
            SourceMetadata = metadata.SourceMetadata,
            PayloadType = payload?.GetType().Name ?? nameof(DecodedEventDto),
            Payload = payload,
            Warnings = dto?.ValidationWarnings ?? [],
            Errors = dto?.ValidationErrors ?? []
        };
    }

    public async Task<int> BroadcastAsync(IAprsEvent aprsEvent, CancellationToken cancellationToken = default)
    {
        if (state != WebSocketEventStreamState.Running || !ShouldIncludeServerEvent(aprsEvent))
        {
            return 0;
        }

        var sent = 0;
        foreach (var client in clients.Values.ToArray())
        {
            if (!client.IsConnected)
            {
                await DisconnectClientAsync(client.ClientId, "WebSocket client disconnected.", cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (!ShouldIncludeClientEvent(aprsEvent, client.Filter))
            {
                continue;
            }

            try
            {
                var streamName = StreamNameForFilter(client.Filter, aprsEvent.Metadata.EventCategory);
                await client.SendAsync(ToEnvelope(aprsEvent, streamName), cancellationToken).ConfigureAwait(false);
                sent++;
            }
            catch (Exception ex)
            {
                lastError = $"WebSocket client send failed: {ex.Message}";
                await DisconnectClientAsync(client.ClientId, "WebSocket client send failed.", cancellationToken).ConfigureAwait(false);
            }
        }

        return sent;
    }

    private object MapPayload(IAprsEvent aprsEvent)
    {
        var existingPayload = ExtractPayload(aprsEvent);
        if (existingPayload is IContractDto contractDto)
        {
            return contractDto;
        }

        var metadata = aprsEvent.Metadata;
        return metadata.EventType switch
        {
            AprsEventType.RawPacketReceived or AprsEventType.RawPacketTransmitted or AprsEventType.AprsPacketParsed or AprsEventType.AprsPacketParseFailed
                => new RawPacketDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    RawPacket = existingPayload as string,
                    ParsedPacketType = metadata.EventType.ToString(),
                    SourceCallsign = metadata.RelatedCallsign,
                    Direction = metadata.EventType == AprsEventType.RawPacketTransmitted
                        ? ContractDirection.Transmitted
                        : ContractDirection.Received,
                    ReceivedTime = metadata.TimestampUtc,
                    Notes = metadata.Summary
                },
            AprsEventType.StationCreated or AprsEventType.StationUpdated or AprsEventType.StationExpired
                => new StationUpdateDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    Callsign = metadata.RelatedCallsign,
                    DisplayName = metadata.RelatedCallsign,
                    StatusText = metadata.Summary,
                    LastHeard = metadata.TimestampUtc,
                    Notes = metadata.Notes
                },
            AprsEventType.ObjectCreated or AprsEventType.ObjectUpdated or AprsEventType.ObjectKilled
                => new AprsObjectDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    ObjectName = metadata.RelatedObjectName,
                    Active = metadata.EventType != AprsEventType.ObjectKilled,
                    Killed = metadata.EventType == AprsEventType.ObjectKilled,
                    UpdatedTime = metadata.TimestampUtc,
                    Notes = metadata.Summary
                },
            AprsEventType.WeatherUpdated
                => new WeatherObservationDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    StationId = metadata.RelatedCallsign ?? metadata.SourceMetadata.SourceId ?? metadata.SourceMetadata.SourceName,
                    Callsign = metadata.RelatedCallsign,
                    ObservationTime = metadata.TimestampUtc,
                    Notes = metadata.Summary
                },
            AprsEventType.MessageReceived or AprsEventType.MessageSent or AprsEventType.MessageAcknowledged or AprsEventType.MessageRejected or AprsEventType.BulletinReceived
                => new MessageDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    MessageId = metadata.RelatedMessageId,
                    From = metadata.RelatedCallsign,
                    Text = metadata.Summary,
                    ReceivedTimestamp = metadata.TimestampUtc,
                    MessageState = metadata.EventType.ToString(),
                    Notes = metadata.Notes
                },
            AprsEventType.GpsUpdated
                => new GpsPositionDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    FixValid = true,
                    Notes = metadata.Summary
                },
            AprsEventType.PortConnected or AprsEventType.PortDisconnected or AprsEventType.PortError
                => new PortStatusDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    PortId = metadata.SourceMetadata.SourceId,
                    PortName = metadata.SourceMetadata.SourceName,
                    Connected = metadata.EventType == AprsEventType.PortConnected,
                    LastConnectedTime = metadata.EventType == AprsEventType.PortConnected ? metadata.TimestampUtc : null,
                    LastDisconnectedTime = metadata.EventType == AprsEventType.PortDisconnected ? metadata.TimestampUtc : null,
                    LastError = metadata.EventType == AprsEventType.PortError ? metadata.Summary : null
                },
            AprsEventType.AlertTriggered or AprsEventType.AlertAcknowledged
                => new AlertDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    AlertId = metadata.EventId.ToString("N"),
                    Severity = metadata.Severity.ToString(),
                    Summary = metadata.Summary,
                    Details = metadata.Notes,
                    TriggeredTime = metadata.TimestampUtc,
                    Acknowledged = metadata.EventType == AprsEventType.AlertAcknowledged
                },
            AprsEventType.RfDiagnosticUpdated
                => new RfDiagnosticDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    PacketId = metadata.RelatedPacketId,
                    Callsign = metadata.RelatedCallsign,
                    Notes = metadata.Summary
                },
            AprsEventType.ReplayStateChanged or AprsEventType.ReplayPacketEmitted
                => new ReplayStatusDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    ReplayState = metadata.EventType.ToString(),
                    Notes = metadata.Summary,
                    TransmitDisabled = true
                },
            AprsEventType.SimulationStateChanged or AprsEventType.SimulationPacketGenerated
                => new SimulationStatusDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    Running = metadata.EventType == AprsEventType.SimulationStateChanged,
                    Notes = metadata.Summary
                },
            AprsEventType.TrainingStateChanged or AprsEventType.TrainingScenarioStarted or AprsEventType.TrainingScenarioCompleted
                => new TrainingScenarioDto
                {
                    SourceMetadata = metadata.SourceMetadata,
                    Timestamp = metadata.TimestampUtc,
                    ScenarioId = metadata.RelatedMessageId ?? metadata.EventId.ToString("N"),
                    Name = metadata.Summary,
                    Notes = metadata.Notes
                },
            _ => ToDecodedEventDto(metadata)
        };
    }

    private static DecodedEventDto ToDecodedEventDto(AprsEventMetadata metadata)
    {
        return new DecodedEventDto
        {
            SourceMetadata = metadata.SourceMetadata,
            Timestamp = metadata.TimestampUtc,
            EventId = metadata.EventId.ToString("N"),
            EventType = metadata.EventType.ToString(),
            Category = metadata.EventCategory.ToString(),
            Severity = metadata.Severity.ToString(),
            Summary = metadata.Summary,
            Details = metadata.Notes,
            EventTime = metadata.TimestampUtc,
            RelatedCallsign = metadata.RelatedCallsign
        };
    }

    private bool ShouldIncludeServerEvent(IAprsEvent aprsEvent)
    {
        var metadata = aprsEvent.Metadata;
        return configuration.AllowedEventCategories.Contains(metadata.EventCategory)
            && configuration.AllowedEventTypes.Contains(metadata.EventType)
            && IncludeByFeatureFlag(metadata);
    }

    private bool IncludeByFeatureFlag(AprsEventMetadata metadata)
    {
        return metadata.EventCategory switch
        {
            AprsEventCategory.Packet => configuration.IncludeRawPackets || metadata.EventType is not (AprsEventType.RawPacketReceived or AprsEventType.RawPacketTransmitted),
            AprsEventCategory.Station => configuration.IncludeStationUpdates,
            AprsEventCategory.Weather => configuration.IncludeWeatherUpdates,
            AprsEventCategory.Object => configuration.IncludeObjectUpdates,
            AprsEventCategory.Message => configuration.IncludeMessageUpdates,
            AprsEventCategory.Alert => configuration.IncludeAlertUpdates,
            AprsEventCategory.Diagnostics or AprsEventCategory.RF => configuration.IncludeDiagnostics,
            _ => configuration.IncludeDecodedEvents
        };
    }

    private static bool ShouldIncludeClientEvent(IAprsEvent aprsEvent, WebSocketEventStreamClientFilter filter)
    {
        var metadata = aprsEvent.Metadata;
        if (!filter.IncludeRawPackets && metadata.EventType is AprsEventType.RawPacketReceived or AprsEventType.RawPacketTransmitted)
        {
            return false;
        }

        if (filter.EventCategories is not null && !filter.EventCategories.Contains(metadata.EventCategory))
        {
            return false;
        }

        if (filter.EventTypes is not null && !filter.EventTypes.Contains(metadata.EventType))
        {
            return false;
        }

        if (metadata.Severity < filter.MinimumSeverity)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.CallsignOrSource))
        {
            var value = filter.CallsignOrSource;
            return EqualsIgnoreCase(metadata.RelatedCallsign, value)
                || EqualsIgnoreCase(metadata.SourceMetadata.SourceName, value)
                || EqualsIgnoreCase(metadata.SourceMetadata.SourceId, value);
        }

        return true;
    }

    private static WebSocketEventStreamClientFilter FilterForEndpoint(string path)
    {
        return NormalizePath(path).ToLowerInvariant() switch
        {
            "/ws/stations" => new WebSocketEventStreamClientFilter { EventCategories = new HashSet<AprsEventCategory> { AprsEventCategory.Station } },
            "/ws/weather" => new WebSocketEventStreamClientFilter { EventCategories = new HashSet<AprsEventCategory> { AprsEventCategory.Weather } },
            "/ws/objects" => new WebSocketEventStreamClientFilter { EventCategories = new HashSet<AprsEventCategory> { AprsEventCategory.Object } },
            "/ws/messages" => new WebSocketEventStreamClientFilter { EventCategories = new HashSet<AprsEventCategory> { AprsEventCategory.Message } },
            "/ws/alerts" => new WebSocketEventStreamClientFilter { EventCategories = new HashSet<AprsEventCategory> { AprsEventCategory.Alert } },
            "/ws/raw-packets" => new WebSocketEventStreamClientFilter { EventCategories = new HashSet<AprsEventCategory> { AprsEventCategory.Packet }, IncludeRawPackets = true },
            "/ws/diagnostics" => new WebSocketEventStreamClientFilter { EventCategories = new HashSet<AprsEventCategory> { AprsEventCategory.Diagnostics, AprsEventCategory.RF } },
            "/ws/system" => new WebSocketEventStreamClientFilter { EventCategories = new HashSet<AprsEventCategory> { AprsEventCategory.System, AprsEventCategory.Extension } },
            _ => WebSocketEventStreamClientFilter.Default
        };
    }

    private static string StreamNameForFilter(WebSocketEventStreamClientFilter filter, AprsEventCategory category)
    {
        if (filter.EventCategories?.Count == 1)
        {
            return filter.EventCategories.Single().ToString().ToLowerInvariant();
        }

        return category.ToString().ToLowerInvariant();
    }

    private static object? ExtractPayload(IAprsEvent aprsEvent)
    {
        return aprsEvent.GetType().GetProperty("Payload")?.GetValue(aprsEvent);
    }

    private static bool EqualsIgnoreCase(string? left, string? right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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

    private static async ValueTask SafeDisconnectAsync(
        IWebSocketEventStreamClient client,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            await client.DisconnectAsync(reason, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // A failing client must not take down the event stream service.
        }
    }
}
