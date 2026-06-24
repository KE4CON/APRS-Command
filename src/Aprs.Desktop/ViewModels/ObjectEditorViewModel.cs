using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class ObjectEditorViewModel
{
    private readonly IAprsObjectEditorService editorService;

    public ObjectEditorViewModel(IAprsObjectEditorService editorService, AprsObjectEditModel model)
    {
        this.editorService = editorService;
        Load(model);
    }

    public string ObjectName { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string SymbolTableIdentifier { get; set; } = "/";

    public string SymbolCode { get; set; } = "-";

    public string Overlay { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;

    public double? TransmitIntervalMinutes { get; set; }

    public bool AprsIsTransmitEnabled { get; set; }

    public bool RfTransmitEnabled { get; set; }

    public bool IsAlive { get; set; } = true;

    public bool IsKilled { get; set; }

    public bool IsLocallyOwned { get; set; } = true;

    public bool IsAdopted { get; set; }

    public string OwnerCallsign { get; set; } = "N0CALL";

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string ValidationSummary { get; private set; } = "Not validated";

    public string WarningSummary { get; private set; } = "Object transmit is disabled in this phase.";

    public string PacketPreview { get; private set; } = string.Empty;

    public AprsObjectEditModel ToModel()
    {
        return new AprsObjectEditModel(
            ObjectName,
            Latitude,
            Longitude,
            FirstCharOrNull(SymbolTableIdentifier),
            FirstCharOrNull(SymbolCode),
            FirstCharOrNull(Overlay),
            Comment,
            TransmitIntervalMinutes is null ? null : TimeSpan.FromMinutes(TransmitIntervalMinutes.Value),
            AprsIsTransmitEnabled,
            RfTransmitEnabled,
            IsAlive,
            IsKilled,
            IsLocallyOwned,
            IsAdopted,
            OwnerCallsign,
            CreatedAtUtc,
            UpdatedAtUtc,
            [],
            [],
            string.IsNullOrWhiteSpace(PacketPreview) ? null : PacketPreview);
    }

    public AprsObjectEditorValidationResult Validate()
    {
        var validation = editorService.Validate(ToModel());
        ValidationSummary = validation.Errors.Count == 0 ? "Ready to save" : string.Join("; ", validation.Errors);
        WarningSummary = validation.Warnings.Count == 0 ? "None" : string.Join("; ", validation.Warnings);
        PacketPreview = editorService.GeneratePacketPreview(ToModel()) ?? string.Empty;
        return validation;
    }

    public void Load(AprsObjectEditModel model)
    {
        ObjectName = model.ObjectName;
        Latitude = model.Latitude;
        Longitude = model.Longitude;
        SymbolTableIdentifier = model.SymbolTableIdentifier?.ToString() ?? string.Empty;
        SymbolCode = model.SymbolCode?.ToString() ?? string.Empty;
        Overlay = model.Overlay?.ToString() ?? string.Empty;
        Comment = model.Comment;
        TransmitIntervalMinutes = model.TransmitInterval?.TotalMinutes;
        AprsIsTransmitEnabled = model.AprsIsTransmitEnabled;
        RfTransmitEnabled = model.RfTransmitEnabled;
        IsAlive = model.IsAlive;
        IsKilled = model.IsKilled;
        IsLocallyOwned = model.IsLocallyOwned;
        IsAdopted = model.IsAdopted;
        OwnerCallsign = model.OwnerCallsign;
        CreatedAtUtc = model.CreatedAtUtc;
        UpdatedAtUtc = model.UpdatedAtUtc;
        ValidationSummary = model.ValidationErrors.Count == 0 ? "Not validated" : string.Join("; ", model.ValidationErrors);
        WarningSummary = model.ValidationWarnings.Count == 0 ? "None" : string.Join("; ", model.ValidationWarnings);
        PacketPreview = model.PacketPreview ?? string.Empty;
    }

    private static char? FirstCharOrNull(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value[0];
    }
}
