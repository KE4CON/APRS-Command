using System.Text.Json;
using AprsCommand.Contracts;
using Xunit;

namespace Aprs.Tests;

public class PublicDataContractsTests
{
    private static readonly JsonSerializerOptions JsonOptions = ContractJsonSerializerOptions.Create();

    [Fact]
    public void DefaultSourceMetadataIsUnknownAndUntrusted()
    {
        var source = new ExternalSourceMetadata();

        Assert.Equal(ExternalSourceType.Unknown, source.SourceType);
        Assert.Equal(ExternalTrustLevel.Untrusted, source.TrustLevel);
        Assert.Equal(ContractDataOrigin.Unknown, source.Origin);
    }

    [Fact]
    public void DefaultExtensionPermissionIsReadOnlyAndNotTransmit()
    {
        Assert.Equal([ExtensionPermission.ReadOnly], ExtensionPermissionDefaults.DefaultPermissions);
        Assert.False(ExtensionPermissionDefaults.IncludesTransmitPermission(ExtensionPermissionDefaults.DefaultPermissions));
        Assert.False(ExtensionPermissionDefaults.IncludesTransmitPermission([ExtensionPermission.QueuePackets]));
    }

    [Fact]
    public void ValidationMessagesCanStoreWarningsAndErrors()
    {
        var dto = new RawPacketDto
        {
            RawPacket = "BADPACKET",
            ValidationWarnings = [new ValidationMessageDto(ValidationSeverity.Warning, "Suspicious path", "path")],
            ValidationErrors = [new ValidationMessageDto(ValidationSeverity.Error, "Missing separator", "format", "rawPacket")]
        };

        Assert.Single(dto.ValidationWarnings);
        Assert.Single(dto.ValidationErrors);
        Assert.Equal("Missing separator", dto.ValidationErrors[0].Message);
    }

    [Fact]
    public void StationUpdateDtoSerializesAndDeserializes()
    {
        var dto = new StationUpdateDto
        {
            SourceMetadata = Source("APRS-IS", ExternalSourceType.AprsIs),
            Callsign = "N0CALL",
            TacticalLabel = "Net Control",
            Latitude = 39.058333,
            Longitude = -84.508333,
            Altitude = 789,
            SymbolTable = "/",
            SymbolCode = "-",
            Course = 123,
            Speed = 45,
            StatusText = "Test station",
            LastHeard = DateTimeOffset.Parse("2026-06-10T09:27:51Z")
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<StationUpdateDto>(json, JsonOptions);

        Assert.Contains("\"schemaVersion\":\"1.0\"", json);
        Assert.Contains("\"callsign\":\"N0CALL\"", json);
        Assert.Equal("N0CALL", roundTrip?.Callsign);
        Assert.Equal(ExternalSourceType.AprsIs, roundTrip?.SourceMetadata.SourceType);
    }

    [Fact]
    public void WeatherObservationDtoSerializesAndDeserializes()
    {
        var dto = new WeatherObservationDto
        {
            SourceMetadata = Source("Tempest", ExternalSourceType.WeatherDriver),
            StationId = "WX9XYZ",
            Callsign = "WX9XYZ",
            Latitude = 39.05,
            Longitude = -84.50,
            Temperature = 72,
            Humidity = 50,
            Pressure = 1013.2,
            WindDirection = 180,
            WindSpeed = 5,
            WindGust = 10,
            RainLastHour = 0,
            RainLast24Hours = 0.12,
            RainSinceMidnight = 0.18,
            ObservationTime = DateTimeOffset.Parse("2026-06-10T09:27:51Z")
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<WeatherObservationDto>(json, JsonOptions);

        Assert.Contains("\"stationId\":\"WX9XYZ\"", json);
        Assert.Contains("\"sourceType\":\"WeatherDriver\"", json);
        Assert.Equal(72, roundTrip?.Temperature);
        Assert.Equal(ExternalSourceType.WeatherDriver, roundTrip?.SourceMetadata.SourceType);
    }

    [Fact]
    public void RawPacketDtoSerializesAndDeserializes()
    {
        var dto = new RawPacketDto
        {
            SourceMetadata = Source("RF", ExternalSourceType.Rf),
            RawPacket = "N0CALL>APRS,WIDE1-1:>Hello",
            ParsedPacketType = "Status",
            SourceCallsign = "N0CALL",
            Destination = "APRS",
            Path = ["WIDE1-1"],
            Direction = ContractDirection.Received,
            ReceivedTime = DateTimeOffset.Parse("2026-06-10T09:27:51Z")
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<RawPacketDto>(json, JsonOptions);

        Assert.Contains("\"rawPacket\":\"N0CALL>APRS,WIDE1-1:>Hello\"", json);
        Assert.Contains("\"direction\":\"Received\"", json);
        Assert.Equal("N0CALL", roundTrip?.SourceCallsign);
        Assert.Equal(ContractDirection.Received, roundTrip?.Direction);
    }

    [Fact]
    public void AdditionalDtosCanBeInstantiated()
    {
        var source = Source("Simulation", ExternalSourceType.Simulation);
        IContractDto[] dtos =
        [
            new AprsObjectDto { SourceMetadata = source, ObjectName = "CHECKPNT1", ObjectType = "Object", Active = true },
            new GpsPositionDto { SourceMetadata = source, Latitude = 39.05, Longitude = -84.50, FixValid = true },
            new MessageDto { SourceMetadata = source, MessageId = "01", From = "K8ABC", To = "N0CALL", Text = "Hello", AckRequested = true },
            new PortStatusDto { SourceMetadata = source, PortId = "tcp", PortName = "TCP KISS", Connected = true },
            new AlertDto { SourceMetadata = source, AlertId = "alert-1", RuleId = "rule-1", Summary = "Station entered area" },
            new TransmitLogDto { SourceMetadata = source, TransmitId = "tx-1", PacketText = "N0CALL>APRS:>Blocked", PermissionUsed = ExtensionPermission.ReadOnly, Allowed = false },
            new DecodedEventDto { SourceMetadata = source, EventId = "evt-1", EventType = "StationUpdated", Summary = "Station updated" },
            new RfDiagnosticDto { SourceMetadata = source, PacketId = "pkt-1", Callsign = "N0CALL", SeenOnRf = true },
            new GeofenceDto { SourceMetadata = source, GeofenceId = "geo-1", Name = "Test", Type = "Circle", CenterLatitude = 39, CenterLongitude = -84, Radius = 1000 },
            new TrainingScenarioDto { SourceMetadata = source, ScenarioId = "training-1", Name = "Map basics", Tasks = [new TrainingScenarioTaskDto("task-1", "Find station")] },
            new SimulationStatusDto { SourceMetadata = source, Enabled = true, Running = true, SimulatedStationCount = 3 },
            new ReplayStatusDto { SourceMetadata = source, Enabled = true, ReplayState = "Running", TotalEntries = 10, CurrentPosition = 2 }
        ];

        Assert.All(dtos, dto => Assert.Equal(ContractSchemaVersion.Current, dto.SchemaVersion));
        Assert.All(dtos, dto => Assert.Equal(ExternalSourceType.Simulation, dto.SourceMetadata.SourceType));
    }

    private static ExternalSourceMetadata Source(string name, ExternalSourceType type)
    {
        return new ExternalSourceMetadata(
            name,
            type,
            name.ToLowerInvariant(),
            DateTimeOffset.Parse("2026-06-10T09:27:51Z"),
            ContractDataOrigin.Received,
            ExternalTrustLevel.OperatorConfigured);
    }
}
