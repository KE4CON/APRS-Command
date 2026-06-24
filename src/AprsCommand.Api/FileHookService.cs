using System.Globalization;
using System.Text;
using System.Text.Json;
using Aprs.Services;
using AprsCommand.Contracts;

namespace AprsCommand.Api;

public sealed class FileHookService : IFileHookService
{
    private readonly FileHookConfiguration configuration;
    private readonly ILocalRestApiDataProvider dataProvider;
    private readonly IAprsEventBus? eventBus;
    private FileHookState state = FileHookState.Stopped;
    private DateTimeOffset? lastExportTime;
    private DateTimeOffset? lastImportTime;
    private int acceptedImportCount;
    private int rejectedImportCount;
    private string? lastError;

    public FileHookService(
        FileHookConfiguration? configuration = null,
        ILocalRestApiDataProvider? dataProvider = null,
        IAprsEventBus? eventBus = null)
    {
        this.configuration = configuration ?? FileHookConfiguration.Default;
        this.dataProvider = dataProvider ?? new InMemoryLocalRestApiDataProvider();
        this.eventBus = eventBus;
    }

    public FileHookStatus Status => new(
        state,
        configuration.FileHooksEnabled,
        configuration.ImportEnabled,
        configuration.ExportEnabled,
        configuration.BaseFolderPath,
        configuration.ImportFolderPath,
        configuration.ExportFolderPath,
        lastExportTime,
        lastImportTime,
        acceptedImportCount,
        rejectedImportCount,
        lastError);

