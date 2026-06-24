namespace Aprs.Services;

public sealed class AlertRuleService : IAlertRuleService
{
    private readonly IBeaconSchedulerClock clock;
    private readonly IDecodedEventLogService? decodedEventLog;
    private readonly List<AlertRule> rules = [];
    private readonly List<AlertTrigger> triggers = [];

    public AlertRuleService(IBeaconSchedulerClock? clock = null, IDecodedEventLogService? decodedEventLog = null)
    {
        this.clock = clock ?? new SystemBeaconSchedulerClock();
        this.decodedEventLog = decodedEventLog;
    }

    public AlertRule AddRule(AlertRule rule)
    {
        if (rules.Any(existing => existing.RuleId == rule.RuleId))
        {
            throw new InvalidOperationException("An alert rule with the same ID already exists.");
        }

        var now = clock.UtcNow;
        var normalized = rule with
        {
            RuleName = string.IsNullOrWhiteSpace(rule.RuleName) ? rule.AlertType.ToString() : rule.RuleName.Trim(),
            CreatedAtUtc = rule.CreatedAtUtc == default ? now : rule.CreatedAtUtc,
            UpdatedAtUtc = rule.UpdatedAtUtc == default ? now : rule.UpdatedAtUtc,
            CooldownInterval = rule.CooldownInterval < TimeSpan.Zero ? TimeSpan.Zero : rule.CooldownInterval
        };
        rules.Add(normalized);
        return normalized;
    }

    public AlertRule? UpdateRule(AlertRule rule)
    {
        var index = rules.FindIndex(existing => existing.RuleId == rule.RuleId);
        if (index < 0)
        {
            return null;
        }

        var updated = rule with
        {
            RuleName = string.IsNullOrWhiteSpace(rule.RuleName) ? rule.AlertType.ToString() : rule.RuleName.Trim(),
            UpdatedAtUtc = rule.UpdatedAtUtc == default ? clock.UtcNow : rule.UpdatedAtUtc
        };
        rules[index] = updated;
        return updated;
    }

    public bool RemoveRule(Guid ruleId)
    {
        return rules.RemoveAll(rule => rule.RuleId == ruleId) > 0;
    }

    public bool SetRuleEnabled(Guid ruleId, bool enabled, DateTimeOffset? updatedAtUtc = null)
    {
        var index = rules.FindIndex(rule => rule.RuleId == ruleId);
        if (index < 0)
        {
            return false;
        }

        rules[index] = rules[index] with
        {
            Enabled = enabled,
            UpdatedAtUtc = updatedAtUtc ?? clock.UtcNow
        };
        return true;
    }

    public IReadOnlyList<AlertRule> GetAllRules()
    {
        return rules.ToArray();
    }

    public IReadOnlyList<AlertRule> GetEnabledRules()
    {
        return rules.Where(rule => rule.Enabled).ToArray();
    }

    public IReadOnlyList<AlertTrigger> EvaluateStationUpdate(StationSnapshot station, DateTimeOffset? evaluatedAtUtc = null)
    {
        var now = evaluatedAtUtc ?? station.LastHeardUtc;
        return Evaluate(now, rule =>
        {
            if (rule.AlertType != AlertType.CallsignHeard)
            {
                return null;
            }

            if (!TargetMatches(rule.Target, station.Callsign) && !TargetMatches(rule.Target, station.RealCallsign))
            {
                return null;
            }

            return CreateTrigger(rule, now, station.Callsign, $"{station.DisplayName} heard.", station.LastRawPacket);
        });
    }

    public IReadOnlyList<AlertTrigger> EvaluateCallsignNotHeard(string callsign, DateTimeOffset lastHeardUtc, DateTimeOffset evaluatedAtUtc)
    {
        return Evaluate(evaluatedAtUtc, rule =>
        {
            if (rule.AlertType != AlertType.CallsignNotHeard || !TargetMatches(rule.Target, callsign))
            {
                return null;
            }

            var threshold = rule.TimeWindow ?? TimeSpan.FromMinutes(rule.ThresholdValue ?? 30);
            var age = evaluatedAtUtc - lastHeardUtc;
            return age >= threshold
                ? CreateTrigger(rule, evaluatedAtUtc, callsign, $"{callsign} has not been heard for {age.TotalMinutes:0} minutes.", $"Last heard {lastHeardUtc:u}.")
                : null;
        });
    }

