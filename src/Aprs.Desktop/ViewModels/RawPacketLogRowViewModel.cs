using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class RawPacketLogRowViewModel
{
    public RawPacketLogRowViewModel(RawPacketLogEntry entry)
    {
        TimestampUtc = entry.TimestampUtc;
        Timestamp = entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
        Direction = entry.Direction.ToString();
        PacketSource = entry.PacketSource;
        Source = FormatSource(entry);
        Callsign = entry.SourceCallsign ?? "-";
        PacketType = entry.ParsedPacketType ?? "Unknown";
        RawPacketText = entry.RawPacketText;
        ValidationStatus = entry.ValidationStatus.ToString();
        Notes = entry.Notes ?? entry.RelatedTransmitResult ?? string.Empty;
    }

    public DateTimeOffset TimestampUtc { get; }

    public string Timestamp { get; }

    public string Direction { get; }

    public AprsPacketSource PacketSource { get; }

    public string Source { get; }

    public string Callsign { get; }

    public string PacketType { get; }

    public string RawPacketText { get; }

    public string ValidationStatus { get; }

    public string Notes { get; }

    private static string FormatSource(RawPacketLogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.SourcePortName))
        {
            return $"{entry.PacketSource} / {entry.SourcePortName}";
        }

        if (!string.IsNullOrWhiteSpace(entry.SourcePortId))
        {
            return $"{entry.PacketSource} / {entry.SourcePortId}";
        }

        return entry.PacketSource.ToString();
    }
}
