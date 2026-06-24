using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class DecodedEventLogServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StationEventCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddStationEvent(DecodedEventType.StationCreated, "N0CALL", "Station N0CALL created.", AprsPacketSource.AprsIs);

        Assert.NotNull(entry);
        Assert.Equal(DecodedEventCategory.Station, entry.EventCategory);
        Assert.Equal("N0CALL", entry.SourceCallsign);
        Assert.Equal(AprsPacketSource.AprsIs, entry.PacketSource);
    }

    [Fact]
    public void ObjectEventCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddObjectEvent(DecodedEventType.ObjectUpdated, "CHECKPNT1", "Object updated.", AprsPacketSource.Rf, "NETCTRL");

        Assert.NotNull(entry);
        Assert.Equal(DecodedEventCategory.Object, entry.EventCategory);
        Assert.Equal("CHECKPNT1", entry.RelatedEntity);
        Assert.Equal("NETCTRL", entry.SourceCallsign);
    }

    [Fact]
    public void WeatherEventCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddWeatherEvent("WX9XYZ", "Weather updated.", AprsPacketSource.TcpKiss);

        Assert.NotNull(entry);
        Assert.Equal(DecodedEventType.WeatherUpdated, entry.EventType);
        Assert.Equal(DecodedEventCategory.Weather, entry.EventCategory);
    }

    [Fact]
    public void MessageEventCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddMessageEvent(DecodedEventType.MessageReceived, "K8ABC", "Message received.", AprsPacketSource.AprsIs);

        Assert.NotNull(entry);
        Assert.Equal(DecodedEventCategory.Message, entry.EventCategory);
        Assert.Equal("K8ABC", entry.SourceCallsign);
    }

    [Fact]
    public void GpsEventCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddGpsEvent("gpsd", "GPS fix updated.");

        Assert.NotNull(entry);
        Assert.Equal(DecodedEventType.GpsUpdated, entry.EventType);
        Assert.Equal(DecodedEventCategory.GPS, entry.EventCategory);
    }

    [Fact]
    public void PortEventCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddPortEvent(DecodedEventType.PortConnected, "TCP KISS", "Port connected.");

        Assert.NotNull(entry);
        Assert.Equal(DecodedEventCategory.Port, entry.EventCategory);
        Assert.Equal("TCP KISS", entry.RelatedEntity);
    }

    [Fact]
    public void TransmitBlockedEventCanBeLogged()
    {
        var service = CreateService();

        var entry = service.AddTransmitEvent(DecodedEventType.PacketTransmitBlocked, "Transmit blocked.", AprsPacketSource.Rf, "N0CALL", "RF transmit disabled.");

        Assert.NotNull(entry);
        Assert.Equal(DecodedEventSeverity.Warning, entry.Severity);
        Assert.Equal(DecodedEventType.PacketTransmitBlocked, entry.EventType);
    }

    [Fact]
    public void IGateDecisionEventCanBeLogged()
    {
        var service = CreateService();
        var decision = new IGateGatingDecisionRecord(
            RawPacket: "MOBILE1>APRS:!3903.50N/08430.50W>Mobile",
            ParsedPacketType: "Position",
            SourceCallsign: "MOBILE1",
            Destination: "APRS",
            Path: [],
            ReceivedTimestampUtc: Now,
            ReceivedRfPort: "RF",
            CandidateState: IGateCandidateState.Candidate,
            Decision: IGateDecision.Allowed,
            Reason: "Allowed.",
            ValidationWarnings: [],
            ValidationErrors: [],
            TransmitAttempted: true,
            TransmitResult: null);

        var entry = service.AddIGateEvent(decision);

        Assert.NotNull(entry);
        Assert.Equal(DecodedEventType.IGatePacketGated, entry.EventType);
        Assert.Equal(DecodedEventCategory.IGate, entry.EventCategory);
        Assert.Equal("Allowed", entry.StructuredEventData["Decision"]);
    }

    [Fact]
    public void DigipeaterDecisionEventCanBeLogged()
    {
        var service = CreateService();
        var decision = new DigipeaterDecisionRecord(
            RawPacket: "MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile",
            ParsedPacketType: "Position",
            SourceCallsign: "MOBILE1",
            Destination: "APRS",
            OriginalPath: ["WIDE1-1"],
            ModifiedPath: ["WIDE1-1*"],
            ModifiedPacket: "MOBILE1>APRS,WIDE1-1*:!3903.50N/08430.50W>Mobile",
            ReceivedTimestampUtc: Now,
            ReceivedRfPort: "RF",
            TransmitRfPort: "RF-TX",
            Decision: DigipeaterDecision.Blocked,
            Reason: "RF transmit disabled.",
            ValidationWarnings: [],
            ValidationErrors: [],
            TransmitAttempted: false,
            TransmitResult: null);

        var entry = service.AddDigipeaterEvent(decision);

        Assert.NotNull(entry);
        Assert.Equal(DecodedEventType.DigipeaterPacketBlocked, entry.EventType);
        Assert.Equal(DecodedEventCategory.Digipeater, entry.EventCategory);
        Assert.Equal(DecodedEventSeverity.Warning, entry.Severity);
    }

    [Fact]
    public void RecentEventsReturnNewestFirst()
    {
        var service = CreateService();
        service.AddEvent(DecodedEventType.StationUpdated, DecodedEventCategory.Station, DecodedEventSeverity.Info, "Old", timestampUtc: Now);
        service.AddEvent(DecodedEventType.WeatherUpdated, DecodedEventCategory.Weather, DecodedEventSeverity.Info, "New", timestampUtc: Now.AddMinutes(1));

        var events = service.GetRecentEvents();

        Assert.Equal("New", events[0].Summary);
        Assert.Equal("Old", events[1].Summary);
    }

    [Fact]
    public void FiltersAndSearchWork()
    {
        var service = CreateService();
        service.AddStationEvent(DecodedEventType.StationUpdated, "N0CALL", "Station updated.", AprsPacketSource.AprsIs);
        service.AddWeatherEvent("WX9XYZ", "Weather pressure rising.", AprsPacketSource.Rf, "Barometer trend");
        service.AddTransmitEvent(DecodedEventType.PacketTransmitBlocked, "Transmit blocked.", AprsPacketSource.Rf, "K8ABC");

        Assert.Single(service.GetEventsByType(DecodedEventType.StationUpdated));
        Assert.Single(service.GetEventsByCategory(DecodedEventCategory.Weather));
        Assert.Single(service.GetEventsBySeverity(DecodedEventSeverity.Warning));
        Assert.Single(service.GetEventsByCallsignOrSource("k8abc"));
        Assert.Single(service.SearchEvents("pressure"));
    }

    [Fact]
    public void MaximumInMemoryEventCountIsEnforced()
    {
        var service = CreateService(DecodedEventLogConfiguration.Default with { MaximumInMemoryEvents = 2 });

        service.AddEvent(DecodedEventType.StationUpdated, DecodedEventCategory.Station, DecodedEventSeverity.Info, "One", timestampUtc: Now);
        service.AddEvent(DecodedEventType.StationUpdated, DecodedEventCategory.Station, DecodedEventSeverity.Info, "Two", timestampUtc: Now.AddSeconds(1));
        service.AddEvent(DecodedEventType.StationUpdated, DecodedEventCategory.Station, DecodedEventSeverity.Info, "Three", timestampUtc: Now.AddSeconds(2));

        var events = service.GetRecentEvents();

        Assert.Equal(2, events.Count);
        Assert.DoesNotContain(events, entry => entry.Summary == "One");
    }

    [Fact]
    public void ClearingEventLogWorks()
    {
        var service = CreateService();
        service.AddStationEvent(DecodedEventType.StationUpdated, "N0CALL", "Station updated.");

        service.ClearEventLog();

        Assert.Empty(service.GetRecentEvents());
    }

    [Fact]
    public void CredentialLikeFieldsAreRedacted()
    {
        var service = CreateService();

        var entry = service.AddEvent(
            DecodedEventType.PacketTransmitBlocked,
            DecodedEventCategory.Packet,
            DecodedEventSeverity.Warning,
            "Blocked pass 12345",
            details: "token=abcdef api_key=secret",
            notes: "password:hunter2 passcode=99999",
            structuredEventData: new Dictionary<string, string>
            {
                ["secret"] = "secret=value"
            });

        Assert.NotNull(entry);
        Assert.DoesNotContain("12345", entry.Summary);
        Assert.DoesNotContain("abcdef", entry.Details);
        Assert.DoesNotContain("hunter2", entry.Notes);
        Assert.DoesNotContain("99999", entry.Notes);
        Assert.DoesNotContain("value", entry.StructuredEventData["secret"]);
        Assert.Contains("[REDACTED]", entry.Summary);
    }

    private static DecodedEventLogService CreateService(DecodedEventLogConfiguration? configuration = null)
    {
        return new DecodedEventLogService(configuration, new FakeClock { UtcNow = Now });
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
