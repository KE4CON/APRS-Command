using Aprs.Core;

namespace Aprs.Desktop.ViewModels;

/// <summary>Immutable snapshot of one telemetry packet for display in the Telemetry Monitor.</summary>
public sealed class TelemetryRecord
{
    public TelemetryRecord(TelemetryAprsPacket packet)
    {
        Callsign       = packet.SourceCallsign + (packet.SourceSsid.HasValue ? $"-{packet.SourceSsid}" : "");
        ReceivedAt     = packet.ReceivedAtUtc.ToLocalTime();
        SequenceNumber = packet.SequenceNumber?.ToString() ?? "—";
        A1             = packet.AnalogValues.Count > 0 ? packet.AnalogValues[0].ToString() : "—";
        A2             = packet.AnalogValues.Count > 1 ? packet.AnalogValues[1].ToString() : "—";
        A3             = packet.AnalogValues.Count > 2 ? packet.AnalogValues[2].ToString() : "—";
        A4             = packet.AnalogValues.Count > 3 ? packet.AnalogValues[3].ToString() : "—";
        A5             = packet.AnalogValues.Count > 4 ? packet.AnalogValues[4].ToString() : "—";
        DigitalBits    = packet.DigitalValues.Count > 0
            ? string.Concat(packet.DigitalValues.Select(b => b ? "1" : "0"))
            : "—";
        RawBody        = packet.RawTelemetryBody;
        IsValid        = packet.IsValid;
    }

    public string Callsign       { get; }
    public DateTimeOffset ReceivedAt { get; }
    public string ReceivedAtDisplay => ReceivedAt.ToString("HH:mm:ss");
    public string SequenceNumber { get; }
    public string A1             { get; }
    public string A2             { get; }
    public string A3             { get; }
    public string A4             { get; }
    public string A5             { get; }
    public string DigitalBits    { get; }
    public string RawBody        { get; }
    public bool   IsValid        { get; }
}
