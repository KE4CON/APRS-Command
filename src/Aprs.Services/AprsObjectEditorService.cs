using System.Text.RegularExpressions;
using Aprs.Core;

namespace Aprs.Services;

public sealed partial class AprsObjectEditorService : IAprsObjectEditorService
{
    private const int MaximumObjectNameLength = 9;
    private readonly IAprsObjectManager objectManager;
    private readonly ILocalStationProfileService localStationProfileService;
    private readonly AprsParser parser = new();

    public AprsObjectEditorService(IAprsObjectManager objectManager, ILocalStationProfileService localStationProfileService)
    {
        this.objectManager = objectManager;
        this.localStationProfileService = localStationProfileService;
    }

    public AprsObjectEditModel CreateNewDraft(DateTimeOffset now)
    {
        var profile = localStationProfileService.GetCurrentProfile();
        return new AprsObjectEditModel(
            ObjectName: string.Empty,
            Latitude: profile.FixedLatitude,
            Longitude: profile.FixedLongitude,
            SymbolTableIdentifier: profile.SymbolTableIdentifier ?? '/',
            SymbolCode: profile.SymbolCode ?? '-',
            Overlay: profile.Overlay,
            Comment: string.Empty,
            TransmitInterval: null,
            AprsIsTransmitEnabled: false,
            RfTransmitEnabled: false,
            IsAlive: true,
            IsKilled: false,
            IsLocallyOwned: true,
            IsAdopted: false,
            OwnerCallsign: profile.FullStationIdentifier,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            ValidationErrors: [],
            ValidationWarnings: ["Object transmit is disabled in this phase."],
            PacketPreview: null);
    }

    public AprsObjectEditModel? LoadForEditing(string objectName, DateTimeOffset now)
    {
        var state = objectManager.GetObject(objectName);
        if (state is null)
        {
            return null;
        }

        var localCallsign = localStationProfileService.GetCurrentProfile().FullStationIdentifier;
        var warnings = new List<string>();
        if (!state.IsLocallyOwned && !state.IsAdopted && !IsSameCallsign(state.OwnerCallsign, localCallsign))
        {
            warnings.Add($"Object is owned by {state.OwnerCallsign}; mark it adopted before saving local ownership changes.");
        }

        if (!string.IsNullOrWhiteSpace(state.OwnershipWarning))
        {
            warnings.Add(state.OwnershipWarning);
        }

        return new AprsObjectEditModel(
            state.Name,
            state.Latitude,
            state.Longitude,
            state.SymbolTableIdentifier,
            state.SymbolCode,
            state.Overlay,
            state.Comment ?? string.Empty,
            TransmitInterval: null,
            AprsIsTransmitEnabled: false,
            RfTransmitEnabled: false,
            state.IsAlive,
            state.IsKilled,
            state.IsLocallyOwned || IsSameCallsign(state.OwnerCallsign, localCallsign),
            state.IsAdopted,
            state.OwnerCallsign,
            state.FirstHeardUtc,
            now,
            state.ValidationErrors,
            warnings,
            null);
    }

    public AprsObjectEditorValidationResult Validate(AprsObjectEditModel model)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var profile = localStationProfileService.GetCurrentProfile();
        var localStation = profile.FullStationIdentifier;

        if (string.IsNullOrWhiteSpace(model.ObjectName))
        {
            errors.Add("Object name is required.");
        }
        else if (model.ObjectName.Trim().Length > MaximumObjectNameLength)
        {
            errors.Add("Object name must be 9 characters or fewer.");
        }

        if (model.Latitude is null or < -90 or > 90)
        {
            errors.Add("Latitude must be between -90 and 90 degrees.");
        }

        if (model.Longitude is null or < -180 or > 180)
        {
            errors.Add("Longitude must be between -180 and 180 degrees.");
        }

        if (model.SymbolTableIdentifier is null)
        {
            errors.Add("Symbol table identifier is required.");
        }

        if (model.SymbolCode is null)
        {
            errors.Add("Symbol code is required.");
        }

        if (model.Comment.Contains('\r') || model.Comment.Contains('\n'))
        {
            errors.Add("Comment cannot contain line breaks.");
        }

        if (string.IsNullOrWhiteSpace(model.OwnerCallsign) || !StationIdentifierRegex().IsMatch(model.OwnerCallsign.Trim()))
        {
            errors.Add("Owner callsign must be a valid callsign with optional SSID.");
        }

        if (model.IsAlive == model.IsKilled)
        {
            errors.Add("Choose either alive or killed object state.");
        }

