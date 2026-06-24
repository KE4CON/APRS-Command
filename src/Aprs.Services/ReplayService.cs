using System.Globalization;
using Aprs.Core;

namespace Aprs.Services;

public sealed class ReplayService : IReplayService
{
    private readonly IReplayPacketSink sink;
    private readonly IAprsParser parser;
    private readonly IBeaconSchedulerClock clock;
    private readonly List<ReplayLogEntry> entries = [];
    private ReplaySessionState state = ReplaySessionState.Stopped;
    private int currentIndex;
    private DateTimeOffset? lastReplayTimestampUtc;
    private string? lastError;

    public ReplayService(
        IReplayPacketSink sink,
        ReplaySessionConfiguration? configuration = null,
        IAprsParser? parser = null,
        IBeaconSchedulerClock? clock = null)
    {
        this.sink = sink;
        Configuration = NormalizeConfiguration(configuration ?? ReplaySessionConfiguration.Default);
        this.parser = parser ?? new AprsParser();
        this.clock = clock ?? new SystemBeaconSchedulerClock();
    }

    public ReplaySessionConfiguration Configuration { get; private set; }

    public async Task<IReadOnlyList<ReplayLogEntry>> LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        state = ReplaySessionState.Loading;
        currentIndex = 0;
        lastError = null;
        entries.Clear();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Fault("Replay file path is required.");
        }

        try
        {
            if (!File.Exists(filePath))
            {
                return Fault("Replay file was not found.");
            }

            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var loaded = ParseLines(lines);
            entries.AddRange(ApplyFilters(loaded));
            entries.Sort((left, right) => left.OriginalTimestampUtc.CompareTo(right.OriginalTimestampUtc));
            Configuration = NormalizeConfiguration(Configuration with { SelectedFilePath = filePath });
            state = ReplaySessionState.Ready;
            return entries.ToArray();
        }
        catch (OperationCanceledException)
        {
            state = ReplaySessionState.Stopped;
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            return Fault(ex.Message);
        }
    }

    public IReadOnlyList<ReplayLogEntry> LoadEntries(IEnumerable<RawPacketLogEntry> rawLogEntries)
    {
        entries.Clear();
        currentIndex = 0;
        lastError = null;

        entries.AddRange(ApplyFilters(rawLogEntries.Select(ConvertLogEntry)));
        entries.Sort((left, right) => left.OriginalTimestampUtc.CompareTo(right.OriginalTimestampUtc));
        state = ReplaySessionState.Ready;
        return entries.ToArray();
    }

    public async Task StartReplayAsync(CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
        {
            state = ReplaySessionState.Completed;
            return;
        }

        if (state is ReplaySessionState.Stopped or ReplaySessionState.Faulted)
        {
            currentIndex = 0;
        }

        state = ReplaySessionState.Playing;
        var entriesToPlay = entries.Count - currentIndex;
        for (var i = 0; i < entriesToPlay && state == ReplaySessionState.Playing; i++)
        {
            await PlayNextAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> PlayNextAsync(CancellationToken cancellationToken = default)
    {
        if (state == ReplaySessionState.Paused || state == ReplaySessionState.Faulted || entries.Count == 0)
        {
            return false;
        }

        if (currentIndex >= entries.Count)
        {
            if (!Configuration.LoopReplay)
            {
                state = ReplaySessionState.Completed;
                return false;
            }

            currentIndex = 0;
        }

        state = ReplaySessionState.Playing;
        var entry = entries[currentIndex];
        var replayTimestamp = clock.UtcNow;
        var dispatchEntry = entry with
        {
            ReplayTimestampUtc = replayTimestamp,
            PacketSource = AprsPacketSource.Replay
        };

        await sink.PublishReplayPacketAsync(
            new ReplayPacketDispatch(dispatchEntry, dispatchEntry.RawPacketText, replayTimestamp, AprsPacketSource.Replay),
            cancellationToken).ConfigureAwait(false);

        entries[currentIndex] = dispatchEntry;
        currentIndex++;
        lastReplayTimestampUtc = replayTimestamp;

        if (currentIndex >= entries.Count)
        {
            state = Configuration.LoopReplay ? ReplaySessionState.Ready : ReplaySessionState.Completed;
        }

        return true;
    }

    public void Pause()
    {
        if (state == ReplaySessionState.Playing || state == ReplaySessionState.Ready)
        {
            state = ReplaySessionState.Paused;
        }
    }

    public void Resume()
    {
        if (state == ReplaySessionState.Paused)
        {
            state = ReplaySessionState.Ready;
        }
    }

    public void Stop()
    {
        state = ReplaySessionState.Stopped;
        currentIndex = 0;
    }

    public bool SeekToEntryIndex(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= entries.Count)
        {
            return false;
        }

        currentIndex = entryIndex;
        state = ReplaySessionState.Ready;
        return true;
    }

    public bool SeekToTimestamp(DateTimeOffset timestampUtc)
    {
        var index = entries.FindIndex(entry => entry.OriginalTimestampUtc >= timestampUtc);
        return index >= 0 && SeekToEntryIndex(index);
    }

    public void UpdateConfiguration(ReplaySessionConfiguration configuration)
    {
        Configuration = NormalizeConfiguration(configuration);
    }

    public ReplaySessionStatus GetStatus()
    {
        var currentTimestamp = currentIndex >= 0 && currentIndex < entries.Count
            ? entries[currentIndex].OriginalTimestampUtc
            : entries.LastOrDefault()?.OriginalTimestampUtc;

        return new ReplaySessionStatus(
            state,
            currentIndex,
            entries.Count,
            Configuration.SpeedMultiplier,
            Configuration.LoopReplay,
            Configuration.TransmitDisabled,
            currentTimestamp,
            lastReplayTimestampUtc,
            Configuration.SelectedFilePath,
            lastError);
    }

    public IReadOnlyList<ReplayLogEntry> GetEntries()
    {
        return entries.ToArray();
    }

    private IReadOnlyList<ReplayLogEntry> Fault(string error)
    {
        lastError = error;
        state = ReplaySessionState.Faulted;
        entries.Clear();
        currentIndex = 0;
        return entries.ToArray();
    }

    private IReadOnlyList<ReplayLogEntry> ParseLines(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return [];
        }

        var firstLine = lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
        if (firstLine is not null && LooksLikeCsvHeader(firstLine))
        {
            return ParseCsvLines(lines);
        }

        return lines
            .Select((line, index) => ParseTextLine(line, index))
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();
    }

    private IReadOnlyList<ReplayLogEntry> ParseCsvLines(IReadOnlyList<string> lines)
    {
        var header = SplitCsvLine(lines[0]);
        var lookup = header
            .Select((name, index) => new { Name = name.Trim(), Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        return lines.Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select((line, index) => ParseCsvEntry(line, index, lookup))
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .ToArray();
    }

    private ReplayLogEntry? ParseCsvEntry(string line, int index, IReadOnlyDictionary<string, int> lookup)
    {
        var values = SplitCsvLine(line);
        var rawPacket = GetCsvValue(values, lookup, "RawPacketText") ?? GetCsvValue(values, lookup, "RawPacket") ?? GetCsvValue(values, lookup, "Packet");
        if (string.IsNullOrWhiteSpace(rawPacket))
        {
            return null;
        }

        var timestamp = TryParseTimestamp(GetCsvValue(values, lookup, "TimestampUtc"), out var parsedTimestamp)
            ? parsedTimestamp
            : clock.UtcNow.AddMilliseconds(index);
        var originalSource = TryParseEnum(GetCsvValue(values, lookup, "PacketSource"), AprsPacketSource.Unknown);
        var direction = TryParseEnum(GetCsvValue(values, lookup, "Direction"), RawPacketLogDirection.Received);
        var notes = GetCsvValue(values, lookup, "Notes");

        return CreateReplayEntry(rawPacket.Trim(), timestamp, originalSource, direction, notes);
    }

    private ReplayLogEntry? ParseTextLine(string line, int index)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return null;
        }

        var timestamp = clock.UtcNow.AddMilliseconds(index);
        var rawPacket = trimmed;

        if (TrySplitTimestampedLine(trimmed, out var parsedTimestamp, out var packetText))
        {
            timestamp = parsedTimestamp;
            rawPacket = packetText;
        }

        return CreateReplayEntry(rawPacket, timestamp, AprsPacketSource.Unknown, RawPacketLogDirection.Received, null);
    }

    private ReplayLogEntry CreateReplayEntry(
        string rawPacketText,
        DateTimeOffset originalTimestampUtc,
        AprsPacketSource originalPacketSource,
        RawPacketLogDirection direction,
        string? notes)
    {
        parser.TryParse(rawPacketText, originalTimestampUtc, out var packet, out _);
        packet ??= new AprsParser().Parse(rawPacketText, originalTimestampUtc);

        return new ReplayLogEntry(
            Guid.NewGuid(),
            originalTimestampUtc,
            null,
            packet.RawLine,
            AprsPacketSource.Replay,
            originalPacketSource,
            direction,
            GetPacketType(packet),
            string.IsNullOrWhiteSpace(packet.SourceCallsign) ? null : packet.SourceCallsign,
            string.IsNullOrWhiteSpace(packet.Destination) ? null : packet.Destination,
            packet.Path,
            packet.ValidationErrors,
            [],
            notes);
    }

    private ReplayLogEntry ConvertLogEntry(RawPacketLogEntry entry)
    {
        return new ReplayLogEntry(
            Guid.NewGuid(),
            entry.TimestampUtc,
            null,
            entry.RawPacketText,
            AprsPacketSource.Replay,
            entry.PacketSource,
            entry.Direction,
            entry.ParsedPacketType,
            entry.SourceCallsign,
            entry.Destination,
            entry.Path,
            entry.ValidationErrors,
            entry.ValidationWarnings,
            entry.Notes);
    }

    private IEnumerable<ReplayLogEntry> ApplyFilters(IEnumerable<ReplayLogEntry> source)
    {
        return source.Where(entry =>
            (Configuration.StartFilterUtc is null || entry.OriginalTimestampUtc >= Configuration.StartFilterUtc)
            && (Configuration.EndFilterUtc is null || entry.OriginalTimestampUtc <= Configuration.EndFilterUtc));
    }

    private static ReplaySessionConfiguration NormalizeConfiguration(ReplaySessionConfiguration configuration)
    {
        var speed = configuration.SpeedMultiplier <= 0 ? 1.0 : configuration.SpeedMultiplier;
        return configuration with { SpeedMultiplier = speed };
    }

    private static bool LooksLikeCsvHeader(string line)
    {
        return line.Contains("RawPacketText", StringComparison.OrdinalIgnoreCase)
            || line.Contains("RawPacket", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TrySplitTimestampedLine(string line, out DateTimeOffset timestamp, out string rawPacket)
    {
        var separators = new[] { ',', '\t', ' ' };
        foreach (var separator in separators)
        {
            var index = line.IndexOf(separator);
            if (index <= 0 || index >= line.Length - 1)
            {
                continue;
            }

            var candidate = line[..index].Trim();
            if (TryParseTimestamp(candidate, out timestamp))
            {
                rawPacket = line[(index + 1)..].Trim();
                return true;
            }
        }

        timestamp = default;
        rawPacket = line;
        return false;
    }

    private static bool TryParseTimestamp(string? value, out DateTimeOffset timestamp)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out timestamp);
    }

    private static string? GetCsvValue(IReadOnlyList<string> values, IReadOnlyDictionary<string, int> lookup, string fieldName)
    {
        return lookup.TryGetValue(fieldName, out var index) && index >= 0 && index < values.Count
            ? values[index]
            : null;
    }

    private static TEnum TryParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static IReadOnlyList<string> SplitCsvLine(string line)
    {
        var values = new List<string>();
        var current = new List<char>();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];
            if (character == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Add('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (character == ',' && !inQuotes)
            {
                values.Add(new string(current.ToArray()));
                current.Clear();
                continue;
            }

            current.Add(character);
        }

        values.Add(new string(current.ToArray()));
        return values;
    }

    private static string GetPacketType(AprsPacket packet)
    {
        var typeName = packet.GetType().Name;
        return typeName.EndsWith("AprsPacket", StringComparison.Ordinal)
            ? typeName[..^"AprsPacket".Length]
            : typeName;
    }
}
