using Aprs.Core;

namespace Aprs.Services;

/// <summary>
/// Central receive pipeline. Takes a raw APRS text line from any transport,
/// records it in the raw packet log, parses it, and applies it to the station
/// database. Raises <see cref="PacketIngested"/> after each line so the UI can
/// refresh. This type has no UI or transport dependencies; transports call
/// <see cref="IngestReceivedLine"/> and the desktop layer subscribes.
/// </summary>
public sealed class AprsIngestionService
{
    private readonly IAprsParser parser;
    private readonly IStationDatabase stationDatabase;
    private readonly IRawPacketLogService rawPacketLog;

    public AprsIngestionService(
        IAprsParser parser,
        IStationDatabase stationDatabase,
        IRawPacketLogService rawPacketLog)
    {
        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        this.stationDatabase = stationDatabase ?? throw new ArgumentNullException(nameof(stationDatabase));
        this.rawPacketLog = rawPacketLog ?? throw new ArgumentNullException(nameof(rawPacketLog));
    }

    /// <summary>Raised after a line has been recorded, parsed, and applied.</summary>
    public event EventHandler? PacketIngested;

    /// <summary>Raised with the parsed packet and its source after ingestion. Packet is null if the line could not be parsed.</summary>
    public event EventHandler<ParsedPacketEventArgs>? PacketParsed;

    /// <summary>
    /// Ingests a single received raw APRS line. Safe to call repeatedly; never throws
    /// on malformed input (the parser flags validation errors instead).
    /// </summary>
    public void IngestReceivedLine(string rawLine, AprsPacketSource source, DateTimeOffset receivedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return;
        }

        rawPacketLog.AddReceivedRawPacket(rawLine, source, timestampUtc: receivedAtUtc);

        AprsPacket? packet = null;
        if (parser.TryParse(rawLine, receivedAtUtc, out packet, out _) && packet is not null)
        {
            stationDatabase.ProcessPacket(packet, source);
        }

        PacketParsed?.Invoke(this, new ParsedPacketEventArgs(packet, source));
        PacketIngested?.Invoke(this, EventArgs.Empty);
    }
}
