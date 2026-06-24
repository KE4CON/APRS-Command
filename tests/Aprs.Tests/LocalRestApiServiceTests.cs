using Aprs.Services;
using AprsCommand.Api;
using AprsCommand.Contracts;
using Xunit;
using ExtensionPermission = AprsCommand.Contracts.ExtensionPermission;

namespace Aprs.Tests;

public class LocalRestApiServiceTests
{
    private const string Token = "test-token";

    [Fact]
    public void ConfigurationDefaultsAreSafe()
    {
        var configuration = LocalRestApiConfiguration.Default;

        Assert.False(configuration.ApiEnabled);
        Assert.True(configuration.LocalhostOnly);
        Assert.Equal("127.0.0.1", configuration.BindAddress);
        Assert.True(configuration.RequireToken);
        Assert.True(configuration.ReadOnlyMode);
        Assert.False(configuration.AllowExternalDataSubmit);
        Assert.False(configuration.AllowTransmitRequest);
    }

    [Fact]
    public async Task StartDoesNotRunWhenApiDisabledByDefault()
    {
        var service = new LocalRestApiService();

        var status = await service.StartAsync();

        Assert.Equal(LocalRestApiState.Stopped, status.State);
        Assert.Contains("disabled", status.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthEndpointReturnsHealthyWhenApiIsRunning()
    {
        var service = CreateService();
        await service.StartAsync();

        var response = await service.HandleAsync(Get("/api/health"));

        Assert.True(response.Success);
        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Body);
    }

    [Fact]
    public async Task VersionEndpointReportsAprsCommandApplicationName()
    {
        var service = CreateService();
        await service.StartAsync();

        var response = await service.HandleAsync(Get("/api/version"));

        Assert.True(response.Success);
        Assert.NotNull(response.Body);
        var app = response.Body.GetType().GetProperty("app")?.GetValue(response.Body);
        Assert.Equal("APRS Command", app);
    }

    [Fact]
    public async Task ReadEndpointReturnsSampleStationDto()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        provider.SeedStations(new StationUpdateDto
        {
            Callsign = "N0CALL",
            TacticalLabel = "Net Control",
            SourceMetadata = Source(ExternalSourceType.Simulation)
        });
        var service = CreateService(provider: provider);
        await service.StartAsync();

        var response = await service.HandleAsync(Get("/api/stations"));

        var stations = Assert.IsAssignableFrom<IReadOnlyList<StationUpdateDto>>(response.Body);
        var station = Assert.Single(stations);
        Assert.Equal("N0CALL", station.Callsign);
    }

    [Fact]
    public async Task MissingOrInvalidTokenIsRejected()
    {
        var service = CreateService();
        await service.StartAsync();

        var missing = await service.HandleAsync(Get("/api/health") with { Token = null });
        var invalid = await service.HandleAsync(Get("/api/health") with { Token = "wrong" });

        Assert.Equal(401, missing.StatusCode);
        Assert.Equal(401, invalid.StatusCode);
    }