    public IReadOnlyList<AlertTrigger> EvaluateWeatherUpdate(CommonWeatherObservation observation, DateTimeOffset? evaluatedAtUtc = null)
    {
        var now = evaluatedAtUtc ?? observation.TimestampUtc;
        var source = observation.Callsign ?? observation.StationDeviceId ?? observation.SourceName;
        return Evaluate(now, rule =>
        {
            if (rule.AlertType != AlertType.WeatherThreshold || !TargetMatches(rule.Target, source))
            {
                return null;
            }

            var value = GetWeatherValue(rule.ConditionType, observation);
            if (value is null || !Compare(value.Value, rule.ComparisonOperator, rule.ThresholdValue))
            {
                return null;
            }

            return CreateTrigger(rule, now, source, $"Weather threshold triggered for {source}.", $"{rule.ConditionType} was {value:0.##}; threshold {rule.ComparisonOperator} {rule.ThresholdValue:0.##}.");
        });
    }

    public IReadOnlyList<AlertTrigger> EvaluateGpsUpdate(GpsPosition position, DateTimeOffset? evaluatedAtUtc = null)
    {
        var now = evaluatedAtUtc ?? position.LastUpdateUtc;
        return Evaluate(now, rule =>
        {
            if (rule.AlertType != AlertType.GpsFixLost)
            {
                return null;
            }

            if (!TargetMatches(rule.Target, position.SourceName))
            {
                return null;
            }

            return !position.FixValid
                ? CreateTrigger(rule, now, position.SourceName, $"GPS fix lost for {position.SourceName}.", position.RawNmeaSentence)
                : null;
        });
    }

    public IReadOnlyList<AlertTrigger> EvaluatePortStatus(AprsPortSnapshot port, DateTimeOffset? evaluatedAtUtc = null)
    {
        var now = evaluatedAtUtc ?? port.LastDisconnectedUtc ?? clock.UtcNow;
        return Evaluate(now, rule =>
        {
            if (!TargetMatches(rule.Target, port.PortName) && !TargetMatches(rule.Target, port.PortId) && !TargetMatches(rule.Target, port.PortType.ToString()))
            {
                return null;
            }

            if (rule.AlertType == AlertType.AprsIsDisconnected
                && port.PortType == AprsPortType.AprsIs
                && port.ConnectionState is AprsPortConnectionState.Disconnected or AprsPortConnectionState.Faulted)
            {
                return CreateTrigger(rule, now, port.PortName, $"APRS-IS port {port.PortName} disconnected.", port.LastError);
            }

            if (rule.AlertType == AlertType.TncDisconnected
                && port.PortType is AprsPortType.TcpKiss or AprsPortType.SerialKiss or AprsPortType.Direwolf or AprsPortType.Agwpe
                && port.ConnectionState is AprsPortConnectionState.Disconnected or AprsPortConnectionState.Faulted)
            {
                return CreateTrigger(rule, now, port.PortName, $"RF/TNC port {port.PortName} disconnected.", port.LastError);
            }

            if (rule.AlertType == AlertType.PortError && !string.IsNullOrWhiteSpace(port.LastError))
            {
                return CreateTrigger(rule, now, port.PortName, $"Port error on {port.PortName}.", port.LastError);
            }

            return null;
        });
    }

    public IReadOnlyList<AlertTrigger> EvaluateRfDiagnostics(RfDiagnosticsSummary summary, DateTimeOffset? evaluatedAtUtc = null)
    {
        var now = evaluatedAtUtc ?? summary.LastUpdatedTimestampUtc ?? clock.UtcNow;
        return Evaluate(now, rule =>
        {
            if (rule.AlertType == AlertType.ExcessiveBeaconing && summary.ExcessiveBeaconWarnings.Count > 0)
            {
                return CreateTrigger(rule, now, rule.Target, "Excessive beaconing detected.", string.Join(" | ", summary.ExcessiveBeaconWarnings));
            }

            if (rule.AlertType == AlertType.PacketRateHigh)
            {
                var topCount = summary.TopPacketSources.Concat(summary.TopTransmittingStations).Select(pair => pair.Value).DefaultIfEmpty(0).Max();
                if (Compare(topCount, rule.ComparisonOperator, rule.ThresholdValue))
                {
                    return CreateTrigger(rule, now, rule.Target, "High packet rate detected.", $"Highest observed rate count was {topCount}.");
                }
            }

            return null;
        });
    }