    public Task<FileHookStatus> StartAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.FileHooksEnabled)
        {
            state = FileHookState.Stopped;
            lastError = "File hooks are disabled.";
            Publish(AprsEventType.ExtensionEvent, AprsEventSeverity.Warning, lastError);
            return Task.FromResult(Status);
        }

        try
        {
            EnsureFolderStructure();
            state = FileHookState.Running;
            lastError = null;
            Publish(AprsEventType.ExtensionEvent, AprsEventSeverity.Info, "File hooks started.");
        }
        catch (Exception ex)
        {
            state = FileHookState.Faulted;
            lastError = ex.Message;
            Publish(AprsEventType.ExtensionEvent, AprsEventSeverity.Error, $"File hooks failed to start: {ex.Message}");
        }

        return Task.FromResult(Status);
    }

    public Task<FileHookStatus> StopAsync(CancellationToken cancellationToken = default)
    {
        state = FileHookState.Stopped;
        lastError = null;
        Publish(AprsEventType.ExtensionEvent, AprsEventSeverity.Info, "File hooks stopped.");
        return Task.FromResult(Status);
    }

    public void EnsureFolderStructure()
    {
        Directory.CreateDirectory(configuration.BaseFolderPath);
        Directory.CreateDirectory(configuration.ImportFolderPath);
        Directory.CreateDirectory(Path.Combine(configuration.ImportFolderPath, "stations"));
        Directory.CreateDirectory(Path.Combine(configuration.ImportFolderPath, "weather"));
        Directory.CreateDirectory(Path.Combine(configuration.ImportFolderPath, "objects"));
        Directory.CreateDirectory(Path.Combine(configuration.ImportFolderPath, "gps"));
        Directory.CreateDirectory(Path.Combine(configuration.ImportFolderPath, "raw-packets"));
        Directory.CreateDirectory(Path.Combine(configuration.ImportFolderPath, "transmit-requests"));
        Directory.CreateDirectory(Path.Combine(configuration.BaseFolderPath, "processed"));
        Directory.CreateDirectory(Path.Combine(configuration.BaseFolderPath, "rejected"));
        Directory.CreateDirectory(configuration.ExportFolderPath);
    }

    public async Task<IReadOnlyList<FileHookExportResult>> ExportAllAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.FileHooksEnabled || !configuration.ExportEnabled)
        {
            return [FileHookExportResult.Failed(FileHookExportKind.Stations, "File exports are disabled.")];
        }

        var results = new List<FileHookExportResult>();
        if (configuration.IncludeStationsExport) results.Add(await ExportAsync(FileHookExportKind.Stations, cancellationToken).ConfigureAwait(false));
        if (configuration.IncludeWeatherExport) results.Add(await ExportAsync(FileHookExportKind.Weather, cancellationToken).ConfigureAwait(false));
        if (configuration.IncludeObjectsExport) results.Add(await ExportAsync(FileHookExportKind.Objects, cancellationToken).ConfigureAwait(false));
        if (configuration.IncludeMessagesExport) results.Add(await ExportAsync(FileHookExportKind.Messages, cancellationToken).ConfigureAwait(false));
        if (configuration.IncludeAlertsExport) results.Add(await ExportAsync(FileHookExportKind.Alerts, cancellationToken).ConfigureAwait(false));
        if (configuration.IncludeRawPacketsExport) results.Add(await ExportAsync(FileHookExportKind.RawPackets, cancellationToken).ConfigureAwait(false));
        if (configuration.IncludeDecodedEventsExport) results.Add(await ExportAsync(FileHookExportKind.DecodedEvents, cancellationToken).ConfigureAwait(false));
        if (configuration.IncludeDiagnosticsExport) results.Add(await ExportAsync(FileHookExportKind.Diagnostics, cancellationToken).ConfigureAwait(false));
        return results;
    }

    public async Task<FileHookExportResult> ExportAsync(FileHookExportKind kind, CancellationToken cancellationToken = default)
    {
        if (!configuration.FileHooksEnabled || !configuration.ExportEnabled)
        {
            return FileHookExportResult.Failed(kind, "File exports are disabled.");
        }

        EnsureFolderStructure();
        var (fileName, content, count) = kind switch
        {
            FileHookExportKind.Stations => ("stations.json", ToJsonEnvelope(dataProvider.GetStations()), dataProvider.GetStations().Count),
            FileHookExportKind.Weather => ("weather.json", ToJsonEnvelope(dataProvider.GetWeather()), dataProvider.GetWeather().Count),
            FileHookExportKind.Objects => ("objects.geojson", ToGeoJson(dataProvider.GetObjects()), dataProvider.GetObjects().Count),
            FileHookExportKind.Messages => ("messages.csv", ToMessagesCsv(dataProvider.GetMessages()), dataProvider.GetMessages().Count),
            FileHookExportKind.Alerts => ("alerts.json", ToJsonEnvelope(dataProvider.GetAlerts()), dataProvider.GetAlerts().Count),
            FileHookExportKind.RawPackets => ("raw-packets.log", ToRawPacketLog(dataProvider.GetRawPackets()), dataProvider.GetRawPackets().Count),
            FileHookExportKind.DecodedEvents => ("decoded-events.json", ToJsonEnvelope(dataProvider.GetEvents()), dataProvider.GetEvents().Count),
            FileHookExportKind.Diagnostics => ("diagnostics.json", ToJsonEnvelope(dataProvider.GetRfDiagnostics()), dataProvider.GetRfDiagnostics().Count),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        if (Encoding.UTF8.GetByteCount(content) > configuration.MaximumExportFileSizeBytes)
        {
            lastError = "Export exceeds maximum configured file size.";
            Publish(AprsEventType.ExtensionEvent, AprsEventSeverity.Error, $"File export failed for {kind}: {lastError}");
            return FileHookExportResult.Failed(kind, lastError);
        }

        var path = Path.Combine(configuration.ExportFolderPath, fileName);
        await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
        lastExportTime = DateTimeOffset.UtcNow;
        lastError = null;
        Publish(AprsEventType.ExtensionEvent, AprsEventSeverity.Info, $"File export completed for {kind}.");
        return FileHookExportResult.Ok(kind, path, content, count);
    }

    public Task<FileHookImportResult> ImportAsync(FileHookImportKind kind, string content, CancellationToken cancellationToken = default)
    {
        if (!configuration.FileHooksEnabled || !configuration.ImportEnabled)
        {
            return Task.FromResult(Reject(kind, "File imports are disabled."));
        }

        if (Encoding.UTF8.GetByteCount(content) > configuration.MaximumImportFileSizeBytes)
        {
            return Task.FromResult(Reject(kind, "Import exceeds maximum configured file size."));
        }

        if (kind == FileHookImportKind.TransmitRequests)
        {
            Publish(AprsEventType.PacketTransmitBlocked, AprsEventSeverity.Warning, "Imported transmit request blocked by policy.");
            return Task.FromResult(Reject(kind, "Imported transmit requests are disabled and blocked by policy."));
        }

        if (!IsAllowedImport(kind))
        {
            return Task.FromResult(Reject(kind, $"{kind} imports are disabled."));
        }

        try
        {
            var accepted = kind switch
            {
                FileHookImportKind.Stations => ImportDtos<StationUpdateDto>(content, dataProvider.SubmitStation, AprsEventType.StationUpdated, ValidateStation),
                FileHookImportKind.Weather => ImportDtos<WeatherObservationDto>(content, dataProvider.SubmitWeather, AprsEventType.WeatherUpdated, ValidateWeather),
                FileHookImportKind.Objects => ImportObjects(content),
                FileHookImportKind.Gps => ImportDtos<GpsPositionDto>(content, dataProvider.SubmitGps, AprsEventType.GpsUpdated, ValidateGps),
                FileHookImportKind.RawPackets => ImportRawPackets(content),
                _ => 0
            };

            acceptedImportCount += accepted;
            lastImportTime = DateTimeOffset.UtcNow;
            lastError = null;
            Publish(AprsEventType.ExtensionEvent, AprsEventSeverity.Info, $"File import accepted {accepted} {kind} record(s).");
            return Task.FromResult(FileHookImportResult.Accepted(kind, accepted));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(Reject(kind, ex.Message));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(Reject(kind, $"Import JSON is malformed: {ex.Message}"));
        }
    }

    public async Task<FileHookScanResult> ScanImportFolderAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.FileHooksEnabled || !configuration.ImportEnabled)
        {
            return new FileHookScanResult(false, Error: "File imports are disabled.");
        }

        EnsureFolderStructure();
        var processed = 0;
        var accepted = 0;
        var rejected = 0;

        foreach (var (folder, kind) in IncomingFolders())
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var result = await ImportAsync(kind, content, cancellationToken).ConfigureAwait(false);
                processed++;
                accepted += result.AcceptedCount;
                rejected += result.RejectedCount;

                var targetRoot = result.Success && configuration.ArchiveProcessedImports
                    ? Path.Combine(configuration.BaseFolderPath, "processed")
                    : Path.Combine(configuration.BaseFolderPath, "rejected");
                Directory.CreateDirectory(targetRoot);
                var targetPath = Path.Combine(targetRoot, Path.GetFileName(file));
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                File.Move(file, targetPath);
            }
        }

        return new FileHookScanResult(true, processed, accepted, rejected);
    }

    public void ClearStatus()
    {
        lastExportTime = null;
        lastImportTime = null;
        acceptedImportCount = 0;
        rejectedImportCount = 0;
        lastError = null;
    }

    private int ImportDtos<TDto>(
        string content,
        Action<TDto> submit,
        AprsEventType eventType,
        Action<TDto> validate)
        where TDto : class, IContractDto
    {
        var dtos = ReadDtos<TDto>(content);
        foreach (var dto in dtos)
        {
            validate(dto);
            var tagged = (TDto)TagFileImport(dto);
            submit(tagged);
            Publish(eventType, AprsEventSeverity.Info, $"Imported {typeof(TDto).Name} from file.", tagged.SourceMetadata);
        }

        return dtos.Count;
    }

    private int ImportObjects(string content)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.Contains("\"FeatureCollection\"", StringComparison.OrdinalIgnoreCase))
        {
            var objects = ReadGeoJsonObjects(content);
            foreach (var obj in objects)
            {
                ValidateObject(obj);
                var tagged = (AprsObjectDto)TagFileImport(obj);
                dataProvider.SubmitObject(tagged);
                Publish(AprsEventType.ObjectUpdated, AprsEventSeverity.Info, "Imported AprsObjectDto from GeoJSON file.", tagged.SourceMetadata);
            }

            return objects.Count;
        }

        return ImportDtos<AprsObjectDto>(content, dataProvider.SubmitObject, AprsEventType.ObjectUpdated, ValidateObject);
    }

    private int ImportRawPackets(string content)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return ImportDtos<RawPacketDto>(content, dataProvider.SubmitRawPacket, AprsEventType.RawPacketReceived, ValidateRawPacket);
        }

        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var packet = new RawPacketDto { RawPacket = line };
            ValidateRawPacket(packet);
            var tagged = (RawPacketDto)TagFileImport(packet);
            dataProvider.SubmitRawPacket(tagged);
            Publish(AprsEventType.RawPacketReceived, AprsEventSeverity.Info, "Imported raw APRS packet from file.", tagged.SourceMetadata);
        }

        return lines.Length;
    }

    private static IReadOnlyList<TDto> ReadDtos<TDto>(string content)
        where TDto : class, IContractDto
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<TDto>>(content, ContractJsonSerializerOptions.Create())
                ?? throw new InvalidOperationException("Import array could not be read.");
        }

        if (root.TryGetProperty("data", out var dataElement))
        {
            return dataElement.Deserialize<List<TDto>>(ContractJsonSerializerOptions.Create())
                ?? throw new InvalidOperationException("Import data array could not be read.");
        }

        var dto = root.Deserialize<TDto>(ContractJsonSerializerOptions.Create())
            ?? throw new InvalidOperationException("Import record could not be read.");
        return [dto];
    }

    private static IReadOnlyList<AprsObjectDto> ReadGeoJsonObjects(string content)
    {
        using var document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("GeoJSON FeatureCollection requires a features array.");
        }

        var objects = new List<AprsObjectDto>();
        foreach (var feature in features.EnumerateArray())
        {
            var properties = feature.TryGetProperty("properties", out var props) ? props : default;
            var geometry = feature.TryGetProperty("geometry", out var geom) ? geom : default;
            double? longitude = null;
            double? latitude = null;

            if (geometry.ValueKind == JsonValueKind.Object
                && geometry.TryGetProperty("coordinates", out var coordinates)
                && coordinates.ValueKind == JsonValueKind.Array
                && coordinates.GetArrayLength() >= 2)
            {
                longitude = coordinates[0].GetDouble();
                latitude = coordinates[1].GetDouble();
            }

            objects.Add(new AprsObjectDto
            {
                ObjectName = ReadString(properties, "objectName") ?? ReadString(properties, "name"),
                ObjectType = ReadString(properties, "objectType") ?? "object",
                Latitude = latitude,
                Longitude = longitude,
                SymbolTable = ReadString(properties, "symbolTable"),
                SymbolCode = ReadString(properties, "symbolCode"),
                Comment = ReadString(properties, "comment"),
                CreatedBy = ReadString(properties, "createdBy")
            });
        }

        return objects;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private string ToJsonEnvelope<T>(IReadOnlyList<T> values)
    {
        var envelope = new FileHookExportEnvelope<T>
        {
            ExportedAt = DateTimeOffset.UtcNow,
            ItemCount = values.Count,
            Data = values
        };
        return JsonSerializer.Serialize(envelope, ContractJsonSerializerOptions.Create());
    }

    private static string ToGeoJson(IReadOnlyList<AprsObjectDto> objects)
    {
        var features = objects.Select(obj => new
        {
            type = "Feature",
            geometry = new
            {
                type = "Point",
                coordinates = new[] { obj.Longitude ?? 0, obj.Latitude ?? 0 }
            },
            properties = new
            {
                schemaVersion = obj.SchemaVersion,
                objectName = obj.ObjectName,
                objectType = obj.ObjectType,
                symbolTable = obj.SymbolTable,
                symbolCode = obj.SymbolCode,
                comment = obj.Comment,
                active = obj.Active,
                killed = obj.Killed,
                createdBy = obj.CreatedBy,
                sourceMetadata = obj.SourceMetadata
            }
        }).ToArray();

        return JsonSerializer.Serialize(new
        {
            type = "FeatureCollection",
            schemaVersion = ContractSchemaVersion.Current,
            exportedAt = DateTimeOffset.UtcNow,
            itemCount = objects.Count,
            features
        }, ContractJsonSerializerOptions.Create());
    }

    private static string ToMessagesCsv(IReadOnlyList<MessageDto> messages)
    {
        var builder = new StringBuilder();
        builder.AppendLine("schemaVersion,messageId,from,to,text,messageState,timestamp");
        foreach (var message in messages)
        {
            builder.AppendLine(string.Join(',', [
                Csv(message.SchemaVersion),
                Csv(message.MessageId),
                Csv(message.From),
                Csv(message.To),
                Csv(message.Text),
                Csv(message.MessageState),
                Csv(message.Timestamp?.ToString("O", CultureInfo.InvariantCulture))
            ]));
        }

        return builder.ToString();
    }

    private static string ToRawPacketLog(IReadOnlyList<RawPacketDto> packets)
    {
        var builder = new StringBuilder();
        foreach (var packet in packets)
        {
            var timestamp = packet.Timestamp?.ToString("O", CultureInfo.InvariantCulture)
                ?? packet.ReceivedTime?.ToString("O", CultureInfo.InvariantCulture)
                ?? string.Empty;
            builder.Append(timestamp);
            builder.Append(' ');
            builder.AppendLine(packet.RawPacket ?? string.Empty);
        }

        return builder.ToString();
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        return value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private FileHookImportResult Reject(FileHookImportKind kind, string error)
    {
        rejectedImportCount++;
        lastError = error;
        Publish(
            kind == FileHookImportKind.TransmitRequests ? AprsEventType.PacketTransmitBlocked : AprsEventType.ExtensionEvent,
            AprsEventSeverity.Warning,
            $"File import rejected: {error}");
        return FileHookImportResult.Rejected(kind, error, [new ValidationMessageDto(ValidationSeverity.Error, error)]);
    }

    private bool IsAllowedImport(FileHookImportKind kind)
    {
        return kind switch
        {
            FileHookImportKind.Stations => configuration.AllowImportedStationData,
            FileHookImportKind.Weather => configuration.AllowImportedWeatherData,
            FileHookImportKind.Objects => configuration.AllowImportedObjectData,
            FileHookImportKind.Gps => configuration.AllowImportedGpsData,
            FileHookImportKind.RawPackets => configuration.AllowImportedRawPacketData,
            FileHookImportKind.TransmitRequests => configuration.AllowImportedTransmitRequests,
            _ => false
        };
    }

    private static void ValidateStation(StationUpdateDto station)
    {
        RequireSchema(station);
        if (string.IsNullOrWhiteSpace(station.Callsign))
        {
            throw new InvalidOperationException("Station import requires callsign.");
        }
    }

    private static void ValidateWeather(WeatherObservationDto weather)
    {
        RequireSchema(weather);
        if (string.IsNullOrWhiteSpace(weather.StationId) && string.IsNullOrWhiteSpace(weather.Callsign))
        {
            throw new InvalidOperationException("Weather import requires stationId or callsign.");
        }
    }

    private static void ValidateObject(AprsObjectDto aprsObject)
    {
        RequireSchema(aprsObject);
        if (string.IsNullOrWhiteSpace(aprsObject.ObjectName))
        {
            throw new InvalidOperationException("Object import requires objectName.");
        }
    }

    private static void ValidateGps(GpsPositionDto gps)
    {
        RequireSchema(gps);
        if (gps.Latitude is < -90 or > 90 || gps.Longitude is < -180 or > 180)
        {
            throw new InvalidOperationException("GPS import has invalid latitude or longitude.");
        }
    }

    private static void ValidateRawPacket(RawPacketDto packet)
    {
        RequireSchema(packet);
        if (string.IsNullOrWhiteSpace(packet.RawPacket))
        {
            throw new InvalidOperationException("Raw packet import requires rawPacket text.");
        }

        if (packet.RawPacket.Contains('\n') || packet.RawPacket.Contains('\r'))
        {
            throw new InvalidOperationException("Raw packet import cannot contain embedded line breaks.");
        }
    }

    private static void RequireSchema(IContractDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SchemaVersion))
        {
            throw new InvalidOperationException("schemaVersion is required.");
        }

        if (!string.Equals(dto.SchemaVersion, ContractSchemaVersion.Current, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsupported schemaVersion: {dto.SchemaVersion}.");
        }

        if (dto.ValidationErrors.Count > 0)
        {
            throw new InvalidOperationException("Import record contains validation errors.");
        }
    }

    private static IContractDto TagFileImport(IContractDto dto)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var metadata = dto.SourceMetadata with
        {
            SourceName = string.IsNullOrWhiteSpace(dto.SourceMetadata.SourceName) ? "File Import" : dto.SourceMetadata.SourceName,
            SourceType = ExternalSourceType.FileImport,
            SourceId = string.IsNullOrWhiteSpace(dto.SourceMetadata.SourceId) ? "file-import" : dto.SourceMetadata.SourceId,
            Timestamp = dto.SourceMetadata.Timestamp ?? timestamp,
            Origin = ContractDataOrigin.Imported,
            TrustLevel = ExternalTrustLevel.External
        };

        return dto switch
        {
            StationUpdateDto station => station with { SourceMetadata = metadata, Timestamp = station.Timestamp ?? metadata.Timestamp },
            WeatherObservationDto weather => weather with { SourceMetadata = metadata, Timestamp = weather.Timestamp ?? metadata.Timestamp },
            AprsObjectDto aprsObject => aprsObject with { SourceMetadata = metadata, Timestamp = aprsObject.Timestamp ?? metadata.Timestamp },
            GpsPositionDto gps => gps with { SourceMetadata = metadata, Timestamp = gps.Timestamp ?? metadata.Timestamp },
            RawPacketDto raw => raw with { SourceMetadata = metadata, Timestamp = raw.Timestamp ?? metadata.Timestamp },
            _ => dto
        };
    }

    private IEnumerable<(string Folder, FileHookImportKind Kind)> IncomingFolders()
    {
        yield return (Path.Combine(configuration.ImportFolderPath, "stations"), FileHookImportKind.Stations);
        yield return (Path.Combine(configuration.ImportFolderPath, "weather"), FileHookImportKind.Weather);
        yield return (Path.Combine(configuration.ImportFolderPath, "objects"), FileHookImportKind.Objects);
        yield return (Path.Combine(configuration.ImportFolderPath, "gps"), FileHookImportKind.Gps);
        yield return (Path.Combine(configuration.ImportFolderPath, "raw-packets"), FileHookImportKind.RawPackets);
        yield return (Path.Combine(configuration.ImportFolderPath, "transmit-requests"), FileHookImportKind.TransmitRequests);
    }

    private void Publish(
        AprsEventType eventType,
        AprsEventSeverity severity,
        string summary,
        ExternalSourceMetadata? source = null)
    {
        if (eventBus is null)
        {
            return;
        }

        var timestamp = source?.Timestamp ?? DateTimeOffset.UtcNow;
        var metadata = AprsEventMetadata.Create(
            eventType,
            CategoryFor(eventType),
            timestamp,
            source ?? new ExternalSourceMetadata("File Hooks", ExternalSourceType.FileImport, "file-hooks", timestamp, ContractDataOrigin.Generated, ExternalTrustLevel.Internal),
            severity,
            summary: summary);

        eventBus.Publish(new AprsEventEnvelope<string>(metadata, summary));
    }

    private static AprsEventCategory CategoryFor(AprsEventType eventType)
    {
        return eventType switch
        {
            AprsEventType.StationUpdated or AprsEventType.StationCreated or AprsEventType.StationExpired => AprsEventCategory.Station,
            AprsEventType.WeatherUpdated => AprsEventCategory.Weather,
            AprsEventType.ObjectUpdated or AprsEventType.ObjectCreated or AprsEventType.ObjectKilled => AprsEventCategory.Object,
            AprsEventType.GpsUpdated => AprsEventCategory.GPS,
            AprsEventType.RawPacketReceived or AprsEventType.PacketTransmitBlocked => AprsEventCategory.Packet,
            _ => AprsEventCategory.Extension
        };
    }
}
