using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class DigipeaterStatusViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ViewModelShowsDisabledDefaultsClearly()
    {
        var viewModel = DigipeaterStatusViewModel.CreateDesignTime();

        Assert.Equal("Disabled", viewModel.DigipeaterEnabled);
        Assert.Equal("Disabled", viewModel.RfTransmitEnabled);
        Assert.Contains("WIDE1-1", viewModel.SupportedAliases);
        Assert.Contains("blocked", viewModel.DecisionSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ViewModelLoadsDecisionLogRows()
    {
        var summary = new DigipeaterStatusSummary(
            DigipeaterEnabled: true,
            RfTransmitEnabled: true,
            RfTransmitPort: "RF-TX",
            DigipeaterCallsign: "MYDIGI",
            SupportedAliases: ["WIDE1-1"],
            FillInDigipeaterMode: true,
            FullDigipeaterMode: false,
            AllowedCount: 1,
            BlockedCount: 0,
            DuplicateCount: 0,
            RateLimitedCount: 0,
            InvalidCount: 0,
            ErrorCount: 0,
            LastDigipeatedPacket: "MOBILE1>APRS,WIDE1-1*:!3903.50N/08430.50W>Mobile",
            LastBlockedReason: null,
            LastDecisionTimestampUtc: Now);
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
            Decision: DigipeaterDecision.Allowed,
            Reason: "RF packet digipeated by safe digipeater service.",
            ValidationWarnings: [],
            ValidationErrors: [],
            TransmitAttempted: true,
            TransmitResult: null);

        var viewModel = new DigipeaterStatusViewModel(summary, [decision]);

        var row = Assert.Single(viewModel.Decisions);
        Assert.Equal("Enabled", viewModel.DigipeaterEnabled);
        Assert.Equal("1 digipeated, 0 blocked, 0 duplicate, 0 rate limited", viewModel.DecisionSummary);
        Assert.Equal("MOBILE1", row.Callsign);
        Assert.Equal("Allowed", row.Decision);
        Assert.Equal("Yes", row.TransmitAttempted);
        Assert.Equal("WIDE1-1*", row.Path);
    }
}
