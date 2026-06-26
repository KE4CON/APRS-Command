using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class AlertRulesViewModel : INotifyPropertyChanged
{
    private readonly IAlertRuleService alertRuleService;
    private AlertTriggerRowViewModel? selectedTrigger;
    private AlertRuleRowViewModel? selectedRule;
    private bool isEditingRule;
    private string editRuleName = string.Empty;
    private AlertType editAlertType = AlertType.CallsignHeard;
    private string editTarget = string.Empty;
    private AlertSeverity editSeverity = AlertSeverity.Info;
    private bool editEnabled = true;
    private Guid? editingRuleId;
    private string editorStatus = string.Empty;

    public AlertRulesViewModel(IAlertRuleService alertRuleService)
    {
        this.alertRuleService = alertRuleService;
        Rules = new ObservableCollection<AlertRuleRowViewModel>();
        History = new ObservableCollection<AlertTriggerRowViewModel>();
        AcknowledgeSelectedCommand = new DesktopCommand(AcknowledgeSelected);
        ClearHistoryCommand = new DesktopCommand(ClearHistory);
        NewRuleCommand = new DesktopCommand(BeginNewRule);
        EditSelectedRuleCommand = new DesktopCommand(BeginEditRule, () => SelectedRule is not null);
        DeleteSelectedRuleCommand = new DesktopCommand(DeleteRule, () => SelectedRule is not null);
        SaveRuleCommand = new DesktopCommand(SaveRule);
        CancelEditCommand = new DesktopCommand(CancelEdit);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AlertRuleRowViewModel> Rules { get; }

    public ObservableCollection<AlertTriggerRowViewModel> History { get; }

    public DesktopCommand AcknowledgeSelectedCommand { get; }
    public DesktopCommand ClearHistoryCommand { get; }
    public DesktopCommand NewRuleCommand { get; }
    public DesktopCommand EditSelectedRuleCommand { get; }
    public DesktopCommand DeleteSelectedRuleCommand { get; }
    public DesktopCommand SaveRuleCommand { get; }
    public DesktopCommand CancelEditCommand { get; }

    public IReadOnlyList<AlertType> AvailableAlertTypes { get; } =
        Enum.GetValues<AlertType>().ToArray();

    public IReadOnlyList<AlertSeverity> AvailableSeverities { get; } =
        Enum.GetValues<AlertSeverity>().ToArray();

    public AlertRuleRowViewModel? SelectedRule
    {
        get => selectedRule;
        set
        {
            if (selectedRule == value) return;
            selectedRule = value;
            OnPropertyChanged();
            EditSelectedRuleCommand.RaiseCanExecuteChanged();
            DeleteSelectedRuleCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsEditingRule
    {
        get => isEditingRule;
        private set { isEditingRule = value; OnPropertyChanged(); }
    }

    public string EditRuleName
    {
        get => editRuleName;
        set { editRuleName = value; OnPropertyChanged(); }
    }

    public AlertType EditAlertType
    {
        get => editAlertType;
        set { editAlertType = value; OnPropertyChanged(); }
    }

    public string EditTarget
    {
        get => editTarget;
        set { editTarget = value; OnPropertyChanged(); }
    }

    public AlertSeverity EditSeverity
    {
        get => editSeverity;
        set { editSeverity = value; OnPropertyChanged(); }
    }

    public bool EditEnabled
    {
        get => editEnabled;
        set { editEnabled = value; OnPropertyChanged(); }
    }

    public string EditorStatus
    {
        get => editorStatus;
        private set { editorStatus = value; OnPropertyChanged(); }
    }

    public string EditorTitle => editingRuleId.HasValue ? "Edit Rule" : "New Rule";

    public AlertTriggerRowViewModel? SelectedTrigger
    {
        get => selectedTrigger;
        set
        {
            if (selectedTrigger == value)
            {
                return;
            }

            selectedTrigger = value;
            OnPropertyChanged();
        }
    }

    public string RuleCountText { get; private set; } = "0 rules";

    public string TriggerCountText { get; private set; } = "0 triggers";

    public void Refresh()
    {
        Replace(Rules, alertRuleService.GetAllRules().Select(rule => new AlertRuleRowViewModel(rule)));
        Replace(History, alertRuleService.GetRecentTriggers(50).Select(trigger => new AlertTriggerRowViewModel(trigger)));
        RuleCountText = $"{Rules.Count} rules";
        TriggerCountText = $"{History.Count} triggers";
        OnPropertyChanged(nameof(RuleCountText));
        OnPropertyChanged(nameof(TriggerCountText));
    }

    public static AlertRulesViewModel CreateDesignTime()
    {
        var now = DateTimeOffset.UtcNow;
        var service = new AlertRuleService(clock: new DesignClock(now));
        var windRule = service.AddRule(new AlertRule(
            Guid.NewGuid(),
            "High wind",
            Enabled: true,
            AlertType.WeatherThreshold,
            AlertConditionType.WindGustMph,
            "WX9XYZ",
            AlertComparisonOperator.GreaterThanOrEqual,
            35,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(10),
            AlertSeverity.Warning,
            AlertNotificationMethod.InApp,
            now,
            now,
            null,
            0,
            "Weather threshold sample."));
        service.AddRule(new AlertRule(
            Guid.NewGuid(),
            "APRS-IS disconnected",
            Enabled: true,
            AlertType.AprsIsDisconnected,
            AlertConditionType.PortDisconnected,
            "APRS-IS",
            AlertComparisonOperator.None,
            null,
            null,
            TimeSpan.FromMinutes(5),
            AlertSeverity.Critical,
            AlertNotificationMethod.InApp,
            now,
            now,
            null,
            0,
            "Connection sample."));
        service.EvaluateWeatherUpdate(new CommonWeatherObservation(
            "WX9XYZ",
            WeatherObservationSourceType.AprsWeatherStation,
            null,
            "WX9XYZ",
            now,
            null,
            null,
            180,
            20,
            41,
            72,
            0,
            0,
            0,
            45,
            1012,
            null,
            null,
            null,
            null,
            null,
            new Dictionary<string, string>(),
            "sample",
            WeatherDataState.Current,
            [],
            []));

        return new AlertRulesViewModel(service);
    }

    private void BeginNewRule()
    {
        editingRuleId = null;
        EditRuleName = string.Empty;
        EditAlertType = AlertType.CallsignHeard;
        EditTarget = string.Empty;
        EditSeverity = AlertSeverity.Info;
        EditEnabled = true;
        EditorStatus = string.Empty;
        IsEditingRule = true;
        OnPropertyChanged(nameof(EditorTitle));
    }

    private void BeginEditRule()
    {
        if (SelectedRule is null) return;
        var rule = alertRuleService.GetAllRules().FirstOrDefault(r => r.RuleId == SelectedRule.RuleId);
        if (rule is null) return;

        editingRuleId = rule.RuleId;
        EditRuleName = rule.RuleName;
        EditAlertType = rule.AlertType;
        EditTarget = rule.Target ?? string.Empty;
        EditSeverity = rule.Severity;
        EditEnabled = rule.Enabled;
        EditorStatus = string.Empty;
        IsEditingRule = true;
        OnPropertyChanged(nameof(EditorTitle));
    }

    private void SaveRule()
    {
        if (string.IsNullOrWhiteSpace(EditRuleName))
        {
            EditorStatus = "Rule name is required.";
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var rule = new AlertRule(
            RuleId: editingRuleId ?? Guid.NewGuid(),
            RuleName: EditRuleName.Trim(),
            Enabled: EditEnabled,
            AlertType: EditAlertType,
            ConditionType: DefaultConditionFor(EditAlertType),
            Target: string.IsNullOrWhiteSpace(EditTarget) ? null : EditTarget.Trim().ToUpperInvariant(),
            ComparisonOperator: AlertComparisonOperator.None,
            ThresholdValue: null,
            TimeWindow: null,
            CooldownInterval: TimeSpan.FromMinutes(5),
            Severity: EditSeverity,
            NotificationMethod: AlertNotificationMethod.InApp,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            LastTriggeredAtUtc: null,
            TriggerCount: 0,
            Notes: null);

        if (editingRuleId.HasValue)
            alertRuleService.UpdateRule(rule);
        else
            alertRuleService.AddRule(rule);

        IsEditingRule = false;
        EditorStatus = string.Empty;
        Refresh();
    }

    private void DeleteRule()
    {
        if (SelectedRule is null) return;
        alertRuleService.RemoveRule(SelectedRule.RuleId);
        SelectedRule = null;
        Refresh();
    }

    private void CancelEdit()
    {
        IsEditingRule = false;
        EditorStatus = string.Empty;
    }

    private static AlertConditionType DefaultConditionFor(AlertType type) => type switch
    {
        AlertType.CallsignHeard or AlertType.CallsignNotHeard => AlertConditionType.Callsign,
        AlertType.WeatherThreshold => AlertConditionType.WindGustMph,
        AlertType.AprsIsDisconnected or AlertType.TncDisconnected => AlertConditionType.PortDisconnected,
        AlertType.MessageReceived => AlertConditionType.Message,
        AlertType.BulletinReceived => AlertConditionType.Bulletin,
        _ => AlertConditionType.Any
    };

    private void AcknowledgeSelected()
    {
        if (selectedTrigger is null)
        {
            return;
        }

        alertRuleService.AcknowledgeTrigger(selectedTrigger.TriggerId);
        Refresh();
    }

    private void ClearHistory()
    {
        alertRuleService.ClearAlertHistory();
        Refresh();
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class DesignClock : IBeaconSchedulerClock
    {
        public DesignClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; set; }
    }
}
