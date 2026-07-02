using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Aprs.Desktop.Configuration;
using Aprs.Mapping;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class ObjectEditorViewModel : INotifyPropertyChanged
{
    private readonly IAprsObjectEditorService editorService;
    private readonly AprsSymbolLookupService symbolService = AprsSymbolLookupService.Default;
    private AprsSymbol? selectedSymbol;

    public ObjectEditorViewModel(IAprsObjectEditorService editorService, AprsObjectEditModel model)
    {
        this.editorService = editorService;
        AvailableSymbols = new ObservableCollection<AprsSymbol>(
            symbolService.GetKnownSymbols().Where(s => s.IsPrimaryTable));
        Load(model);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AprsSymbol> AvailableSymbols { get; }

    public AprsSymbol? SelectedSymbol
    {
        get => selectedSymbol;
        set
        {
            if (ReferenceEquals(selectedSymbol, value)) return;
            selectedSymbol = value;
            if (value is not null)
            {
                SymbolTableIdentifier = value.SymbolTableIdentifier.ToString();
                SymbolCode = value.SymbolCode.ToString();
                OnPropertyChanged(nameof(SymbolDisplay));
            }
            OnPropertyChanged();
        }
    }

    public string SymbolDisplay => selectedSymbol is not null
        ? $"{selectedSymbol.Description}  ({SymbolTableIdentifier}{SymbolCode})"
        : $"{SymbolTableIdentifier}{SymbolCode}";

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

    public string OwnerCallsign { get; set; } = StationProfile.Load().FullCallsign;

    // ── Area object shape properties ──────────────────────────────────────────

    /// <summary>When true, this object is encoded as an APRS area object using the \l symbol.</summary>
    public bool IsAreaObject { get; set; }

    public AprsAreaObjectShape AreaShape { get; set; } = AprsAreaObjectShape.OpenBlueRectangle;

    public int AreaColorCode { get; set; } = 1; // blue

    public double AreaWidthKm { get; set; } = 1.0;

    public double AreaHeightKm { get; set; } = 1.0;

    public static IReadOnlyList<(AprsAreaObjectShape Shape, string Label)> AreaShapes { get; } =
    [
        (AprsAreaObjectShape.OpenBlackCircle,   "Open Circle (black)"),
        (AprsAreaObjectShape.OpenRedLine,       "Open Line (red)"),
        (AprsAreaObjectShape.OpenBlueTriangle,  "Open Triangle (blue)"),
        (AprsAreaObjectShape.OpenBlueRectangle, "Open Rectangle (blue)"),
        (AprsAreaObjectShape.FilledBlackCircle, "Filled Circle (black)"),
        (AprsAreaObjectShape.FilledRedLine,     "Filled Line (red)"),
        (AprsAreaObjectShape.FilledBlueTriangle,"Filled Triangle (blue)"),
        (AprsAreaObjectShape.FilledBlueRectangle,"Filled Rectangle (blue)"),
    ];

    public static IReadOnlyList<(int Code, string Label)> AreaColors { get; } =
    [
        (0, "Black"), (1, "Blue"), (2, "Green"), (3, "Cyan"),
        (4, "Red"),   (5, "Violet"), (6, "Amber"), (7, "White"),
    ];

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string ValidationSummary { get; private set; } = "Not validated";

    public string WarningSummary { get; private set; } = "Object transmit is disabled in this phase.";

    public string PacketPreview { get; private set; } = string.Empty;

    public AprsObjectEditModel ToModel()
    {
        // When IsAreaObject is enabled, override symbol and prepend area encoding to comment.
        var (tableId, code, comment) = IsAreaObject
            ? (AprsAreaObjectEncoder.AreaSymbol.Table,
               AprsAreaObjectEncoder.AreaSymbol.Code,
               AprsAreaObjectEncoder.Encode(AreaShape, AreaColorCode, AreaWidthKm, AreaHeightKm,
                   string.IsNullOrWhiteSpace(Comment) ? null : Comment))
            : (FirstCharOrNull(SymbolTableIdentifier), FirstCharOrNull(SymbolCode), Comment);

        return new AprsObjectEditModel(
            ObjectName,
            Latitude,
            Longitude,
            tableId,
            code,
            FirstCharOrNull(Overlay),
            comment,
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
        selectedSymbol = AvailableSymbols.FirstOrDefault(
            s => s.SymbolTableIdentifier.ToString() == SymbolTableIdentifier && s.SymbolCode.ToString() == SymbolCode);
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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
