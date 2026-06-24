using Xunit;

namespace Aprs.Tests;

public sealed class FinalReleaseValidationTests
{
    private static readonly string[] RequiredChecklistHeadings =
    [
        "## Source And Build Validation",
        "## Unit Test Validation",
        "## Desktop Launch Validation",
        "## First-Run Setup Validation",
        "## UI Layout Validation",
        "## In-App Help Validation",
        "## Documentation Validation",
        "## Package Script Validation",
        "## Portable Package Contents Validation",
        "## Safety Defaults Validation",
        "## API WebSocket File Hook Plugin Safety Validation",
        "## Replay Simulation Training Safety Validation",
        "## Platform Notes",
        "## Known Issues",
        "## Release Approval Checklist"
    ];

    [Fact]
    public void FinalReleaseChecklistContainsRequiredSections()
    {
        var checklist = Read("docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md");

        Assert.Contains("APRS Command Final Release Validation Checklist", checklist, StringComparison.Ordinal);
        foreach (var heading in RequiredChecklistHeadings)
        {
            Assert.Contains(heading, checklist, StringComparison.Ordinal);
        }

        Assert.Contains("dotnet --version", checklist, StringComparison.Ordinal);
        Assert.Contains("dotnet restore", checklist, StringComparison.Ordinal);
        Assert.Contains("dotnet build", checklist, StringComparison.Ordinal);
        Assert.Contains("dotnet test", checklist, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project src/Aprs.Desktop/Aprs.Desktop.csproj", checklist, StringComparison.Ordinal);
        Assert.Contains("test -f CrossPlatformAprs.sln", checklist, StringComparison.Ordinal);
        Assert.Contains("Actual solution path", checklist, StringComparison.Ordinal);
        Assert.Contains("Actual desktop project path", checklist, StringComparison.Ordinal);
        Assert.Contains("No test requires live APRS-IS", checklist, StringComparison.Ordinal);
    }

    [Fact]
    public void FinalReleaseChecklistCoversSafetyDefaultsAndCredentialReview()
    {
        var checklist = Read("docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md");

        foreach (var phrase in new[]
        {
            "APRS-IS transmit disabled",
            "RF transmit disabled",
            "iGate disabled",
            "Digipeater disabled",
            "Beaconing disabled",
            "Weather beaconing disabled",
            "Object transmit disabled",
            "Message transmit disabled",
            "REST API disabled",
            "WebSocket disabled",
            "File hooks disabled",
            "Plugin loading disabled",
            "Replay cannot transmit",
            "Simulation cannot transmit",
            "Training mode cannot transmit",
            "Imported transmit requests are blocked by default",
            "Plugin transmit requests are blocked by default",
            "API transmit requests are blocked by default",
            "No real APRS-IS passcodes",
            "No API tokens",
            "No signing secrets"
        })
        {
            Assert.Contains(phrase, checklist, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FinalReleaseChecklistCoversPackageAndPlatformSmokeValidation()
    {
        var checklist = Read("docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md");

        foreach (var phrase in new[]
        {
            "APRS-Command-win-x64.zip",
            "APRS-Command-osx-arm64.tar.gz",
            "APRS-Command-osx-x64.tar.gz",
            "APRS-Command-linux-x64.tar.gz",
            "APRS-Command-linux-arm64.tar.gz",
            "artifacts/publish/osx-arm64/",
            "artifacts/packages/APRS-Command-osx-arm64-test.tar.gz",
            "artifacts/checksums/APRS-Command-osx-arm64-test.tar.gz.sha256",
            "/tmp/aprs-command-osx-arm64-test",
            "test -x ./Aprs.Desktop",
            "open \"./APRS Command.command\"",
            "-test",
            "SHA256 checksum",
            "Windows:",
            "macOS:",
            "Linux:",
            "Raspberry Pi / Linux ARM64:"
        })
        {
            Assert.Contains(phrase, checklist, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReleaseValidationScriptRunsExpectedSafeChecks()
    {
        var scriptPath = Path.Combine(RepositoryRoot, "scripts", "validate-release.sh");

        Assert.True(File.Exists(scriptPath), scriptPath);

        var script = File.ReadAllText(scriptPath);

        Assert.Contains("dotnet --version", script, StringComparison.Ordinal);
        Assert.Contains("dotnet restore", script, StringComparison.Ordinal);
        Assert.Contains("dotnet build", script, StringComparison.Ordinal);
        Assert.Contains("dotnet test", script, StringComparison.Ordinal);
        Assert.Contains("CrossPlatformAprs.sln", script, StringComparison.Ordinal);
        Assert.Contains("src/Aprs.Desktop/Aprs.Desktop.csproj", script, StringComparison.Ordinal);
        Assert.Contains("Could not locate $description", script, StringComparison.Ordinal);
        Assert.Contains("docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md", script, StringComparison.Ordinal);
        Assert.Contains("scripts/package-runtime.sh", script, StringComparison.Ordinal);
        Assert.Contains("CopyToPublishDirectory", script, StringComparison.Ordinal);
        Assert.Contains("BEGIN PRIVATE KEY", script, StringComparison.Ordinal);
        Assert.Contains("--glob '!validate-release.sh'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet run --project src/Aprs.Desktop", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleasePlanningDocsLinkToFinalChecklist()
    {
        foreach (var relativePath in new[]
        {
            "README.md",
            "docs/BUILD_AND_PUBLISH.md",
            "docs/PACKAGING_PREPARATION.md",
            "docs/INSTALLER_AND_PACKAGE_PLAN.md"
        })
        {
            Assert.Contains("docs/FINAL_RELEASE_VALIDATION_CHECKLIST.md", Read(relativePath), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReleaseValidationReportRecordsCurrentPlatformTestPackage()
    {
        var report = Read("docs/RELEASE_VALIDATION_REPORT.md");

        Assert.Contains("APRS Command Release Validation Report", report, StringComparison.Ordinal);
        Assert.Contains(".NET SDK version", report, StringComparison.Ordinal);
        Assert.Contains("Actual solution path", report, StringComparison.Ordinal);
        Assert.Contains("Actual desktop project path", report, StringComparison.Ordinal);
        Assert.Contains("macOS", report, StringComparison.Ordinal);
        Assert.Contains("osx-arm64", report, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-osx-arm64-test.tar.gz", report, StringComparison.Ordinal);
        Assert.Contains("artifacts/publish/osx-arm64/", report, StringComparison.Ordinal);
        Assert.Contains("artifacts/checksums/APRS-Command-osx-arm64-test.tar.gz.sha256", report, StringComparison.Ordinal);
        Assert.Contains("/tmp/aprs-command-osx-arm64-test", report, StringComparison.Ordinal);
        Assert.Contains("tar -xzf artifacts/packages/APRS-Command-osx-arm64-test.tar.gz", report, StringComparison.Ordinal);
        Assert.Contains("./Aprs.Desktop 2>&1 | tee launch-error.txt", report, StringComparison.Ordinal);
        Assert.Contains("SHA256", report, StringComparison.Ordinal);
        Assert.Contains("No live transmit occurred", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("APRS Command.command", report, StringComparison.Ordinal);
        Assert.Contains("manual visual", report, StringComparison.OrdinalIgnoreCase);
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