    [Fact]
    public async Task ExternalSubmitRejectedWhenReadOnly()
    {
        var service = CreateService(configuration: EnabledConfiguration() with
        {
            AllowExternalDataSubmit = true,
            ReadOnlyMode = true
        });
        await service.StartAsync();

        var response = await service.HandleAsync(Post(
            "/api/external/station",
            new StationUpdateDto { Callsign = "N0CALL" },
            [ExtensionPermission.SubmitLocalData]));

        Assert.Equal(403, response.StatusCode);
        Assert.Contains("read-only", response.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExternalSubmitRejectedWhenDisabled()
    {
        var service = CreateService(configuration: EnabledConfiguration() with
        {
            AllowExternalDataSubmit = false,
            ReadOnlyMode = false
        });
        await service.StartAsync();

        var response = await service.HandleAsync(Post(
            "/api/external/weather",
            new WeatherObservationDto { StationId = "WX9XYZ" },
            [ExtensionPermission.SubmitLocalData]));

        Assert.Equal(403, response.StatusCode);
        Assert.Contains("disabled", response.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExternalStationSubmitAcceptedOnlyWithPermission()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        var service = CreateService(
            configuration: EnabledConfiguration() with { AllowExternalDataSubmit = true, ReadOnlyMode = false },
            provider: provider);
        await service.StartAsync();

        var missingPermission = await service.HandleAsync(Post(
            "/api/external/station",
            new StationUpdateDto { Callsign = "N0CALL" },
            [ExtensionPermission.ReadOnly]));
        var accepted = await service.HandleAsync(Post(
            "/api/external/station",
            new StationUpdateDto { Callsign = "N0CALL" },
            [ExtensionPermission.SubmitLocalData]));

        Assert.Equal(403, missingPermission.StatusCode);
        Assert.Equal(201, accepted.StatusCode);
        var station = Assert.Single(provider.GetStations());
        Assert.Equal("N0CALL", station.Callsign);
        Assert.Equal(ExternalSourceType.LocalApi, station.SourceMetadata.SourceType);
        Assert.Equal(ContractDataOrigin.LocalApi, station.SourceMetadata.Origin);
    }

    [Fact]
    public async Task ExternalWeatherSubmitAcceptedOnlyWithPermission()
    {
        var provider = new InMemoryLocalRestApiDataProvider();
        var service = CreateService(
            configuration: EnabledConfiguration() with { AllowExternalDataSubmit = true, ReadOnlyMode = false },
            provider: provider);
        await service.StartAsync();

        var response = await service.HandleAsync(Post(
            "/api/external/weather",
            new WeatherObservationDto { StationId = "WX9XYZ", Temperature = 72 },
            [ExtensionPermission.SubmitLocalData]));

        Assert.Equal(201, response.StatusCode);
        var observation = Assert.Single(provider.GetWeather());
        Assert.Equal("WX9XYZ", observation.StationId);
        Assert.Equal(ExternalSourceType.LocalApi, observation.SourceMetadata.SourceType);
    }

    [Fact]
    public async Task TransmitQueueEndpointIsBlockedByDefaultAndDoesNotTransmit()
    {
        var transmit = new FakeTransmitServices();
        var service = CreateService();
        await service.StartAsync();

        var response = await service.HandleAsync(Post(
            "/api/transmit/queue",
            new RawPacketDto { RawPacket = "N0CALL>APRS:>Test" },
            [ExtensionPermission.QueuePackets, ExtensionPermission.TransmitAprsIs]));

        Assert.Equal(403, response.StatusCode);
        Assert.Equal(0, transmit.AprsIsTransmitCalls);
        Assert.Equal(0, transmit.RfTransmitCalls);
    }

    [Fact]
    public async Task EventBusReceivesRequestRejectedAndSubmitEvents()
    {
        var bus = new AprsEventBus();
        var service = CreateService(
            configuration: EnabledConfiguration() with { AllowExternalDataSubmit = true, ReadOnlyMode = false },
            eventBus: bus);
        await service.StartAsync();

        await service.HandleAsync(Get("/api/health") with { Token = "wrong" });
        await service.HandleAsync(Post(
            "/api/external/raw-packet",
            new RawPacketDto { RawPacket = "N0CALL>APRS:>External" },
            [ExtensionPermission.SubmitLocalData]));

        var events = bus.GetRecentEvents();
        Assert.Contains(events, evt => evt.Metadata.Summary?.Contains("rejected", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(events, evt => evt.Metadata.EventType == AprsEventType.RawPacketReceived);
    }

    [Fact]
    public async Task NonLocalhostClientRejectedByDefault()
    {
        var service = CreateService();
        await service.StartAsync();

        var response = await service.HandleAsync(Get("/api/health") with { RemoteAddress = "192.0.2.1" });

        Assert.Equal(403, response.StatusCode);
        Assert.Contains("localhost", response.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static LocalRestApiService CreateService(
        LocalRestApiConfiguration? configuration = null,
        InMemoryLocalRestApiDataProvider? provider = null,
        IAprsEventBus? eventBus = null)
    {
        return new LocalRestApiService(configuration ?? EnabledConfiguration(), provider, eventBus);
    }

    private static LocalRestApiConfiguration EnabledConfiguration()
    {
        return LocalRestApiConfiguration.Default with
        {
            ApiEnabled = true,
            ApiTokenReference = Token
        };
    }

    private static LocalRestApiRequest Get(string path)
    {
        return new LocalRestApiRequest("GET", path, Token: Token, Permissions: ExtensionPermissionDefaults.DefaultPermissions);
    }

    private static LocalRestApiRequest Post(string path, object body, IReadOnlyList<ExtensionPermission> permissions)
    {
        return new LocalRestApiRequest("POST", path, body, Token, permissions);
    }

    private static ExternalSourceMetadata Source(ExternalSourceType sourceType)
    {
        return new ExternalSourceMetadata("test", sourceType, "test", DateTimeOffset.UtcNow, ContractDataOrigin.Simulated, ExternalTrustLevel.Internal);
    }

    private sealed class FakeTransmitServices
    {
        public int AprsIsTransmitCalls { get; private set; }
        public int RfTransmitCalls { get; private set; }
    }
}
