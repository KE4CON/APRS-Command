using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class RawPacketLogViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ViewModelLoadsEntries()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(4, viewModel.RowCount);
        Assert.Contains(viewModel.Rows, row => row.Callsign == "N0CALL");
    }

    [Fact]
    public void SearchFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SearchText = "Weather";

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("WX9XYZ", row.Callsign);
    }

    [Fact]
    public void PacketSourceFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedPacketSourceFilter = nameof(AprsPacketSource.TcpKiss);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal(AprsPacketSource.TcpKiss, row.PacketSource);
    }

    [Fact]
    public void DirectionFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedDirectionFilter = nameof(RawPacketLogDirection.Blocked);

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("Blocked", row.Direction);
    }

    [Fact]
    public void PacketTypeFilterWorks()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedPacketTypeFilter = "Weather";

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("Weather", row.PacketType);
    }

    [Fact]
    public void ClearCommandClearsLog()
    {
        var viewModel = CreateViewModel();

        viewModel.ClearLogCommand.Execute(null);

        Assert.Equal(0, viewModel.RowCount);
        Assert.Empty(viewModel.Rows);
    }

    private static RawPacketLogViewModel CreateViewModel()
    {
        var service = new RawPacketLogService(clock: new FakeClock { UtcNow = Now });
        service.AddReceivedRawPacket("N0CALL>APRS:>Net control", AprsPacketSource.AprsIs, "aprs-is", "APRS-IS", Now);
        service.AddReceivedRawPacket("W1AW-9>APRS,WIDE1-1:=4123.45N/07234.56W>Mobile", AprsPacketSource.TcpKiss, "tcp", "TCP KISS", Now.AddSeconds(1));
        service.AddReceivedRawPacket("WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132Weather", AprsPacketSource.Rf, "rf", "RF", Now.AddSeconds(2));
        service.AddBlockedPacket("BAD>APRS:>Blocked", AprsPacketSource.Rf, "rf", "RF", Now.AddSeconds(3), "Safety gate blocked transmit");

        return new RawPacketLogViewModel(service);
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
