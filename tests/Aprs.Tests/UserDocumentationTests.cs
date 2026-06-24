using Xunit;

namespace Aprs.Tests;

public sealed class UserDocumentationTests
{
    private static readonly string[] RequiredUserDocs =
    [
        "docs/USER_MANUAL.md",
        "docs/QUICK_START.md",
        "docs/INSTALLATION_GUIDE.md",
        "docs/FIRST_RUN_SETUP.md",
        "docs/SAFETY_AND_TRANSMIT_GUIDE.md",
        "docs/APRS_IS_SETUP_GUIDE.md",
        "docs/RF_TNC_SETUP_GUIDE.md",
        "docs/MAP_AND_OFFLINE_MAPS_GUIDE.md",
        "docs/MESSAGES_GUIDE.md",
        "docs/OBJECTS_GUIDE.md",
        "docs/WEATHER_GUIDE.md",
        "docs/ALERTS_AND_GEOFENCES_GUIDE.md",
        "docs/REPLAY_SIMULATION_TRAINING_GUIDE.md",
        "docs/RF_DIAGNOSTICS_GUIDE.md",
        "docs/LOGS_EVENTS_AND_EXPORTS_GUIDE.md",
        "docs/TROUBLESHOOTING.md",
        "docs/GLOSSARY.md"
    ];

    [Fact]
    public void RequiredUserDocumentationFilesExist()
    {
        foreach (var relativePath in RequiredUserDocs)
        {
            Assert.True(File.Exists(Path.Combine(RepositoryRoot, relativePath)), relativePath);
        }
    }

    [Fact]
    public void ReadmeLinksToCoreUserDocumentation()
    {
        var readme = Read("README.md");

        Assert.Contains("Quick Start", readme, StringComparison.Ordinal);
        Assert.Contains("Installation Guide", readme, StringComparison.Ordinal);
        Assert.Contains("User Manual", readme, StringComparison.Ordinal);
        Assert.Contains("First-Run Setup", readme, StringComparison.Ordinal);
        Assert.Contains("Safety and Transmit Guide", readme, StringComparison.Ordinal);
        Assert.Contains("APRS-IS Setup Guide", readme, StringComparison.Ordinal);
        Assert.Contains("RF/TNC Setup Guide", readme, StringComparison.Ordinal);
        Assert.Contains("Map and Offline Maps Guide", readme, StringComparison.Ordinal);
        Assert.Contains("Troubleshooting", readme, StringComparison.Ordinal);
        Assert.Contains("Glossary", readme, StringComparison.Ordinal);
        Assert.Contains("Developer Guide", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/QUICK_START.md)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/GLOSSARY.md)", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("/Users/", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void UserDocumentationUsesAprsCommandNameAndCurrentLayout()
    {
        var combined = string.Join(Environment.NewLine, RequiredUserDocs.Select(Read));

        Assert.Contains("APRS Command", combined, StringComparison.Ordinal);
        Assert.Contains("map-first", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("station list", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("packet monitor", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CrossPlatform APRS", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("APRS Viewer", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("APRS View", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("top navigation", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserDocumentationDescribesTransmitDisabledByDefault()
    {
        var safety = Read("docs/SAFETY_AND_TRANSMIT_GUIDE.md");
        var manual = Read("docs/USER_MANUAL.md");
        var firstRun = Read("docs/FIRST_RUN_SETUP.md");

        Assert.Contains("does not transmit by default", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("APRS-IS transmit disabled", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF transmit disabled", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("iGate disabled", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("digipeater disabled", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("beaconing disabled", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("weather beaconing disabled", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("object transmit", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("message transmit", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("REST API disabled", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WebSocket disabled", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file hooks disabled", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plugin loading disabled", safety, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("disabled by default", manual, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("transmit disabled", firstRun, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UserDocumentationUsesSafeExampleValues()
    {
        var combined = string.Join(Environment.NewLine, RequiredUserDocs.Select(Read));

        Assert.Contains("N0CALL", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("12345", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("api_key", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passcode=", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret=", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token=", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("transmitEnabled\": true", combined, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrossPlatformAprs.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }

    private static string Read(string relativePath)
    {
        return File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));
    }
}
