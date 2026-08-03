using Aprs.Core;
using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Covers replay-review isolation: replayed packets land in a dedicated station database
/// (never the live one), and the Replay view model drives the map controller in/out of
/// replay mode.
/// </summary>
public sealed class ReplayIsolationTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReplayPacketsGoToTheReplayDatabaseAndLeaveLiveUntouched()
    {
        var live = new StationDatabase();
        var replay = new StationDatabase();
        var ingestion = new AprsIngestionService(
            new AprsParser(), live, new RawPacketLogService(new AprsParser()), replay);

        ingestion.IngestReceivedLine("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Live", AprsPacketSource.AprsIs, Now);
        ingestion.IngestReceivedLine("W1AW>APRS,TCPIP*:!3903.50N/08430.50W-Replay", AprsPacketSource.Replay, Now);

        // Live packet only in live; replayed packet only in replay.
        Assert.Single(live.GetAllStations());
        Assert.Single(replay.GetAllStations());
    }

    [Fact]
    public void WithNoReplayDatabaseEverythingFallsBackToLive()
    {
        var live = new StationDatabase();
        var ingestion = new AprsIngestionService(
            new AprsParser(), live, new RawPacketLogService(new AprsParser()));

        ingestion.IngestReceivedLine("N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-A", AprsPacketSource.AprsIs, Now);
        ingestion.IngestReceivedLine("W1AW>APRS,TCPIP*:!3903.50N/08430.50W-B", AprsPacketSource.Replay, Now);

        Assert.Equal(2, live.GetAllStations().Count);
    }

    [Fact]
    public async Task PlayEntersReplayModeAndReturnToLiveExitsIt()
    {
        var service = new ReplayService(new NoOpReplayPacketSink());
        service.LoadEntries([CreateEntry("N0CALL>APRS:>Replay me")]);
        var viewModel = new ReplayViewModel(service);
        var controller = new FakeMapController();
        viewModel.SetMapController(controller);

        await viewModel.PlayAsync();

        Assert.True(controller.EnterCalls >= 1);
        Assert.True(viewModel.IsReplayMode);
        Assert.False(viewModel.IsStreaming); // finished streaming the single entry

        viewModel.ReturnToLive();

        Assert.Equal(1, controller.ExitCalls);
        Assert.False(controller.IsReplayMode);
        Assert.False(viewModel.IsReplayMode);
    }

    private static RawPacketLogEntry CreateEntry(string rawPacket)
    {
        return new RawPacketLogEntry(
            Guid.NewGuid(),
            Now,
            rawPacket,
            null,
            null,
            null,
            [],
            AprsPacketSource.AprsIs,
            RawPacketLogDirection.Received,
            null,
            null,
            RawPacketValidationStatus.Valid,
            [],
            [],
            true,
            null,
            null);
    }

    private sealed class FakeMapController : IReplayMapController
    {
        public bool IsReplayMode { get; private set; }

        public int EnterCalls { get; private set; }

        public int ExitCalls { get; private set; }

        public void EnterReplayMode()
        {
            IsReplayMode = true;
            EnterCalls++;
        }

        public void ExitReplayMode()
        {
            IsReplayMode = false;
            ExitCalls++;
        }
    }
}
