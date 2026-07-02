namespace Aprs.Desktop.Configuration;

/// <summary>
/// The category of event this session template is designed for.
/// </summary>
public enum SessionEventType
{
    /// <summary>Public service event — parade, marathon, bike ride, etc.</summary>
    PublicService,

    /// <summary>Emergency communications activation — ARES, RACES, or served-agency support.</summary>
    EmergencyComms,

    /// <summary>Organized net — weekly check-in net, training net, or special-event net.</summary>
    Net,

    /// <summary>SOTA (Summits on the Air) or POTA (Parks on the Air) portable operation.</summary>
    PortableOperation,

    /// <summary>Skywarn or other weather spotter activation.</summary>
    Skywarn,

    /// <summary>Operator-defined custom event type.</summary>
    Custom
}

/// <summary>
/// A session template pre-configures APRS Command for a specific type of event or operation.
/// Applying a template writes the configured values into the active settings without overwriting
/// fields the template does not touch (such as callsign and location).
/// </summary>
public sealed record SessionTemplate(
    string Id,
    string Name,
    string Description,
    SessionEventType EventType,
    bool IsBuiltIn,

    // Beacon settings
    string? BeaconPath,
    string? BeaconComment,
    int? AprsIsBeaconMinutes,

    // Filter
    int? FilterRadiusMiles,

    // Notes visible in the template picker
    string? OperatorNotes,

    // Message templates to activate when this session starts
    IReadOnlyList<string> MessageTemplateIds,

    // Net script to activate
    string? NetScriptId)
{
    public static SessionTemplate Create(
        string name,
        string description,
        SessionEventType eventType,
        bool isBuiltIn,
        string? beaconPath = null,
        string? beaconComment = null,
        int? aprsIsBeaconMinutes = null,
        int? filterRadiusMiles = null,
        string? operatorNotes = null,
        IReadOnlyList<string>? messageTemplateIds = null,
        string? netScriptId = null)
        => new(
            Id:                   Guid.NewGuid().ToString("N")[..8],
            Name:                 name,
            Description:          description,
            EventType:            eventType,
            IsBuiltIn:            isBuiltIn,
            BeaconPath:           beaconPath,
            BeaconComment:        beaconComment,
            AprsIsBeaconMinutes:  aprsIsBeaconMinutes,
            FilterRadiusMiles:    filterRadiusMiles,
            OperatorNotes:        operatorNotes,
            MessageTemplateIds:   messageTemplateIds ?? [],
            NetScriptId:          netScriptId);
}
