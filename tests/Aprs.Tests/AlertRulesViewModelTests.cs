using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AlertRulesViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AlertViewModelListsRulesAndTriggers()
    {
        var service = new AlertRuleService(new FakeClock { UtcNow = Now });
        service.AddRule(new AlertRule(
            Guid.NewGuid(),
            "N0CALL heard",
            true,
            AlertType.CallsignHeard,
            AlertConditionType.Callsign,
            "N0CALL",
            AlertComparisonOperator.None,
            null,
            null,
            TimeSpan.Zero,
            AlertSeverity.Info,
            AlertNotificationMethod.InApp,
            Now,
            Now,
            null,
            0,
            null));
        service.EvaluateStationUpdate(CreateStation("N0CALL"), Now);

        var viewModel = new AlertRulesViewModel(service);

        Assert.Equal("1 rules", viewModel.RuleCountText);
        Assert.Equal("1 triggers", viewModel.TriggerCountText);
        Assert.Single(viewModel.Rules);
        Assert.Single(viewModel.History);
    }

    [Fact]
    public void ViewModelCanAcknowledgeAndClearHistory()
    {
        var service = new AlertRuleService(new FakeClock { UtcNow = Now });
        service.AddRule(new AlertRule(Guid.NewGuid(), "N0CALL heard", true, AlertType.CallsignHeard, AlertConditionType.Callsign, "N0CALL", AlertComparisonOperator.None, null, null, TimeSpan.Zero, AlertSeverity.Info, AlertNotificationMethod.InApp, Now, Now, null, 0, null));
        service.EvaluateStationUpdate(CreateStation("N0CALL"), Now);
        var viewModel = new AlertRulesViewModel(service);
        viewModel.SelectedTrigger = Assert.Single(viewModel.History);

        viewModel.AcknowledgeSelectedCommand.Execute(null);

        Assert.Equal("Yes", Assert.Single(viewModel.History).Acknowledged);

        viewModel.ClearHistoryCommand.Execute(null);

        Assert.Empty(viewModel.History);
        Assert.Equal("0 triggers", viewModel.TriggerCountText);
    }

    private static StationSnapshot CreateStation(string callsign)
    {
        return new StationSnapshot(callsign, null, callsign, null, callsign, StationLifecycleState.Active, false, null, null, null, null, null, Now, Now, null, "Status", null, null, null, 1, [], AprsPacketSource.Rf, null, null);
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
