namespace Aprs.Services;

public interface IAprsObjectEditorService
{
    /// <summary>
    /// Creates a safe local APRS object draft without transmitting anything.
    /// </summary>
    AprsObjectEditModel CreateNewDraft(DateTimeOffset now);

    AprsObjectEditModel? LoadForEditing(string objectName, DateTimeOffset now);

    AprsObjectEditorValidationResult Validate(AprsObjectEditModel model);

    string? GeneratePacketPreview(AprsObjectEditModel model);

    AprsObjectEditorSaveResult Save(AprsObjectEditModel model, DateTimeOffset now);

    AprsObjectEditorSaveResult MarkKilled(AprsObjectEditModel model, DateTimeOffset now);

    bool DeleteLocalObject(string objectName);
}