        if (!IsSameCallsign(model.OwnerCallsign, localStation) && !model.IsAdopted)
        {
            warnings.Add($"Object is owned by {model.OwnerCallsign}; adoption is required before saving local changes.");
            errors.Add("Remote-owned objects cannot be saved locally until they are marked adopted.");
        }

        if (model.AprsIsTransmitEnabled || model.RfTransmitEnabled)
        {
            var profileValidation = localStationProfileService.ValidateProfile(profile);
            if (!profileValidation.IsSafeToTransmit)
            {
                errors.Add("Object transmit cannot be enabled because the local station profile is not safe to transmit.");
            }

            errors.Add("Object transmit is not implemented in this phase.");
        }

        warnings.Add("Object packet preview is local only; no packet is transmitted in this phase.");

        return new AprsObjectEditorValidationResult(errors.Count == 0, errors, warnings);
    }

    public string? GeneratePacketPreview(AprsObjectEditModel model)
    {
        var validation = Validate(model);
        if (validation.Errors.Any(error => error.Contains("required", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Latitude", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Longitude", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Symbol", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Owner callsign", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return BuildObjectPacket(model, DateTimeOffset.UtcNow);
    }

    public AprsObjectEditorSaveResult Save(AprsObjectEditModel model, DateTimeOffset now)
    {
        var validation = Validate(model);
        var preview = validation.IsValid ? BuildObjectPacket(model, now) : null;
        if (!validation.IsValid || preview is null)
        {
            return CreateSaveResult(false, null, model, preview, validation);
        }

        var parsed = parser.Parse(preview, now);
        if (parsed is not ObjectAprsPacket objectPacket)
        {
            var errors = validation.Errors.Concat(["Generated object packet preview could not be parsed."]).ToArray();
            return new AprsObjectEditorSaveResult(false, null, model, preview, errors, validation.Warnings);
        }

        var state = objectManager.AcceptObject(objectPacket, AprsPacketSource.Simulation);
        if (state is not null)
        {
            state = objectManager.MarkLocallyCreated(state.Name, model.OwnerCallsign, now) ?? state;
            if (model.IsAdopted)
            {
                state = objectManager.AdoptObject(state.Name, model.OwnerCallsign, now) ?? state;
            }
        }

        var savedModel = model with
        {
            ObjectName = NormalizeObjectName(model.ObjectName),
            UpdatedAtUtc = now,
            ValidationErrors = validation.Errors,
            ValidationWarnings = validation.Warnings,
            PacketPreview = preview
        };

        return new AprsObjectEditorSaveResult(state is not null, state, savedModel, preview, validation.Errors, validation.Warnings);
    }

    public AprsObjectEditorSaveResult MarkKilled(AprsObjectEditModel model, DateTimeOffset now)
    {
        return Save(model with { IsAlive = false, IsKilled = true }, now);
    }

    public bool DeleteLocalObject(string objectName)
    {
        var existing = objectManager.GetObject(objectName);
        if (existing is null || (!existing.IsLocallyCreated && !existing.IsLocallyOwned && !existing.IsAdopted))
        {
            return false;
        }

        return objectManager.RemoveObject(objectName);
    }

    private static AprsObjectEditorSaveResult CreateSaveResult(
        bool success,
        AprsObjectState? state,
        AprsObjectEditModel model,
        string? preview,
        AprsObjectEditorValidationResult validation)
    {
        return new AprsObjectEditorSaveResult(success, state, model with
        {
            ValidationErrors = validation.Errors,
            ValidationWarnings = validation.Warnings,
            PacketPreview = preview
        }, preview, validation.Errors, validation.Warnings);
    }

    private static string BuildObjectPacket(AprsObjectEditModel model, DateTimeOffset now)
    {
        var name = NormalizeObjectName(model.ObjectName).PadRight(MaximumObjectNameLength)[..MaximumObjectNameLength];
        var indicator = model.IsKilled ? '_' : '*';
        var timestamp = now.ToString("HHmmss' z'").Replace(" ", string.Empty);
        var packetBody = $";{name}{indicator}{timestamp}{AprsCoordinateFormatter.FormatLatitude(model.Latitude!.Value)}{model.SymbolTableIdentifier!.Value}{AprsCoordinateFormatter.FormatLongitude(model.Longitude!.Value)}{model.SymbolCode!.Value}{model.Comment.Trim()}";
        return $"{model.OwnerCallsign.Trim().ToUpperInvariant()}>APRS:{packetBody}";
    }

    private static string NormalizeObjectName(string objectName)
    {
        return objectName.Trim().ToUpperInvariant();
    }

    private static bool IsSameCallsign(string left, string right)
    {
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[A-Z0-9]{1,6}(-([0-9]|1[0-5]))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex StationIdentifierRegex();
}
