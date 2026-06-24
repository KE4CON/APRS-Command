using Aprs.Core;
using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class RfDiagnosticsViewModelTests
{
    [Fact]
    public void ViewModelExposesSummaryAndRecentPackets()
    {
        var parser = new AprsParser();
        var service = new RfDiagnosticsService();
        parser.TryParse("MOBILE1>APRS,WIDE1-1:!3903.50N/08430.50W>Mobile", DateTimeOffset.UtcNow, out var packet, out _);
        service.AcceptPacket(packet!, AprsPacketSource.Rf, "RF");

        var viewModel = new RfDiagnosticsViewModel(service);

        Assert.Equal("1", viewModel.TotalPacketsText);
        Assert.Equal("1", viewModel.RfPacketsText);
        Assert.Single(viewModel.RecentPackets);
        Assert.Contains(viewModel.StationRates, rate => rate.StartsWith("MOBILE1:", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClearCommandClearsDiagnostics()
    {
        var parser = new AprsParser();
        var service = new RfDiagnosticsService();
        parser.TryParse("MOBILE1>APRS:>One", DateTimeOffset.UtcNow, out var packet, out _);
        service.AcceptPacket(packet!, AprsPacketSource.Rf, "RF");
        var viewModel = new RfDiagnosticsViewModel(service);

        viewModel.ClearCommand.Execute(null);

        Assert.Equal("0", viewModel.TotalPacketsText);
        Assert.Empty(viewModel.RecentPackets);
    }
}
