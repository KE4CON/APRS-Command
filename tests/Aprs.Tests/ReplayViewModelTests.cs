using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class ReplayViewModelTests
{
    [Fact]
    public void ViewModelExposesInitialStateAndTransmitDisabled()
    {
        var viewModel = new ReplayViewModel(new ReplayService(new NoOpReplayPacketSink()));

        Assert.Equal("Stopped", viewModel.State);
        Assert.Equal("0 / 0", viewModel.CurrentPositionText);
        Assert.Equal("Replay transmit disabled", viewModel.TransmitStatusText);
    }

    [Fact]
    public void ViewModelLoadsReplayFileAndShowsProgress()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aprs-replay-vm-{Guid.NewGuid():N}.log");
        File.WriteAllLines(path, ["N0CALL>APRS:>Replay"]);
        var viewModel = new ReplayViewModel(new ReplayService(new NoOpReplayPacketSink()));
        viewModel.SelectedReplayFilePath = path;

        try
        {
            viewModel.LoadCommand.Execute(null);

            Assert.Equal("Ready", viewModel.State);
            Assert.Equal(1, viewModel.TotalPackets);
            Assert.Single(viewModel.Entries);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ViewModelPlayUpdatesPosition()
    {
        var service = new ReplayService(new NoOpReplayPacketSink());
        service.LoadEntries(
        [
            new RawPacketLogEntry(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "N0CALL>APRS:>Replay",
                "Status",
                "N0CALL",
                "APRS",
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
                null)
        ]);
        var viewModel = new ReplayViewModel(service);

        viewModel.PlayCommand.Execute(null);

        Assert.Equal("Completed", viewModel.State);
        Assert.Equal("1 / 1", viewModel.CurrentPositionText);
        Assert.Equal("100%", viewModel.ProgressText);
    }
}
