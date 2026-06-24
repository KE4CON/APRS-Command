using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsObjectEditorServiceTests
{
    private static readonly DateTimeOffset TestNow = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateNewDraft_UsesSafeDefaults()
    {
        var (_, _, editor) = CreateEditor();

        var draft = editor.CreateNewDraft(TestNow);

        Assert.True(draft.IsAlive);
        Assert.False(draft.IsKilled);
        Assert.False(draft.AprsIsTransmitEnabled);
        Assert.False(draft.RfTransmitEnabled);
        Assert.True(draft.IsLocallyOwned);
        Assert.Equal("N0CALL", draft.OwnerCallsign);
        Assert.Contains(draft.ValidationWarnings, warning => warning.Contains("disabled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Save_ValidObjectAddsLocalObjectToManager()
    {
        var (manager, _, editor) = CreateEditor();
        var draft = ValidDraft(editor);

        var result = editor.Save(draft, TestNow.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ObjectState);
        Assert.Equal("CHECKPNT1", result.ObjectState.Name);
        Assert.True(result.ObjectState.IsLocallyCreated);
        Assert.True(result.ObjectState.IsLocallyOwned);
        Assert.Equal("N0CALL", result.ObjectState.OwnerCallsign);
        Assert.Contains(";CHECKPNT1*", result.PacketPreview);
        Assert.Same(result.ObjectState, manager.GetObject("CHECKPNT1"));
    }

    [Fact]
    public void Validate_InvalidObjectNameFails()
    {
        var (_, _, editor) = CreateEditor();

        var validation = editor.Validate(ValidDraft(editor) with { ObjectName = "TOO-LONG-NAME" });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("9 characters", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(-91, -84.5, "Latitude")]
    [InlineData(39.0, -181, "Longitude")]
    public void Validate_InvalidCoordinatesFail(double latitude, double longitude, string expectedField)
    {
        var (_, _, editor) = CreateEditor();

        var validation = editor.Validate(ValidDraft(editor) with { Latitude = latitude, Longitude = longitude });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains(expectedField, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MissingSymbolFails()
    {
        var (_, _, editor) = CreateEditor();

        var validation = editor.Validate(ValidDraft(editor) with { SymbolTableIdentifier = null, SymbolCode = null });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("Symbol table", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(validation.Errors, error => error.Contains("Symbol code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_CommentWithLineBreakFails()
    {
        var (_, _, editor) = CreateEditor();

        var validation = editor.Validate(ValidDraft(editor) with { Comment = "Line 1\nLine 2" });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("line breaks", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MarkKilled_SavesKilledObjectState()
    {
        var (_, _, editor) = CreateEditor();

        var result = editor.MarkKilled(ValidDraft(editor), TestNow.AddMinutes(2));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ObjectState);
        Assert.True(result.ObjectState.IsKilled);
        Assert.False(result.ObjectState.IsAlive);
        Assert.Equal(AprsObjectLifecycleState.Killed, result.ObjectState.LifecycleState);
    }

    [Fact]
    public void LoadForEditing_RemoteOwnedObjectShowsAdoptionWarning()
    {
        var (manager, _, editor) = CreateEditor();
        manager.AcceptPacket(Parse("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1"), AprsPacketSource.AprsIs);

        var model = editor.LoadForEditing("CHECKPNT1", TestNow.AddMinutes(1));

        Assert.NotNull(model);
        Assert.False(model.IsLocallyOwned);
        Assert.Contains(model.ValidationWarnings, warning => warning.Contains("adopt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Save_RemoteOwnedObjectRequiresAdoption()
    {
        var (manager, _, editor) = CreateEditor();
        manager.AcceptPacket(Parse("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1"), AprsPacketSource.AprsIs);
        var model = editor.LoadForEditing("CHECKPNT1", TestNow.AddMinutes(1))!;

        var result = editor.Save(model with { Comment = "Local edit" }, TestNow.AddMinutes(2));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Contains("adopt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Save_AdoptedRemoteObjectSucceedsAndPreservesOwner()
    {
        var (manager, _, editor) = CreateEditor();
        manager.AcceptPacket(Parse("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1"), AprsPacketSource.AprsIs);
        var model = editor.LoadForEditing("CHECKPNT1", TestNow.AddMinutes(1))!;

        var result = editor.Save(model with { IsAdopted = true, Comment = "Adopted edit" }, TestNow.AddMinutes(2));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ObjectState);
        Assert.True(result.ObjectState.IsAdopted);
        Assert.Equal("OBJ1", result.ObjectState.OwnerCallsign);
        Assert.Equal("Adopted edit", result.ObjectState.Comment);
    }

    [Fact]
    public void Validate_TransmitEnabledFailsSafely()
    {
        var (_, _, editor) = CreateEditor();

        var validation = editor.Validate(ValidDraft(editor) with { AprsIsTransmitEnabled = true });

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("not implemented", StringComparison.OrdinalIgnoreCase));
    }

    private static AprsObjectEditModel ValidDraft(IAprsObjectEditorService editor)
    {
        return editor.CreateNewDraft(TestNow) with
        {
            ObjectName = "CHECKPNT1",
            Latitude = 39.058333,
            Longitude = -84.508333,
            SymbolTableIdentifier = '/',
            SymbolCode = '-',
            Comment = "Checkpoint 1"
        };
    }

    private static (AprsObjectManager Manager, LocalStationProfileService ProfileService, AprsObjectEditorService Editor) CreateEditor()
    {
        var manager = new AprsObjectManager();
        var profileService = new LocalStationProfileService(TestNow);
        profileService.UpdateProfile(profileService.GetCurrentProfile() with
        {
            Callsign = "N0CALL",
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333
        }, TestNow);
        var editor = new AprsObjectEditorService(manager, profileService);
        return (manager, profileService, editor);
    }

    private static AprsPacket Parse(string rawLine)
    {
        return new AprsParser().Parse(rawLine, TestNow);
    }
}
