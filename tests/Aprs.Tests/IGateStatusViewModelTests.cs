using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class IGateStatusViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ViewModelShowsDisabledDefaultsClearly()
    {
        var viewModel = IGateStatusViewModel.CreateDesignTime();

        Assert.Equal("Disabled", viewModel.IGateEnabled);
        Assert.Equal("Disabled", viewModel.RfToAprsIsGatingEnabled);
        Assert.Equal("Disabled", viewModel.AprsIsTransmitEnabled);
        Assert.Equal("Enabled", viewModel.AprsIsTransmitRequired);
        Assert.Contains("blocked", viewModel.DecisionSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewModelLoadsDecisionLogRows()
    {
        var summary = new IGateStatusSummary(
            IGateEnabled: true,
            RfToAprsIsGatingEnabled: true,
            AprsIsTransmitEnabled: true,
            AprsIsTransmitRequired: true,
            AllowedCount: 1,
            BlockedCount: 0,
            DuplicateCount: 0,
            RateLimitedCount: 0,
            InvalidCount: 0,
            ErrorCount: 0,
            LastGatedPacket: "MOBILE1>APRS:!3903.50N/08430.50W>Mobile",
            LastBlockedReason: null,
            LastDecisionTimestampUtc: Now);
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
            Reason: "RF packet gated to APRS-IS by safe iGate service.",
            ValidationWarnings: [],
            ValidationErrors: [],
            TransmitAttempted: true,
            TransmitResult: null);

        var viewModel = new IGateStatusViewModel(summary, [decision]);

        var row = Assert.Single(viewModel.Decisions);
        Assert.Equal("Enabled", viewModel.IGateEnabled);
        Assert.Equal("1 gated, 0 blocked, 0 duplicate, 0 rate limited", viewModel.DecisionSummary);
        Assert.Equal("MOBILE1", row.Callsign);
        Assert.Equal("Allowed", row.Decision);
        Assert.Equal("Yes", row.TransmitAttempted);
    }
}