    public IReadOnlyList<AlertTrigger> EvaluateMessage(AprsMessageRecord message, DateTimeOffset? evaluatedAtUtc = null)
    {
        var now = evaluatedAtUtc ?? message.ReceivedAtUtc ?? message.CreatedAtUtc;
        return Evaluate(now, rule =>
        {
            if (rule.AlertType != AlertType.MessageReceived || message.Direction != AprsMessageDirection.Incoming)
            {
                return null;
            }

            if (!TargetMatches(rule.Target, message.RemoteStationCallsign) && !TargetMatches(rule.Target, message.Sender))
            {
                return null;
            }

            return CreateTrigger(rule, now, message.RemoteStationCallsign, $"Message received from {message.RemoteStationCallsign}.", message.MessageBody);
        });
    }

    public IReadOnlyList<AlertTrigger> EvaluateBulletin(AprsBulletinRecord bulletin, DateTimeOffset? evaluatedAtUtc = null)
    {
        var now = evaluatedAtUtc ?? bulletin.ReceivedAtUtc;
        return Evaluate(now, rule =>
        {
            if (rule.AlertType != AlertType.BulletinReceived)
            {
                return null;
            }

            if (!TargetMatches(rule.Target, bulletin.SenderCallsign) && !TargetMatches(rule.Target, bulletin.Addressee) && !TargetMatches(rule.Target, bulletin.BulletinId))
            {
                return null;
            }

            return CreateTrigger(rule, now, bulletin.SenderCallsign, $"Bulletin {bulletin.BulletinId} received from {bulletin.SenderCallsign}.", bulletin.BulletinText);
        });
    }

    public IReadOnlyList<AlertTrigger> EvaluateGeofenceEvent(GeofenceStationEvent geofenceEvent, DateTimeOffset? evaluatedAtUtc = null)
    {
        var now = evaluatedAtUtc ?? geofenceEvent.TimestampUtc;
        return Evaluate(now, rule =>
        {
            if (geofenceEvent.EventType == GeofenceEventType.Entered && rule.AlertType != AlertType.StationEnteredArea)
            {
                return null;
            }

            if (geofenceEvent.EventType == GeofenceEventType.Left && rule.AlertType != AlertType.StationLeftArea)
            {
                return null;
            }

            if (!TargetMatches(rule.Target, geofenceEvent.GeofenceName)
                && !TargetMatches(rule.Target, geofenceEvent.StationCallsign)
                && !TargetMatches(rule.Target, geofenceEvent.GeofenceId.ToString()))
            {
                return null;
            }

            return CreateTrigger(rule, now, geofenceEvent.StationCallsign, geofenceEvent.Summary, geofenceEvent.Details);
        });
    }

    public bool AcknowledgeTrigger(Guid triggerId, DateTimeOffset? acknowledgedAtUtc = null)
    {
        var index = triggers.FindIndex(trigger => trigger.TriggerId == triggerId);
        if (index < 0)
        {
            return false;
        }

        triggers[index] = triggers[index] with
        {
            Acknowledged = true,
            AcknowledgedAtUtc = acknowledgedAtUtc ?? clock.UtcNow
        };
        return true;
    }

    public IReadOnlyList<AlertTrigger> GetRecentTriggers(int? maximumCount = null)
    {
        var query = triggers.OrderByDescending(trigger => trigger.TimestampUtc).ThenByDescending(trigger => trigger.TriggerId);
        return maximumCount is > 0 ? query.Take(maximumCount.Value).ToArray() : query.ToArray();
    }

    public void ClearAlertHistory()
    {
        triggers.Clear();
    }

