namespace Aprs.Services;

/// <summary>
/// Stores APRS alert rules, evaluates app events, and records alert triggers.
/// </summary>
public interface IAlertRuleService
{
    AlertRule AddRule(AlertRule rule);

    AlertRule? UpdateRule(AlertRule rule);

    bool RemoveRule(Guid ruleId);

    bool SetRuleEnabled(Guid ruleId, bool enabled, DateTimeOffset? updatedAtUtc = null);

    IReadOnlyList<AlertRule> GetAllRules();

    IReadOnlyList<AlertRule> GetEnabledRules();

    IReadOnlyList<AlertTrigger> EvaluateStationUpdate(StationSnapshot station, DateTimeOffset? evaluatedAtUtc = null);

    IReadOnlyList<AlertTrigger> EvaluateCallsignNotHeard(string callsign, DateTimeOffset lastHeardUtc, DateTimeOffset evaluatedAtUtc);

    IReadOnlyList<AlertTrigger> EvaluateWeatherUpdate(CommonWeatherObservation observation, DateTimeOffset? evaluatedAtUtc = null);

    IReadOnlyList<AlertTrigger> EvaluateGpsUpdate(GpsPosition position, DateTimeOffset? evaluatedAtUtc = null);

    IReadOnlyList<AlertTrigger> EvaluatePortStatus(AprsPortSnapshot port, DateTimeOffset? evaluatedAtUtc = null);

    IReadOnlyList<AlertTrigger> EvaluateRfDiagnostics(RfDiagnosticsSummary summary, DateTimeOffset? evaluatedAtUtc = null);

    IReadOnlyList<AlertTrigger> EvaluateMessage(AprsMessageRecord message, DateTimeOffset? evaluatedAtUtc = null);

    IReadOnlyList<AlertTrigger> EvaluateBulletin(AprsBulletinRecord bulletin, DateTimeOffset? evaluatedAtUtc = null);

    IReadOnlyList<AlertTrigger> EvaluateGeofenceEvent(GeofenceStationEvent geofenceEvent, DateTimeOffset? evaluatedAtUtc = null);

    bool AcknowledgeTrigger(Guid triggerId, DateTimeOffset? acknowledgedAtUtc = null);

    IReadOnlyList<AlertTrigger> GetRecentTriggers(int? maximumCount = null);

    void ClearAlertHistory();
}
