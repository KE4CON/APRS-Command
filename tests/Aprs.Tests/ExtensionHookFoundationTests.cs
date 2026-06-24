using Aprs.Services;
using AprsCommand.Contracts;
using Xunit;
using ServiceExtensionPermission = Aprs.Services.ExtensionPermission;

namespace Aprs.Tests;

public class ExtensionHookFoundationTests
{
    [Fact]
    public void PublicDtosCanBeInstantiatedWithSafeContractMetadata()
    {
        var source = new ExternalSourceMetadata(
            "APRS-IS",
            ExternalSourceType.AprsIs,
            "aprs-is",
            DateTimeOffset.UtcNow,
            ContractDataOrigin.Received,
            ExternalTrustLevel.OperatorConfigured);

        var station = new StationUpdateDto { SourceMetadata = source, Callsign = "N0CALL", DisplayName = "Net Control" };
        var weather = new WeatherObservationDto { SourceMetadata = source, StationId = "WX9XYZ", Temperature = 72 };
        var aprsObject = new AprsObjectDto { SourceMetadata = source, ObjectName = "CHECKPNT1", CreatedBy = "OBJ1" };
        var gps = new GpsPositionDto { SourceMetadata = source, Latitude = 39.058333, Longitude = -84.508333, FixValid = true };
        var raw = new RawPacketDto { SourceMetadata = source, RawPacket = "N0CALL>APRS:>Test" };
        var message = new MessageDto { SourceMetadata = source, From = "K8ABC", To = "N0CALL", Text = "Hello" };
        var port = new PortStatusDto { SourceMetadata = source, PortId = "aprs-is", PortName = "APRS-IS" };
        var alert = new AlertDto { SourceMetadata = source, AlertId = "alert-1", Summary = "Station heard" };
        var transmit = new TransmitLogDto { SourceMetadata = source, PacketText = "N0CALL>APRS:>Test", Allowed = false };

        Assert.Equal(PublicContractDefaults.SchemaVersion, station.SchemaVersion);
        Assert.Equal(ExternalSourceType.AprsIs, station.SourceMetadata.SourceType);
        Assert.Equal("WX9XYZ", weather.StationId);
        Assert.Equal("CHECKPNT1", aprsObject.ObjectName);
        Assert.True(gps.FixValid);
        Assert.Equal("N0CALL>APRS:>Test", raw.RawPacket);
        Assert.Equal("Hello", message.Text);
        Assert.Equal("aprs-is", port.PortId);
        Assert.Equal("Station heard", alert.Summary);
        Assert.False(transmit.Allowed);
    }

    [Fact]
    public void SourceMetadataDefaultsToUnknownAndUntrusted()
    {
        var now = DateTimeOffset.UtcNow;

        var source = SourceMetadata.Unknown(now);

        Assert.Null(source.SourceName);
        Assert.Equal(DataSourceType.Unknown, source.SourceType);
        Assert.Equal(DataOrigin.Unknown, source.Origin);
        Assert.Equal(SourceTrustLevel.Untrusted, source.TrustLevel);
        Assert.Equal(now, source.TimestampUtc);
    }

    [Fact]
    public void SourceMetadataCreateNormalizesBlankFields()
    {
        var now = DateTimeOffset.UtcNow;

        var source = SourceMetadata.Create("  Training  ", DataSourceType.Training, "  scenario-1  ", now, DataOrigin.Training, SourceTrustLevel.Internal);

        Assert.Equal("Training", source.SourceName);
        Assert.Equal("scenario-1", source.SourceId);
        Assert.Equal(DataSourceType.Training, source.SourceType);
        Assert.Equal(SourceTrustLevel.Internal, source.TrustLevel);
    }

    [Fact]
    public void DefaultExtensionPermissionIsReadOnly()
    {
        var permissions = ExtensionPermissionSet.Default;

        Assert.True(permissions.HasPermission(ServiceExtensionPermission.ReadOnly));
        Assert.False(permissions.HasPermission(ServiceExtensionPermission.SubmitLocalData));
        Assert.False(permissions.HasTransmitPermission);
    }

    [Theory]
    [InlineData(ServiceExtensionPermission.TransmitAprsIs)]
    [InlineData(ServiceExtensionPermission.TransmitRf)]
    [InlineData(ServiceExtensionPermission.QueuePackets)]
    public void TransmitRelatedPermissionsAreNotEnabledByDefault(ServiceExtensionPermission permission)
    {
        Assert.False(ExtensionPermissionSet.Default.HasPermission(permission));
    }

    [Fact]
    public void EventBusPublishesToMatchingSubscriber()
    {
        var bus = new ApplicationEventBus();
        var now = DateTimeOffset.UtcNow;
        ApplicationEvent? received = null;

        using var subscription = bus.Subscribe(ApplicationEventType.StationUpdated, evt => received = evt);
        var applicationEvent = ApplicationEvent.Create(
            ApplicationEventType.StationUpdated,
            now,
            SourceMetadata.Create("Simulation", DataSourceType.Simulation, "sim", now, DataOrigin.Simulated),
            "N0CALL",
            "Station updated");

        bus.Publish(applicationEvent);

        Assert.Same(applicationEvent, received);
    }

    [Fact]
    public void EventBusCanPublishWithNoSubscribers()
    {
        var bus = new ApplicationEventBus();
        var now = DateTimeOffset.UtcNow;

        bus.Publish(ApplicationEvent.Create(ApplicationEventType.RawPacketReceived, now));
    }

    [Fact]
    public void EventBusSubscriptionCanBeDisposed()
    {
        var bus = new ApplicationEventBus();
        var count = 0;
        var now = DateTimeOffset.UtcNow;

        var subscription = bus.Subscribe(ApplicationEventType.AlertTriggered, _ => count++);
        subscription.Dispose();

        bus.Publish(ApplicationEvent.Create(ApplicationEventType.AlertTriggered, now));

        Assert.Equal(0, count);
    }
}
