using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class AlertRulesViewModel : INotifyPropertyChanged
{
    private readonly IAlertRuleService alertRuleService;
    private AlertTriggerRowViewModel? selectedTrigger;

    public AlertRulesViewModel(IAlertRuleService alertRuleService)
    {
        this.alertRuleService = alertRuleService;
        Rules = new ObservableCollection<AlertRuleRowViewModel>();
        History = new ObservableCollection<AlertTriggerRowViewModel>();
        AcknowledgeSelectedCommand = new DesktopCommand(AcknowledgeSelected);
        ClearHistoryCommand = new DesktopCommand(ClearHistory);
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AlertRuleRowViewModel> Rules { get; }

    public ObservableCollection<AlertTriggerRowViewModel> History { get; }

    public DesktopCommand AcknowledgeSelectedCommand { get; }

    public DesktopCommand ClearHistoryCommand { get; }

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
