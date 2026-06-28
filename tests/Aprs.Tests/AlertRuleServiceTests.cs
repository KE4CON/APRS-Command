using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AlertRuleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AlertRuleCanBeAdded()
    {
        var service = CreateService();
        var rule = service.AddRule(CreateRule(AlertType.CallsignHeard, AlertConditionType.Callsign, "N0CALL"));

        Assert.Equal(rule, Assert.Single(service.GetAllRules()));
        Assert.Equal(rule, Assert.Single(service.GetEnabledRules()));
    }

    [Fact]
    public void AlertRuleCanBeDisabled()
    {
        var service = CreateService();
        var rule = service.AddRule(CreateRule(AlertType.CallsignHeard, AlertConditionType.Callsign, "N0CALL"));

        var disabled = service.SetRuleEnabled(rule.RuleId, false, Now);

        Assert.True(disabled);
        Assert.Empty(service.GetEnabledRules());
    }

    [Fact]
    public void DisabledRuleDoesNotTrigger()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.CallsignHeard, AlertConditionType.Callsign, "N0CALL") with { Enabled = false });

        var triggers = service.EvaluateStationUpdate(CreateStation("N0CALL"));

        Assert.Empty(triggers);
        Assert.Empty(service.GetRecentTriggers());
    }

    [Fact]
    public void CallsignHeardRuleTriggers()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.CallsignHeard, AlertConditionType.Callsign, "N0CALL"));

        var triggers = service.EvaluateStationUpdate(CreateStation("N0CALL"), Now);

        var trigger = Assert.Single(triggers);
        Assert.Equal(AlertType.CallsignHeard, trigger.AlertType);
        Assert.Equal("N0CALL", trigger.SourceCallsignOrName);
    }

    [Fact]
    public void CallsignNotHeardRuleTriggersAfterConfiguredTime()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.CallsignNotHeard, AlertConditionType.NotHeardWithin, "N0CALL") with
        {
            TimeWindow = TimeSpan.FromMinutes(30)
        });

        var triggers = service.EvaluateCallsignNotHeard("N0CALL", Now.AddMinutes(-31), Now);

        Assert.Single(triggers);
    }

    [Fact]
    public void AprsIsDisconnectedRuleTriggers()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.AprsIsDisconnected, AlertConditionType.PortDisconnected, "APRS-IS"));

        var triggers = service.EvaluatePortStatus(CreatePort("aprs", "APRS-IS", AprsPortType.AprsIs, AprsPortConnectionState.Disconnected), Now);

        Assert.Single(triggers);
    }

    [Fact]
    public void TncDisconnectedRuleTriggers()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.TncDisconnected, AlertConditionType.PortDisconnected, "Direwolf"));

        var triggers = service.EvaluatePortStatus(CreatePort("direwolf", "Direwolf", AprsPortType.Direwolf, AprsPortConnectionState.Faulted), Now);

        Assert.Single(triggers);
    }

    [Fact]
    public void GpsFixLostRuleTriggers()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.GpsFixLost, AlertConditionType.GpsFixInvalid, "gpsd"));

        var triggers = service.EvaluateGpsUpdate(new GpsPosition(null, null, null, null, null, Now, FixValid: false, null, null, null, "gpsd", "$GPRMC,V", Now), Now);

        Assert.Single(triggers);
    }

    [Fact]
    public void WeatherWindThresholdRuleTriggers()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.WeatherThreshold, AlertConditionType.WindGustMph, "WX9XYZ") with
        {
            ComparisonOperator = AlertComparisonOperator.GreaterThanOrEqual,
            ThresholdValue = 40
        });

        var triggers = service.EvaluateWeatherUpdate(CreateWeather("WX9XYZ", windGust: 45, temperature: 70), Now);

        Assert.Single(triggers);
    }

    [Fact]
    public void WeatherTemperatureThresholdRuleTriggers()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.WeatherThreshold, AlertConditionType.TemperatureFahrenheit, "WX9XYZ") with
        {
            ComparisonOperator = AlertComparisonOperator.LessThanOrEqual,
            ThresholdValue = 32
        });

        var triggers = service.EvaluateWeatherUpdate(CreateWeather("WX9XYZ", windGust: 10, temperature: 31), Now);

        Assert.Single(triggers);
    }

    [Fact]
    public void MessageReceivedRuleTriggers()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.MessageReceived, AlertConditionType.Message, "K8ABC"));

        var triggers = service.EvaluateMessage(CreateMessage("K8ABC"), Now);

        Assert.Single(triggers);
    }

    [Fact]
    public void BulletinReceivedRuleTriggers()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.BulletinReceived, AlertConditionType.Bulletin, "W1AW"));

        var triggers = service.EvaluateBulletin(new AprsBulletinRecord("BLN0", "W1AW", "BLN0", "Club meeting", "W1AW>APRS::BLN0     :Club meeting", Now, AprsPacketSource.AprsIs, Now, true, null, []), Now);

        Assert.Single(triggers);
    }

    [Fact]
    public void CooldownPreventsRepeatedAlertSpam()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.CallsignHeard, AlertConditionType.Callsign, "N0CALL") with
        {
            CooldownInterval = TimeSpan.FromMinutes(5)
        });

        var first = service.EvaluateStationUpdate(CreateStation("N0CALL"), Now);
        var second = service.EvaluateStationUpdate(CreateStation("N0CALL"), Now.AddMinutes(1));

        Assert.Single(first);
        Assert.Empty(second);
        Assert.Single(service.GetRecentTriggers());
    }

    [Fact]
    public void AlertCanBeAcknowledged()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.CallsignHeard, AlertConditionType.Callsign, "N0CALL"));
        var trigger = Assert.Single(service.EvaluateStationUpdate(CreateStation("N0CALL"), Now));

        var acknowledged = service.AcknowledgeTrigger(trigger.TriggerId, Now.AddMinutes(1));

        Assert.True(acknowledged);
        Assert.True(Assert.Single(service.GetRecentTriggers()).Acknowledged);
    }

    [Fact]
    public void AlertHistoryCanBeCleared()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.CallsignHeard, AlertConditionType.Callsign, "N0CALL"));
        service.EvaluateStationUpdate(CreateStation("N0CALL"), Now);

        service.ClearAlertHistory();

        Assert.Empty(service.GetRecentTriggers());
    }

    [Fact]
    public void RfDiagnosticsExcessiveBeaconingTriggers()
    {
        var service = CreateService();
        service.AddRule(CreateRule(AlertType.ExcessiveBeaconing, AlertConditionType.PacketRate, null));
        var summary = new RfDiagnosticsSummary(
            10,
            10,
            0,
            0,
            1,
            [new KeyValuePair<string, int>("RF", 10)],
            [new KeyValuePair<string, int>("N0CALL", 10)],
            ["N0CALL exceeded beacon rate."],
            [],
            10,
            0,
            0,
            Now);

        var triggers = service.EvaluateRfDiagnostics(summary, Now);

        Assert.Single(triggers);
    }

    [Fact]
    public void DecodedEventLogReceivesTriggeredAlert()
    {
        var eventLog = new DecodedEventLogService(clock: new FakeClock { UtcNow = Now });
        var service = CreateService(eventLog: eventLog);
        service.AddRule(CreateRule(AlertType.CallsignHeard, AlertConditionType.Callsign, "N0CALL"));

        service.EvaluateStationUpdate(CreateStation("N0CALL"), Now);

        var entry = Assert.Single(eventLog.GetEventsByType(DecodedEventType.AlertTriggered));
        Assert.Equal(DecodedEventCategory.Alert, entry.EventCategory);
        Assert.Equal("N0CALL", entry.SourceCallsign);
    }

    private static AlertRuleService CreateService(IDecodedEventLogService? eventLog = null)
    {
        return new AlertRuleService(new FakeClock { UtcNow = Now }, eventLog);
    }

    private static AlertRule CreateRule(AlertType alertType, AlertConditionType conditionType, string? target)
    {
        return new AlertRule(
            Guid.NewGuid(),
            alertType.ToString(),
            Enabled: true,
            alertType,
            conditionType,
            target,
            AlertComparisonOperator.None,
            null,
            null,
            TimeSpan.Zero,
            AlertSeverity.Warning,
            AlertNotificationMethod.InApp,
            Now,
            Now,
            null,
            0,
            null);
    }

    private static StationSnapshot CreateStation(string callsign)
    {
        return new StationSnapshot(
            callsign,
            null,
            callsign,
            null,
            callsign,
            StationLifecycleState.Active,
            false,
            39,
            -84,
            '/',
            '>',
            "Test",
            Now,
            Now,
            $"{callsign}>APRS:>Test",
            "Status",
            null,
            null,
            null,
            1,
            0,
            [],
            AprsPacketSource.Rf,
            null,
            null);
    }

    private static CommonWeatherObservation CreateWeather(string callsign, double windGust, double temperature)
    {
        return new CommonWeatherObservation(
            callsign,
            WeatherObservationSourceType.AprsWeatherStation,
            null,
            callsign,
            Now,
            null,
            null,
            180,
            10,
            windGust,
            temperature,
            0,
            0,
            0,
            50,
            1013,
            null,
            null,
            null,
            null,
            null,
            new Dictionary<string, string>(),
            "raw",
            WeatherDataState.Current,
            [],
            []);
    }

    private static AprsPortSnapshot CreatePort(string id, string name, AprsPortType type, AprsPortConnectionState state)
    {
        return new AprsPortSnapshot(
            id,
            name,
            type,
            Enabled: true,
            ReceiveEnabled: true,
            TransmitEnabled: false,
            state,
            null,
            Now,
            null,
            null,
            0,
            0,
            state == AprsPortConnectionState.Faulted ? "Faulted" : null,
            name,
            null);
    }

    private static AprsMessageRecord CreateMessage(string remote)
    {
        return new AprsMessageRecord(
            Guid.NewGuid(),
            "01",
            "N0CALL",
            remote,
            "N0CALL",
            remote,
            "N0CALL",
            "Hello",
            $"{remote}>APRS::N0CALL   :Hello{{01",
            AprsMessageDirection.Incoming,
            AprsMessageStatus.Received,
            Now,
            null,
            Now,
            Now,
            AprsPacketSource.AprsIs,
            AprsMessageKind.PrivateMessage,
            []);
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
