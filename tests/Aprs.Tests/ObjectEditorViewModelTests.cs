using Aprs.Desktop.ViewModels;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class ObjectEditorViewModelTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Validate_ExposesValidationErrors()
    {
        var viewModel = CreateViewModel();
        viewModel.ObjectName = string.Empty;

        var validation = viewModel.Validate();

        Assert.False(validation.IsValid);
        Assert.Contains("Object name", viewModel.ValidationSummary);
    }

    [Fact]
    public void SaveCommand_SavesValidObjectThroughManagerViewModel()
    {
        var managerViewModel = ObjectManagerViewModel.CreateDesignTime();
        managerViewModel.CreateNewObject();
        managerViewModel.Editor.ObjectName = "LOCALOBJ";
        managerViewModel.Editor.Latitude = 39.058333;
        managerViewModel.Editor.Longitude = -84.508333;
        managerViewModel.Editor.Comment = "Local object";

        var result = managerViewModel.Save();

        Assert.True(result.IsSuccess);
        Assert.Contains(managerViewModel.Objects, row => row.Name == "LOCALOBJ");
        Assert.Contains("Saved", managerViewModel.StatusText);
    }

    [Fact]
    public void CreateDesignTime_LoadsSampleObjectsAndEditor()
    {
        var viewModel = ObjectManagerViewModel.CreateDesignTime();

        Assert.True(viewModel.Objects.Count >= 2);
        Assert.NotNull(viewModel.Editor);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Editor.OwnerCallsign));
    }

    private static ObjectEditorViewModel CreateViewModel()
    {
        var manager = new AprsObjectManager();
        var profileService = new LocalStationProfileService(TestNow);
        profileService.UpdateProfile(profileService.GetCurrentProfile() with
        {
            Callsign = "N0CALL",
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333
        }, TestNow);
        var service = new AprsObjectEditorService(manager, profileService);
        return new ObjectEditorViewModel(service, service.CreateNewDraft(TestNow));
    }
}