    private IReadOnlyList<AlertTrigger> Evaluate(DateTimeOffset timestamp, Func<AlertRule, AlertTrigger?> evaluator)
    {
        var fired = new List<AlertTrigger>();
        foreach (var rule in rules.Where(rule => rule.Enabled).ToArray())
        {
            var trigger = evaluator(rule);
            if (trigger is null || IsCoolingDown(rule, timestamp))
            {
                continue;
            }

            RecordTrigger(trigger, timestamp);
            fired.Add(trigger);
        }

        return fired;
    }

    private void RecordTrigger(AlertTrigger trigger, DateTimeOffset timestamp)
    {
        var ruleIndex = rules.FindIndex(rule => rule.RuleId == trigger.RuleId);
        if (ruleIndex >= 0)
        {
            var rule = rules[ruleIndex];
            rules[ruleIndex] = rule with
            {
                LastTriggeredAtUtc = timestamp,
                TriggerCount = rule.TriggerCount + 1,
                UpdatedAtUtc = timestamp
            };
        }

        triggers.Add(trigger);
        decodedEventLog?.AddEvent(
            DecodedEventType.AlertTriggered,
            DecodedEventCategory.Alert,
            MapSeverity(trigger.Severity),
            trigger.Summary,
            trigger.Details,
            trigger.SourceCallsignOrName,
            trigger.RuleName,
            notes: trigger.Notes,
            timestampUtc: trigger.TimestampUtc);
    }

    private bool IsCoolingDown(AlertRule rule, DateTimeOffset timestamp)
    {
        return rule.LastTriggeredAtUtc is not null
            && timestamp - rule.LastTriggeredAtUtc.Value < rule.CooldownInterval;
    }

    private static AlertTrigger CreateTrigger(AlertRule rule, DateTimeOffset timestamp, string? source, string summary, string? details)
    {
        return new AlertTrigger(
            Guid.NewGuid(),
            rule.RuleId,
            rule.RuleName,
            timestamp,
            rule.Severity,
            rule.AlertType,
            string.IsNullOrWhiteSpace(source) ? null : source.Trim(),
            summary,
            details,
            null,
            Acknowledged: false,
            AcknowledgedAtUtc: null,
            rule.Notes);
    }

    private static bool TargetMatches(string? target, string? value)
    {
        return string.IsNullOrWhiteSpace(target)
            || string.Equals(target.Trim(), value?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static double? GetWeatherValue(AlertConditionType conditionType, CommonWeatherObservation observation)
    {
        return conditionType switch
        {
            AlertConditionType.WindSpeedMph => observation.WindSpeedMph,
            AlertConditionType.WindGustMph => observation.WindGustMph,
            AlertConditionType.TemperatureFahrenheit => observation.TemperatureFahrenheit,
            AlertConditionType.BarometricPressureMillibars => observation.BarometricPressureMillibars,
            AlertConditionType.RainLastHourInches => observation.RainLastHourInches,
            AlertConditionType.RainLast24HoursInches => observation.RainLast24HoursInches,
            AlertConditionType.RainSinceMidnightInches => observation.RainSinceMidnightInches,
            _ => null
        };
    }

    private static bool Compare(double value, AlertComparisonOperator comparisonOperator, double? threshold)
    {
        if (threshold is null)
        {
            return true;
        }

        return comparisonOperator switch
        {
            AlertComparisonOperator.Equals => Math.Abs(value - threshold.Value) < 0.0001,
            AlertComparisonOperator.NotEquals => Math.Abs(value - threshold.Value) >= 0.0001,
            AlertComparisonOperator.GreaterThan => value > threshold.Value,
            AlertComparisonOperator.GreaterThanOrEqual => value >= threshold.Value,
            AlertComparisonOperator.LessThan => value < threshold.Value,
            AlertComparisonOperator.LessThanOrEqual => value <= threshold.Value,
            _ => value >= threshold.Value
        };
    }

    private static DecodedEventSeverity MapSeverity(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Info => DecodedEventSeverity.Info,
            AlertSeverity.Advisory => DecodedEventSeverity.Info,
            AlertSeverity.Warning => DecodedEventSeverity.Warning,
            AlertSeverity.Critical => DecodedEventSeverity.Critical,
            _ => DecodedEventSeverity.Info
        };
    }
}
