namespace Aprs.Desktop.Configuration;

/// <summary>
/// Persisted collection of session templates.
/// Built-in templates are always present and cannot be deleted.
/// Custom templates are created, edited, and deleted by the operator.
/// </summary>
public sealed record SessionTemplateSettings(IReadOnlyList<SessionTemplate> Templates)
{
    public static IReadOnlyList<SessionTemplate> BuiltInTemplates { get; }
    public static SessionTemplateSettings Default { get; }

    static SessionTemplateSettings()
    {
        BuiltInTemplates = new List<SessionTemplate>
        {
            SessionTemplate.Create(
                name:                "Public Service Event",
                description:        "Marathon, parade, bike ride, or other community event requiring position tracking and message coordination between field operators.",
                eventType:          SessionEventType.PublicService,
                isBuiltIn:          true,
                beaconPath:         "WIDE1-1,WIDE2-1",
                beaconComment:      "Public Service",
                aprsIsBeaconMinutes:5,
                filterRadiusMiles:  50,
                operatorNotes:      "Confirm net frequency and net control callsign before the event. Enable APRS-IS transmit. Place objects at start line, finish line, and any aid stations. Use Net Control window to manage check-ins."),

            SessionTemplate.Create(
                name:                "Emergency Communications (ARES/RACES)",
                description:        "Emergency activation supporting a served agency. Optimized for reliable position reporting and message traffic in austere conditions.",
                eventType:          SessionEventType.EmergencyComms,
                isBuiltIn:          true,
                beaconPath:         "WIDE1-1,WIDE2-1",
                beaconComment:      "EMCOMM ACTIVATED",
                aprsIsBeaconMinutes:5,
                filterRadiusMiles:  100,
                operatorNotes:      "Verify ICS check-in with served agency. Use Events menu to start After-Action Report logging. Enable APRS-IS transmit. Document all message traffic — ICS-213 forms generated automatically from sent messages."),

            SessionTemplate.Create(
                name:                "Weekly Check-In Net",
                description:        "Regular scheduled net with roll call, announcements, and operator check-ins. Optimized for net control operations.",
                eventType:          SessionEventType.Net,
                isBuiltIn:          true,
                beaconPath:         "WIDE1-1,WIDE2-1",
                beaconComment:      "Net Control",
                aprsIsBeaconMinutes:10,
                filterRadiusMiles:  75,
                operatorNotes:      "Open Net Control window before the net starts. Use F1-F8 keyboard shortcuts for rapid roster updates. Announce net frequency in beacon comment if desired. Export ICS-211 check-in list after net closes."),

            SessionTemplate.Create(
                name:                "SOTA / POTA Portable Operation",
                description:        "Summits on the Air or Parks on the Air portable activation. Optimized for announcing your portable location to chasers.",
                eventType:          SessionEventType.PortableOperation,
                isBuiltIn:          true,
                beaconPath:         "WIDE1-1",
                beaconComment:      "POTA/SOTA Portable",
                aprsIsBeaconMinutes:10,
                filterRadiusMiles:  200,
                operatorNotes:      "Update your beacon comment with the specific summit or park reference (e.g. SOTA W4T/SU-001 or POTA K-0001). Use a single-hop path (WIDE1-1 only) to reduce network congestion from a hilltop site that may have excellent propagation."),

            SessionTemplate.Create(
                name:                "Skywarn Weather Spotter",
                description:        "Skywarn activation for severe weather spotting and reporting. Optimized for weather station display and NWS alert monitoring.",
                eventType:          SessionEventType.Skywarn,
                isBuiltIn:          true,
                beaconPath:         "WIDE1-1,WIDE2-1",
                beaconComment:      "Skywarn Spotter Active",
                aprsIsBeaconMinutes:5,
                filterRadiusMiles:  100,
                operatorNotes:      "Enable NWS alerts in the Weather menu. Monitor weather station data. Report position updates as you move to spotting locations. Coordinate with local NWS office for reporting frequency and format."),
        };
        Default = new SessionTemplateSettings(BuiltInTemplates.ToList());
    }
}
