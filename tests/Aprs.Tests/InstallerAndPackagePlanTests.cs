using Xunit;

namespace Aprs.Tests;

public sealed class InstallerAndPackagePlanTests
{
    private static readonly string[] PackagingFiles =
    [
        "docs/INSTALLER_AND_PACKAGE_PLAN.md",
        "packaging/templates/aprs-command.desktop.template",
        "packaging/templates/macos-info-plist-notes.md",
        "packaging/templates/windows-shortcuts.md",
        "packaging/templates/release-notes-template.md"
    ];

    [Fact]
    public void InstallerAndPackagePlanDocumentsSupportedPlatforms()
    {
        var plan = Read("docs/INSTALLER_AND_PACKAGE_PLAN.md");

        Assert.Contains("Windows x64", plan, StringComparison.Ordinal);
        Assert.Contains("macOS Apple Silicon", plan, StringComparison.Ordinal);
        Assert.Contains("macOS Intel", plan, StringComparison.Ordinal);
        Assert.Contains("Linux x64", plan, StringComparison.Ordinal);
        Assert.Contains("Linux ARM64", plan, StringComparison.Ordinal);
        Assert.Contains("Raspberry Pi 5", plan, StringComparison.Ordinal);
        Assert.Contains("win-x64", plan, StringComparison.Ordinal);
        Assert.Contains("osx-arm64", plan, StringComparison.Ordinal);
        Assert.Contains("osx-x64", plan, StringComparison.Ordinal);
        Assert.Contains("linux-x64", plan, StringComparison.Ordinal);
        Assert.Contains("linux-arm64", plan, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallerAndPackagePlanDocumentsReleaseFoldersAndSafetyDefaults()
    {
        var plan = Read("docs/INSTALLER_AND_PACKAGE_PLAN.md");

        Assert.Contains("artifacts/", plan, StringComparison.Ordinal);
        Assert.Contains("publish/", plan, StringComparison.Ordinal);
        Assert.Contains("packages/", plan, StringComparison.Ordinal);
        Assert.Contains("checksums/", plan, StringComparison.Ordinal);
        Assert.Contains("release-notes/", plan, StringComparison.Ordinal);
        Assert.Contains("SHA256", plan, StringComparison.Ordinal);
        Assert.Contains("APRS-IS transmit disabled", plan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RF transmit disabled", plan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("REST API disabled", plan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WebSocket disabled", plan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file hooks disabled", plan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plugin loading disabled", plan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Replay, simulation, and training cannot transmit", plan, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackagingTemplatesUseAprsCommandMetadata()
    {
        var desktopTemplate = Read("packaging/templates/aprs-command.desktop.template");
        var macosNotes = Read("packaging/templates/macos-info-plist-notes.md");
        var windowsNotes = Read("packaging/templates/windows-shortcuts.md");
        var releaseNotes = Read("packaging/templates/release-notes-template.md");

        Assert.Contains("Name=APRS Command", desktopTemplate, StringComparison.Ordinal);
        Assert.Contains("CFBundleDisplayName`: `APRS Command`", macosNotes, StringComparison.Ordinal);
        Assert.Contains("Desktop shortcut: `APRS Command`", windowsNotes, StringComparison.Ordinal);
        Assert.Contains("APRS Command Release Notes Template", releaseNotes, StringComparison.Ordinal);
    }

    [Fact]
    public void PackagingDocsAreLinkedFromMainDocs()
    {
        Assert.Contains("docs/INSTALLER_AND_PACKAGE_PLAN.md", Read("README.md"), StringComparison.Ordinal);
        Assert.Contains("docs/INSTALLER_AND_PACKAGE_PLAN.md", Read("docs/PACKAGING_PREPARATION.md"), StringComparison.Ordinal);
        Assert.Contains("docs/INSTALLER_AND_PACKAGE_PLAN.md", Read("docs/BUILD_AND_PUBLISH.md"), StringComparison.Ordinal);
        Assert.Contains("docs/INSTALLER_AND_PACKAGE_PLAN.md", Read("docs/INSTALLATION_GUIDE.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void PackagingFilesDoNotContainSecretsOrEnableTransmit()
    {
        foreach (var file in PackagingFiles)
        {
            var content = Read(file);

            Assert.DoesNotContain("12345", content, StringComparison.Ordinal);
            Assert.DoesNotContain("api_key", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password=", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("passcode=", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret=", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token=", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("BEGIN PRIVATE KEY", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TransmitEnabled=true", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RF transmit enabled", content, StringComparison.OrdinalIgnoreCase);
        }
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
