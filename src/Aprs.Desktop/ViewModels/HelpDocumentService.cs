using System.Text;
using System.Text.RegularExpressions;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Loads APRS Command help documents from the published app folder or the repository docs folder during development.
/// </summary>
public sealed class HelpDocumentService
{
    public const string MissingDocumentMessage = "This help topic is not available in this build.";

    private static readonly HelpTopicDefinition[] TopicDefinitions =
    [
        new("user-manual", "User Manual", "USER_MANUAL.md"),
        new("quick-start", "Quick Start", "QUICK_START.md"),
        new("installation-guide", "Installation Guide", "INSTALLATION_GUIDE.md"),
        new("first-run-setup", "First-Run Setup", "FIRST_RUN_SETUP.md"),
        new("safety-and-transmit-guide", "Safety and Transmit Guide", "SAFETY_AND_TRANSMIT_GUIDE.md"),
        new("aprs-is-setup-guide", "APRS-IS Setup Guide", "APRS_IS_SETUP_GUIDE.md"),
        new("rf-tnc-setup-guide", "RF/TNC Setup Guide", "RF_TNC_SETUP_GUIDE.md"),
        new("map-and-offline-maps-guide", "Map and Offline Maps Guide", "MAP_AND_OFFLINE_MAPS_GUIDE.md"),
        new("messages-guide", "Messages Guide", "MESSAGES_GUIDE.md"),
        new("objects-guide", "Objects Guide", "OBJECTS_GUIDE.md"),
        new("weather-guide", "Weather Guide", "WEATHER_GUIDE.md"),
        new("alerts-and-geofences-guide", "Alerts and Geofences Guide", "ALERTS_AND_GEOFENCES_GUIDE.md"),
        new("replay-simulation-training-guide", "Replay, Simulation, and Training Guide", "REPLAY_SIMULATION_TRAINING_GUIDE.md"),
        new("rf-diagnostics-guide", "RF Diagnostics Guide", "RF_DIAGNOSTICS_GUIDE.md"),
        new("logs-events-exports-guide", "Logs, Events, and Exports Guide", "LOGS_EVENTS_AND_EXPORTS_GUIDE.md"),
        new("troubleshooting", "Troubleshooting", "TROUBLESHOOTING.md"),
        new("glossary", "Glossary", "GLOSSARY.md"),
        new("about", "About APRS Command", null)
    ];

    private readonly string docsFolderPath;

    public HelpDocumentService()
        : this(ResolveDefaultDocsFolder())
    {
    }

    public HelpDocumentService(string docsFolderPath)
    {
        this.docsFolderPath = docsFolderPath;
    }

    public string DocsFolderPath => docsFolderPath;

    public IReadOnlyList<HelpTopic> LoadTopics()
    {
        return TopicDefinitions.Select(LoadTopic).ToArray();
    }

    private HelpTopic LoadTopic(HelpTopicDefinition definition)
    {
        if (definition.RelativePath is null)
        {
            return new HelpTopic(definition.Id, definition.Title, null, CreateAboutContent(), true);
        }

        var path = Path.Combine(docsFolderPath, definition.RelativePath);
        if (!File.Exists(path))
        {
            return new HelpTopic(definition.Id, definition.Title, definition.RelativePath, MissingDocumentMessage, false);
        }

        return new HelpTopic(definition.Id, definition.Title, definition.RelativePath, ConvertMarkdownToReadableText(File.ReadAllText(path)), true);
    }

    private string CreateAboutContent()
    {
        return $"""
               # APRS Command

               APRS Command is a cross-platform amateur-radio APRS desktop client.

               Version: development build

               APRS Command is safe by default. Transmit, APRS-IS transmit, RF transmit, beaconing, iGate, digipeater, object transmit, message transmit, and weather beaconing remain disabled until explicitly configured and allowed by centralized safety checks.

               Documentation location:
               {docsFolderPath}

               Extension hooks summary:
               APRS Command includes foundations for public data contracts, local REST API, WebSocket event streams, file import/export hooks, and plugin/driver support. Extension entry points cannot bypass transmit safety.

               License:
               License placeholder. A final project license has not been selected in this help topic.
               """;
    }

    private static string ConvertMarkdownToReadableText(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var builder = new StringBuilder();
        var insideCodeFence = false;

        foreach (var rawLine in normalized.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                insideCodeFence = !insideCodeFence;
                continue;
            }

            if (!insideCodeFence)
            {
                var trimmedStart = line.TrimStart();
                if (trimmedStart.StartsWith('#'))
                {
                    line = trimmedStart.TrimStart('#').TrimStart();
                }
                else if (trimmedStart.StartsWith("- ", StringComparison.Ordinal))
                {
                    var indent = line[..(line.Length - trimmedStart.Length)];
                    line = $"{indent}- {trimmedStart[2..]}";
                }

                line = Regex.Replace(line, @"\[(?<text>[^\]]+)\]\([^\)]+\)", "${text}");
                line = line.Replace("`", string.Empty, StringComparison.Ordinal);
            }

            builder.AppendLine(line);
        }

        return builder.ToString().Trim();
    }

    private static string ResolveDefaultDocsFolder()
    {
        var publishedDocs = Path.Combine(AppContext.BaseDirectory, "docs");
        if (Directory.Exists(publishedDocs))
        {
            return publishedDocs;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "docs");
            if (File.Exists(Path.Combine(candidate, "USER_MANUAL.md")))
            {
                return candidate;
            }

            current = current.Parent;
        }

        var workingDirectoryDocs = Path.Combine(Environment.CurrentDirectory, "docs");
        return workingDirectoryDocs;
    }

    private sealed record HelpTopicDefinition(string Id, string Title, string? RelativePath);
}
