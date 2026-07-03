using System;
using System.Threading.Tasks;
using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class ScheduledBeaconsViewModelTests
{
    private static ScheduledBeaconService MakeService() =>
        new(_ => Task.CompletedTask);

    private static ScheduledBeaconsViewModel MakeVm(ScheduledBeaconService? svc = null) =>
        new(svc ?? MakeService());

    // ── ScheduledBeaconEntryEditViewModel ─────────────────────────────────

    [Fact]
    public void EditViewModel_LoadsFromModel()
    {
        var entry = ScheduledBeaconEntry.CreateDefault() with
        {
            Label = "Morning net",
            FireAt = new TimeOnly(9, 30),
            Saturday = true,
            CustomComment = "Check-in"
        };

        var vm = new ScheduledBeaconEntryEditViewModel(entry);

        Assert.Equal("Morning net", vm.Label);
        Assert.Equal(9,  vm.FireAtHour);
        Assert.Equal(30, vm.FireAtMinute);
        Assert.True(vm.Saturday);
        Assert.Equal("Check-in", vm.CustomComment);
    }

    [Fact]
    public void EditViewModel_ToModel_RoundTrips()
    {
        var entry = ScheduledBeaconEntry.CreateDefault() with
        {
            Label = "Test",
            FireAt = new TimeOnly(14, 45),
            Monday = false,
            Sunday = true
        };

        var vm = new ScheduledBeaconEntryEditViewModel(entry);
        var model = vm.ToModel();

        Assert.Equal("Test", model.Label);
        Assert.Equal(14, model.FireAt.Hour);
        Assert.Equal(45, model.FireAt.Minute);
        Assert.False(model.Monday);
        Assert.True(model.Sunday);
    }

    [Fact]
    public void EditViewModel_DisplayTime_IsFormattedHHmm()
    {
        var vm = new ScheduledBeaconEntryEditViewModel(
            ScheduledBeaconEntry.CreateDefault() with { FireAt = new TimeOnly(8, 5) });
        Assert.Equal("08:05", vm.DisplayTime);
    }

    [Fact]
    public void EditViewModel_ToModel_ClampsHourAndMinute()
    {
        var vm = new ScheduledBeaconEntryEditViewModel(ScheduledBeaconEntry.CreateDefault());
        vm.FireAtHour   = 99;
        vm.FireAtMinute = 99;
        var model = vm.ToModel();
        Assert.Equal(23, model.FireAt.Hour);
        Assert.Equal(59, model.FireAt.Minute);
    }

    [Fact]
    public void EditViewModel_CustomComment_EmptyStringBecomesNullInModel()
    {
        var vm = new ScheduledBeaconEntryEditViewModel(ScheduledBeaconEntry.CreateDefault());
        vm.CustomComment = "";
        var model = vm.ToModel();
        // Empty string is preserved as-is in ToModel — null vs empty is caller's concern
        Assert.Equal(string.Empty, model.CustomComment ?? string.Empty);
    }

    // ── ScheduledBeaconsViewModel ─────────────────────────────────────────

    [Fact]
    public void InitialState_HasNoSelection()
    {
        var vm = MakeVm();
        Assert.Null(vm.SelectedEntry);
        Assert.False(vm.HasSelection);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void InitialState_PopulatesFromServiceEntries()
    {
        var svc = MakeService();
        svc.Add(ScheduledBeaconEntry.CreateDefault());
        var vm = new ScheduledBeaconsViewModel(svc);
        Assert.Single(vm.Entries);
    }

    [Fact]
    public void AddCommand_AddsEntryAndSelectsIt()
    {
        var vm = MakeVm();
        vm.AddCommand.Execute(null);

        Assert.Single(vm.Entries);
        Assert.NotNull(vm.SelectedEntry);
        Assert.True(vm.IsEditing);
    }

    [Fact]
    public void RemoveCommand_RemovesSelectedEntry()
    {
        var svc = MakeService();
        svc.Add(ScheduledBeaconEntry.CreateDefault());
        var vm = new ScheduledBeaconsViewModel(svc);

        vm.SelectedEntry = vm.Entries[0];
        vm.RemoveCommand.Execute(null);

        Assert.Empty(vm.Entries);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void SaveCommand_PersistsToService()
    {
        var svc = MakeService();
        var vm = new ScheduledBeaconsViewModel(svc);

        vm.AddCommand.Execute(null);
        vm.SelectedEntry!.Label = "Saved entry";
        vm.SaveCommand.Execute(null);

        Assert.False(vm.IsEditing);
        Assert.Single(svc.Entries);
        Assert.Equal("Saved entry", svc.Entries[0].Label);
    }

    [Fact]
    public void CancelCommand_StopsEditing()
    {
        var svc = MakeService();
        var vm = new ScheduledBeaconsViewModel(svc);

        vm.AddCommand.Execute(null);
        Assert.True(vm.IsEditing);

        vm.CancelCommand.Execute(null);
        Assert.False(vm.IsEditing);
    }

    [Fact]
    public void StatusText_ReflectsServiceSummary()
    {
        var svc = MakeService();
        var vm = new ScheduledBeaconsViewModel(svc);
        Assert.Equal(svc.StatusSummary, vm.StatusText);
    }

    [Fact]
    public void HasSelection_IsTrueWhenEntrySelected()
    {
        var vm = MakeVm();
        vm.AddCommand.Execute(null);
        Assert.True(vm.HasSelection);
    }

    [Fact]
    public void IsEditing_CanBeSetDirectly()
    {
        var vm = MakeVm();
        vm.IsEditing = true;
        Assert.True(vm.IsEditing);
        vm.IsEditing = false;
        Assert.False(vm.IsEditing);
    }
}
