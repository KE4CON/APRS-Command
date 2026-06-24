namespace Aprs.Services;

public sealed record AprsObjectEditorSaveResult(
    bool IsSuccess,
    AprsObjectState? ObjectState,
    AprsObjectEditModel Model,
    string? PacketPreview,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
