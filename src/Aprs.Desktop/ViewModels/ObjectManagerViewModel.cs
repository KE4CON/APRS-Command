using Aprs.Core;
using Aprs.Desktop.Runtime;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class ObjectManagerViewModel
{
    private readonly IAprsObjectManager objectManager;
    private readonly IAprsObjectEditorService editorService;
    private ObjectTransmitService? transmitService;
    private readonly List<ObjectListRowViewModel> objects = [];

    public ObjectManagerViewModel(IAprsObjectManager objectManager, IAprsObjectEditorService editorService,
        ObjectTransmitService? transmitService = null)
    {
        this.objectManager  = objectManager;
        this.editorService  = editorService;
        this.transmitService = transmitService;
        Editor = new ObjectEditorViewModel(editorService, editorService.CreateNewDraft(DateTimeOffset.UtcNow));
        NewObjectCommand     = new DesktopCommand(CreateNewObject);
        EditSelectedCommand  = new DesktopCommand(EditSelectedObject);
        SaveCommand          = new DesktopCommand(() => Save());
        CancelCommand        = new DesktopCommand(Cancel);
        MarkKilledCommand    = new DesktopCommand(() => MarkKilled());
        DeleteCommand        = new DesktopCommand(() => DeleteSelectedObject());
        TransmitNowCommand   = new DesktopCommand(async () => await TransmitSelectedAsync());
        TransmitAllCommand   = new DesktopCommand(async () => await TransmitAllAsync());
        Refresh();
    }

    public IReadOnlyList<ObjectListRowViewModel> Objects => objects;

    public ObjectListRowViewModel? SelectedObject { get; set; }

    public ObjectEditorViewModel Editor { get; }

    public string StatusText { get; private set; } = "No object saved yet.";

    public IReadOnlyList<AprsObjectState> GetObjectStates()
    {
        return objectManager.GetAllObjects();
    }

    public DesktopCommand NewObjectCommand { get; }

    public DesktopCommand EditSelectedCommand { get; }

    public DesktopCommand SaveCommand { get; }

    public DesktopCommand CancelCommand { get; }

    public DesktopCommand MarkKilledCommand { get; }

    public DesktopCommand DeleteCommand { get; }

    public DesktopCommand TransmitNowCommand { get; }

    public DesktopCommand TransmitAllCommand { get; }

    public static ObjectManagerViewModel CreateDesignTime()
    {
        var manager = new AprsObjectManager();
        var profileService = new LocalStationProfileService(new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
        profileService.UpdateProfile(profileService.GetCurrentProfile() with
        {
            Callsign = "N0CALL",
            FixedLatitude = 39.058333,
            FixedLongitude = -84.508333
        }, new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));
        var parser = new AprsParser();
        manager.AcceptPacket(parser.Parse("OBJ1>APRS:;CHECKPNT1*111111z3903.50N/08430.50W-Checkpoint 1", DateTimeOffset.UtcNow), AprsPacketSource.Simulation);
        manager.AcceptPacket(parser.Parse("ITEM1>APRS:)REPEATER!3903.50N/08430.50WrLocal repeater", DateTimeOffset.UtcNow), AprsPacketSource.Simulation);
        return new ObjectManagerViewModel(manager, new AprsObjectEditorService(manager, profileService));
    }

    public void CreateNewObject()
    {
        Editor.Load(editorService.CreateNewDraft(DateTimeOffset.UtcNow));
        StatusText = "New local object draft.";
    }

    public void EditSelectedObject()
    {
        if (SelectedObject is null)
        {
            StatusText = "Select an object to edit.";
            return;
        }

        var model = editorService.LoadForEditing(SelectedObject.Name, DateTimeOffset.UtcNow);
        if (model is null)
        {
            StatusText = "Selected object was not found.";
            return;
        }

        Editor.Load(model);
        StatusText = $"Editing {SelectedObject.Name}.";
    }

    public bool SelectObjectByName(string objectName)
    {
        var row = objects.FirstOrDefault(candidate => string.Equals(candidate.Name, objectName, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return false;
        }

        SelectedObject = row;
        EditSelectedObject();
        return true;
    }

    public void CreateObjectDraftAt(double latitude, double longitude)
    {
        var draft = editorService.CreateNewDraft(DateTimeOffset.UtcNow) with
        {
            Latitude = latitude,
            Longitude = longitude
        };
        Editor.Load(draft);
        Editor.Validate();
        StatusText = $"Object draft placed at {latitude:0.0000}, {longitude:0.0000}.";
    }

    public bool MoveObjectTo(string objectName, double latitude, double longitude, bool adoptIfRemote = false)
    {
        var model = editorService.LoadForEditing(objectName, DateTimeOffset.UtcNow);
        if (model is null)
        {
            StatusText = "Selected object was not found.";
            return false;
        }

        if (!model.IsLocallyOwned && !model.IsAdopted && !adoptIfRemote)
        {
            Editor.Load(model);
            Editor.Validate();
            StatusText = "Remote-owned objects must be adopted before local movement.";
            return false;
        }

        var moved = model with
        {
            Latitude = latitude,
            Longitude = longitude,
            IsAdopted = model.IsAdopted || adoptIfRemote
        };
        var result = editorService.Save(moved, DateTimeOffset.UtcNow);
        Editor.Load(result.Model);
        StatusText = result.IsSuccess ? $"Moved {objectName} locally." : string.Join("; ", result.Errors);
        Refresh();
        SelectObjectByName(objectName);
        return result.IsSuccess;
    }

    public AprsObjectEditorSaveResult Save()
    {
        var result = editorService.Save(Editor.ToModel(), DateTimeOffset.UtcNow);
        Editor.Load(result.Model);
        StatusText = result.IsSuccess
            ? $"Saved {result.ObjectState!.Name}. Click 'Transmit Now' to send to the network."
            : string.Join("; ", result.Errors);
        Refresh();
        return result;
    }

    public void Cancel()
    {
        Editor.Load(editorService.CreateNewDraft(DateTimeOffset.UtcNow));
        StatusText = "Edit cancelled.";
    }

    public AprsObjectEditorSaveResult MarkKilled()
    {
        var result = editorService.MarkKilled(Editor.ToModel(), DateTimeOffset.UtcNow);
        Editor.Load(result.Model);
        StatusText = result.IsSuccess ? $"Marked {result.ObjectState!.Name} killed locally." : string.Join("; ", result.Errors);
        Refresh();
        return result;
    }

    public bool DeleteSelectedObject()
    {
        if (SelectedObject is null)
        {
            StatusText = "Select a local object to delete.";
            return false;
        }

        var removed = editorService.DeleteLocalObject(SelectedObject.Name);
        StatusText = removed ? $"Removed {SelectedObject.Name}." : "Only local or adopted objects can be removed here.";
        Refresh();
        return removed;
    }

    public bool CanTransmit => transmitService is not null;

    public void SetTransmitService(ObjectTransmitService service)
    {
        transmitService = service;
    }

    public void Refresh()
    {
        objects.Clear();
        objects.AddRange(objectManager.GetAllObjects().Select(state => new ObjectListRowViewModel(state)));
    }

    private async Task TransmitSelectedAsync()
    {
        if (transmitService is null) { StatusText = "Transmit service not available — check APRS-IS connection."; return; }
        var name = SelectedObject?.Name ?? Editor.ObjectName;
        if (string.IsNullOrWhiteSpace(name)) { StatusText = "Select or save an object first."; return; }
        StatusText = $"Transmitting {name}…";
        var result = await transmitService.TransmitObjectAsync(name).ConfigureAwait(false);
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => StatusText = result);
    }

    private async Task TransmitAllAsync()
    {
        if (transmitService is null) { StatusText = "Transmit service not available — check APRS-IS connection."; return; }
        StatusText = "Transmitting all local objects…";
        var count = await transmitService.TransmitAllLocalObjectsAsync().ConfigureAwait(false);
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            StatusText = count == 0 ? "No local objects to transmit." : $"Transmitted {count} object(s).");
    }
}
