using Aprs.Desktop.Configuration;
using Aprs.Desktop.ViewModels;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Tests for the Connections view-model layer: loading from the store, add / remove / edit, and
/// saving the port list back. Uses the in-memory store so nothing touches disk.
/// </summary>
public sealed class ConnectionsViewModelTests
{
    private static ConnectionsViewModel NewViewModel(out InMemoryAppSettingsStore store, AppSettings? initial = null)
    {
        store = new InMemoryAppSettingsStore(initial);
        return new ConnectionsViewModel(store);
    }

    [Fact]
    public void Load_StartsWithTheDefaultAprsIsPort()
    {
        var vm = NewViewModel(out _);

        var port = Assert.Single(vm.Ports);
        Assert.Equal(ConnectionPortType.AprsIs, port.Type);
        Assert.False(port.TransmitEnabled);
        Assert.Same(port, vm.SelectedPort);
    }

    [Fact]
    public void AddPort_AppendsAndSelectsNewPort_OfChosenType()
    {
        var vm = NewViewModel(out _);

        vm.NewPortType = ConnectionPortType.SerialKiss;
        vm.AddPortCommand.Execute(null);

        Assert.Equal(2, vm.Ports.Count);
        Assert.Equal(ConnectionPortType.SerialKiss, vm.SelectedPort!.Type);
        Assert.True(vm.SelectedPort.IsSerial);
        Assert.NotNull(vm.SelectedPort.ToModel().Configuration.SerialKiss); // type config filled in
    }

    [Fact]
    public void AddPort_NotPersistedUntilSave()
    {
        var vm = NewViewModel(out var store);

        vm.NewPortType = ConnectionPortType.NetworkTncKiss;
        vm.AddPortCommand.Execute(null);

        Assert.Single(store.Load().Connections.Ports);   // store unchanged before save
        vm.SaveCommand.Execute(null);
        Assert.Equal(2, store.Load().Connections.Ports.Count); // now persisted
    }

    [Fact]
    public void EditFields_RoundTripThroughSave()
    {
        var vm = NewViewModel(out var store);
        vm.NewPortType = ConnectionPortType.NetworkTncKiss;
        vm.AddPortCommand.Execute(null);

        var row = vm.SelectedPort!;
        row.Name = "GrayWolf";
        row.NetworkHost = "10.0.0.20";
        row.NetworkPort = 8001;
        row.Enabled = true;
        row.ReceiveEnabled = true;
        row.TransmitEnabled = true;

        vm.SaveCommand.Execute(null);

        var saved = store.Load().Connections.Ports.Single(p => p.Name == "GrayWolf");
        Assert.Equal(ConnectionPortType.NetworkTncKiss, saved.Type);
        Assert.Equal("10.0.0.20", saved.Configuration.NetworkTncKiss!.Host);
        Assert.Equal(8001, saved.Configuration.NetworkTncKiss!.Port);
        Assert.True(saved.Enabled);
        Assert.True(saved.TransmitEnabled);
    }

    [Fact]
    public void EditAprsIsFields_RoundTrip()
    {
        var vm = NewViewModel(out var store);
        var row = vm.SelectedPort!; // the default APRS-IS port

        row.AprsIsServer = "rotate.aprs2.net";
        row.AprsIsPort = 14580;
        row.AprsIsPasscode = "12345";
        row.AprsIsFilter = "m/100";

        vm.SaveCommand.Execute(null);

        var saved = store.Load().Connections.Ports.Single(p => p.Type == ConnectionPortType.AprsIs);
        Assert.Equal("rotate.aprs2.net", saved.Configuration.AprsIs!.ServerHost);
        Assert.Equal(14580, saved.Configuration.AprsIs!.ServerPort);
        Assert.Equal("12345", saved.Configuration.AprsIs!.Passcode);
        Assert.Equal("m/100", saved.Configuration.AprsIs!.Filter);
    }

    [Fact]
    public void RemoveSelectedPort_RemovesAndReselects()
    {
        var vm = NewViewModel(out var store);
        vm.NewPortType = ConnectionPortType.SerialKiss;
        vm.AddPortCommand.Execute(null); // now 2 ports, serial selected

        vm.RemoveSelectedPortCommand.Execute(null);

        Assert.Single(vm.Ports);
        Assert.NotNull(vm.SelectedPort); // reselected the remaining one
        vm.SaveCommand.Execute(null);
        Assert.Single(store.Load().Connections.Ports);
    }

    [Fact]
    public void RemoveCommand_CannotExecuteWithNoSelection()
    {
        var vm = NewViewModel(out _);
        vm.SelectedPort = null;

        Assert.False(vm.RemoveSelectedPortCommand.CanExecute(null));
    }

    [Fact]
    public void Revert_DiscardsUnsavedEdits()
    {
        var vm = NewViewModel(out _);
        vm.NewPortType = ConnectionPortType.Agwpe;
        vm.AddPortCommand.Execute(null);
        Assert.Equal(2, vm.Ports.Count);

        vm.RevertCommand.Execute(null); // reload from store (which was never saved)

        Assert.Single(vm.Ports);
        Assert.Equal(ConnectionPortType.AprsIs, vm.Ports[0].Type);
    }

    [Fact]
    public void Save_PreservesOtherSettingsSections()
    {
        var initial = AppSettings.Default with { Station = StationProfile.Default with { Callsign = "KE4CON", Latitude = 40, Longitude = -83, FilterRadiusKm = 100 } };
        var vm = NewViewModel(out var store, initial);

        vm.SaveCommand.Execute(null);

        Assert.Equal("KE4CON", store.Load().Station.Callsign); // station section untouched by a connections save
    }

    [Fact]
    public void DesignTime_ProducesSamplePorts()
    {
        var vm = ConnectionsViewModel.CreateDesignTime();
        Assert.True(vm.Ports.Count >= 2);
    }
}
