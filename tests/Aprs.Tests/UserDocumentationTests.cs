using Xunit;

namespace Aprs.Tests;

public sealed class UserDocumentationTests
{
    private static readonly string[] RequiredUserDocs =
    [
        "docs/help/USER_MANUAL.md",
        "docs/help/QUICK_START.md",
        "docs/help/INSTALLATION_GUIDE.md",
        "docs/help/FIRST_RUN_SETUP.md",
        "docs/help/SAFETY_AND_TRANSMIT_GUIDE.md",
        "docs/help/APRS_IS_SETUP_GUIDE.md",
        "docs/help/RF_TNC_SETUP_GUIDE.md",
        "docs/help/MAP_AND_OFFLINE_MAPS_GUIDE.md",
        "docs/help/MESSAGES_GUIDE.md",
        "docs/help/OBJECTS_GUIDE.md",
        "docs/help/WEATHER_GUIDE.md",
        "docs/help/ALERTS_AND_GEOFENCES_GUIDE.md",
        "docs/help/REPLAY_SIMULATION_TRAINING_GUIDE.md",
        "docs/help/RF_DIAGNOSTICS_GUIDE.md",
        "docs/help/LOGS_EVENTS_AND_EXPORTS_GUIDE.md",
        "docs/help/TROUBLESHOOTING.md",
        "docs/help/GLOSSARY.md"
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

        // Consolidated Documentation section: headline deliverables live in docs/published/,
        // the in-app Help topics under docs/help/, and contributor material in its own folders.
        Assert.Contains("User Manual", readme, StringComparison.Ordinal);
        Assert.Contains("Programming Guide", readme, StringComparison.Ordinal);
        Assert.Contains("Quick Start", readme, StringComparison.Ordinal);
        Assert.Contains("Developer Guide", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/published/USER_MANUAL.docx)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/published/PROGRAMMING_GUIDE.md)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/help/)", readme, StringComparison.Ordinal);
        Assert.Contains("(docs/architecture/DEVELOPER_GUIDE.md)", readme, StringComparison.Ordinal);
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
        var safety = Read("docs/help/SAFETY_AND_TRANSMIT_GUIDE.md");
        var manual = Read("docs/help/USER_MANUAL.md");
        var firstRun = Read("docs/help/FIRST_RUN_SETUP.md");

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
