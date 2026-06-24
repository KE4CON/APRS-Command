using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class IGateMonitorServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
    private readonly AprsParser parser = new();

    [Fact]
    public void DefaultConfiguration_IsMonitorOnlyAndDisabled()
    {
        var configuration = IGateMonitorConfiguration.Default;

        Assert.False(configuration.MonitorEnabled);
        Assert.False(configuration.RfToAprsIsCandidateDetectionEnabled);
        Assert.True(configuration.AprsIsDuplicateDetectionEnabled);
        Assert.Contains("Monitor-only", configuration.Notes);
        Assert.Contains("not implemented", configuration.Notes);
    }

    [Fact]
    public void DisabledMonitor_RejectsPacketWithoutTransmitCapability()
    {
        var monitor = new IGateMonitorService();

        var candidate = monitor.AcceptRfPacket(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), "RF");

        Assert.Equal(IGateCandidateState.Rejected, candidate.CandidateState);
        Assert.Contains("disabled", candidate.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, monitor.GetSummary().CandidateCount);
    }

    [Fact]
    public void RfPositionPacket_BecomesCandidate()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration());

        var candidate = monitor.AcceptRfPacket(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), "RF");

        Assert.Equal(IGateCandidateState.Candidate, candidate.CandidateState);
        Assert.Equal(IGateDuplicateState.NotSeen, candidate.DuplicateState);
        Assert.True(candidate.IsRfSource);
        Assert.False(candidate.WasAlsoSeenOnAprsIs);
        Assert.Equal("Position", candidate.ParsedPacketType);
        Assert.Equal(["WIDE1-1"], candidate.Path);
        Assert.Contains("candidate", candidate.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NonRfSource_IsRejectedAsGateCandidate()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration());

        var candidate = monitor.AcceptPacket(
            Parse("MOBILE1>APRS:!3903.50N/08430.50W>Mobile"),
            AprsPacketSource.AprsIs,
            "APRS-IS");

        Assert.Equal(IGateCandidateState.Rejected, candidate.CandidateState);
        Assert.False(candidate.IsRfSource);
        Assert.Contains("not an RF", candidate.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MalformedPacket_IsInvalidAndDoesNotCrash()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration());

        var candidate = monitor.AcceptRfPacket(Parse("BADPACKETWITHOUTSEPARATOR"), "RF");

        Assert.Equal(IGateCandidateState.Invalid, candidate.CandidateState);
        Assert.NotEmpty(candidate.ValidationErrors);
        Assert.Contains("validation", candidate.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnsupportedPacketType_IsRejectedWhenTypeFilterEnabled()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration());

        var candidate = monitor.AcceptRfPacket(Parse("N0CALL>APRS:>Status only"), "RF");

        Assert.Equal(IGateCandidateState.Rejected, candidate.CandidateState);
        Assert.Contains("Packet type", candidate.Reason);
    }

    [Fact]
    public void AprsIsSeenBeforeRf_MarksRfPacketDuplicateWithinWindow()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration());
        monitor.AcceptAprsIsPacket(Parse("MOBILE1>APRS,TCPIP*,qAC,T2TEST:!3903.50N/08430.50W>Mobile"), "T2TEST", Now);

        var candidate = monitor.AcceptRfPacket(
            Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", Now.AddMinutes(5)),
            "RF",
            Now.AddMinutes(5));

        Assert.Equal(IGateCandidateState.Duplicate, candidate.CandidateState);
        Assert.Equal(IGateDuplicateState.DuplicateWithinWindow, candidate.DuplicateState);
        Assert.True(candidate.WasAlsoSeenOnAprsIs);
        Assert.Contains("APRS-IS", candidate.Reason);
    }

    [Fact]
    public void AprsIsSeenAfterRf_UpdatesExistingCandidate()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration());
        monitor.AcceptRfPacket(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), "RF", Now);

        monitor.AcceptAprsIsPacket(
            Parse("MOBILE1>APRS,TCPIP*,qAC,T2TEST:!3903.50N/08430.50W>Mobile", Now.AddMinutes(2)),
            "T2TEST",
            Now.AddMinutes(2));

        var candidate = Assert.Single(monitor.GetRecentCandidates());
        Assert.Equal(IGateCandidateState.AlreadySeenOnAprsIs, candidate.CandidateState);
        Assert.Equal(IGateDuplicateState.DuplicateWithinWindow, candidate.DuplicateState);
        Assert.True(candidate.WasAlsoSeenOnAprsIs);
        Assert.Contains("later seen", candidate.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TcpipOrQConstructPath_IsMarkedAlreadySeenOnAprsIs()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration());

        var candidate = monitor.AcceptRfPacket(
            Parse("MOBILE1>APRS,TCPIP*,qAC,T2TEST:!3903.50N/08430.50W>Mobile"),
            "RF");

        Assert.Equal(IGateCandidateState.AlreadySeenOnAprsIs, candidate.CandidateState);
        Assert.Equal("qAC", candidate.QConstruct);
        Assert.True(candidate.WasAlsoSeenOnAprsIs);
        Assert.Contains("q construct", candidate.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicateOutsideWindow_RemainsCandidate()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration() with
        {
            DuplicateDetectionWindow = TimeSpan.FromMinutes(5)
        });
        monitor.AcceptAprsIsPacket(Parse("MOBILE1>APRS,TCPIP*,qAC,T2TEST:!3903.50N/08430.50W>Mobile"), "T2TEST", Now);

        var candidate = monitor.AcceptRfPacket(
            Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", Now.AddMinutes(6)),
            "RF",
            Now.AddMinutes(6));

        Assert.Equal(IGateCandidateState.Candidate, candidate.CandidateState);
        Assert.Equal(IGateDuplicateState.NotSeen, candidate.DuplicateState);
    }

    [Fact]
    public void ExpireCandidates_MarksOldCandidatesExpired()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration() with
        {
            CandidateRetentionTime = TimeSpan.FromMinutes(10)
        });
        monitor.AcceptRfPacket(Parse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile"), "RF", Now);

        monitor.ExpireCandidates(Now.AddMinutes(11));

        var candidate = Assert.Single(monitor.GetRecentCandidates());
        Assert.Equal(IGateCandidateState.Expired, candidate.CandidateState);
        Assert.Equal(1, monitor.GetSummary().ExpiredCount);
    }

    [Fact]
    public void CandidateList_EnforcesMaximumSize()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration() with { MaximumCandidateListSize = 2 });

        monitor.AcceptRfPacket(Parse("CALL1>APRS:!3901.00N/08401.00W>One"), "RF", Now);
        monitor.AcceptRfPacket(Parse("CALL2>APRS:!3902.00N/08402.00W>Two"), "RF", Now.AddSeconds(1));
        monitor.AcceptRfPacket(Parse("CALL3>APRS:!3903.00N/08403.00W>Three"), "RF", Now.AddSeconds(2));

        var candidates = monitor.GetRecentCandidates();
        Assert.Equal(2, candidates.Count);
        Assert.DoesNotContain(candidates, candidate => candidate.SourceCallsign == "CALL1");
    }

    [Fact]
    public void ClearCandidates_RemovesStoredCandidatesAndSummaryCounts()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration());
        monitor.AcceptRfPacket(Parse("MOBILE1>APRS:!3903.50N/08430.50W>Mobile"), "RF");

        monitor.ClearCandidates();

        Assert.Empty(monitor.GetRecentCandidates());
        Assert.Equal(0, monitor.GetSummary().TotalCandidates);
    }

    [Fact]
    public void Summary_TracksLastRfAndAprsIsPackets()
    {
        var monitor = new IGateMonitorService(EnabledConfiguration());
        monitor.AcceptRfPacket(Parse("MOBILE1>APRS:!3903.50N/08430.50W>Mobile"), "RF", Now);
        monitor.AcceptAprsIsPacket(Parse("WX9XYZ>APRS,TCPIP*,qAC,T2TEST:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132"), "T2TEST", Now.AddMinutes(1));

        var summary = monitor.GetSummary();

        Assert.True(summary.MonitorEnabled);
        Assert.Equal(Now, summary.LastRfPacketUtc);
        Assert.Equal(Now.AddMinutes(1), summary.LastAprsIsPacketUtc);
        Assert.Equal(1, summary.TotalCandidates);
    }

    private AprsPacket Parse(string rawLine)
    {
        return Parse(rawLine, Now);
    }

    private AprsPacket Parse(string rawLine, DateTimeOffset receivedAtUtc)
    {
        return parser.Parse(rawLine, receivedAtUtc);
    }

    private static IGateMonitorConfiguration EnabledConfiguration()
    {
        return IGateMonitorConfiguration.Default with
        {
            MonitorEnabled = true,
            RfToAprsIsCandidateDetectionEnabled = true,
            LocalRfPortsToMonitor = ["RF", "Direwolf TCP KISS"]
        };
    }
}
