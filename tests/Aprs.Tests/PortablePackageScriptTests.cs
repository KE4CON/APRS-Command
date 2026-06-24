using Xunit;

namespace Aprs.Tests;

public sealed class PortablePackageScriptTests
{
    private static readonly string[] RuntimeIdentifiers =
    [
        "win-x64",
        "osx-arm64",
        "osx-x64",
        "linux-x64",
        "linux-arm64"
    ];

    [Fact]
    public void PortablePackageScriptsExistForSupportedRuntimeIdentifiers()
    {
        foreach (var runtimeIdentifier in RuntimeIdentifiers)
        {
            var scriptPath = Path.Combine(RepositoryRoot, "scripts", $"package-{runtimeIdentifier}.sh");

            Assert.True(File.Exists(scriptPath), scriptPath);
            Assert.Contains($"package-runtime.sh\" {runtimeIdentifier}", File.ReadAllText(scriptPath), StringComparison.Ordinal);
        }

        Assert.True(File.Exists(Path.Combine(RepositoryRoot, "scripts", "package-all.sh")));
        Assert.True(File.Exists(Path.Combine(RepositoryRoot, "scripts", "package-win-x64.ps1")));
    }

    [Fact]
    public void SharedPackageScriptStagesDocsAndCreatesChecksums()
    {
        var script = Read("scripts/package-runtime.sh");

        Assert.Contains("publish-runtime.sh", script, StringComparison.Ordinal);
        Assert.Contains("CrossPlatformAprs.sln", script, StringComparison.Ordinal);
        Assert.Contains("src/Aprs.Desktop/Aprs.Desktop.csproj", script, StringComparison.Ordinal);
        Assert.Contains("Could not locate $description", script, StringComparison.Ordinal);
        Assert.Contains("Repository root: $REPO_ROOT", script, StringComparison.Ordinal);
        Assert.Contains("Desktop project: $PROJECT", script, StringComparison.Ordinal);
        Assert.Contains("artifacts/packages", script, StringComparison.Ordinal);
        Assert.Contains("artifacts/checksums", script, StringComparison.Ordinal);
        Assert.Contains("artifacts/release-notes", script, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-$RID", script, StringComparison.Ordinal);
        Assert.Contains("TEST_PACKAGE_PATH", script, StringComparison.Ordinal);
        Assert.Contains("-test.zip", script, StringComparison.Ordinal);
        Assert.Contains("-test.tar.gz", script, StringComparison.Ordinal);
        Assert.Contains("write_checksum", script, StringComparison.Ordinal);
        Assert.Contains("QUICK_START.md", script, StringComparison.Ordinal);
        Assert.Contains("INSTALLATION_GUIDE.md", script, StringComparison.Ordinal);
        Assert.Contains("SAFETY_AND_TRANSMIT_GUIDE.md", script, StringComparison.Ordinal);
        Assert.Contains("TROUBLESHOOTING.md", script, StringComparison.Ordinal);
        Assert.Contains("VERSION.txt", script, StringComparison.Ordinal);
        Assert.Contains("APRS Command.command", script, StringComparison.Ordinal);
        Assert.Contains("chmod +x \"$STAGING_DIR/Aprs.Desktop\"", script, StringComparison.Ordinal);
        Assert.Contains("exec ./Aprs.Desktop", script, StringComparison.Ordinal);
        Assert.Contains("shasum -a 256", script, StringComparison.Ordinal);
        Assert.Contains("sha256sum", script, StringComparison.Ordinal);
        Assert.Contains("zip -qr", script, StringComparison.Ordinal);
        Assert.Contains("tar -czf", script, StringComparison.Ordinal);
    }

    [Fact]
    public void MacAppleSiliconPackageScriptCreatesTestPackageAndChecksum()
    {
        var script = Read("scripts/package-osx-arm64.sh");

        Assert.Contains("package-runtime.sh\" osx-arm64", script, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-osx-arm64-test.tar.gz", script, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-osx-arm64-test.tar.gz.sha256", script, StringComparison.Ordinal);
        Assert.Contains("shasum -a 256", script, StringComparison.Ordinal);
        Assert.Contains("sha256sum", script, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildAndPublishDocsListPortablePackageOutputs()
    {
        var document = Read("docs/BUILD_AND_PUBLISH.md");

        Assert.Contains("./scripts/package-win-x64.sh", document, StringComparison.Ordinal);
        Assert.Contains("./scripts/package-linux-arm64.sh", document, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-win-x64.zip", document, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-win-x64-test.zip", document, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-osx-arm64.tar.gz", document, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-osx-arm64-test.tar.gz", document, StringComparison.Ordinal);
        Assert.Contains("APRS Command.command", document, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-osx-x64.tar.gz", document, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-osx-x64-test.tar.gz", document, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-linux-x64.tar.gz", document, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-linux-x64-test.tar.gz", document, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-linux-arm64.tar.gz", document, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-linux-arm64-test.tar.gz", document, StringComparison.Ordinal);
        Assert.Contains("artifacts/checksums/<package-file>.sha256", document, StringComparison.Ordinal);
        Assert.Contains("ls -la artifacts/packages/APRS-Command-osx-arm64-test.tar.gz", document, StringComparison.Ordinal);
        Assert.Contains("ls -la artifacts/checksums/APRS-Command-osx-arm64-test.tar.gz.sha256", document, StringComparison.Ordinal);
        Assert.Contains("mkdir -p /tmp/aprs-command-osx-arm64-test", document, StringComparison.Ordinal);
        Assert.Contains("cd /tmp/aprs-command-osx-arm64-test/APRS-Command-osx-arm64", document, StringComparison.Ordinal);
        Assert.Contains("chmod +x ./Aprs.Desktop", document, StringComparison.Ordinal);
        Assert.Contains("open \"./APRS Command.command\"", document, StringComparison.Ordinal);
        Assert.Contains("./Aprs.Desktop 2>&1 | tee launch-error.txt", document, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseNotesTemplateDocumentsSafeDefaultsAndPackages()
    {
        var template = Read("docs/RELEASE_NOTES_TEMPLATE.md");

        Assert.Contains("APRS Command Release Notes Template", template, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-win-x64.zip", template, StringComparison.Ordinal);
        Assert.Contains("APRS-Command-linux-arm64.tar.gz", template, StringComparison.Ordinal);
        Assert.Contains("SHA256", template, StringComparison.Ordinal);
        Assert.Contains("APRS Command does not transmit by default", template, StringComparison.Ordinal);
        Assert.Contains("Replay, simulation, and training cannot transmit", template, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageScriptsDoNotContainSecretsOrEnableTransmit()
    {
        foreach (var relativePath in Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "scripts"), "package-*")
            .Select(path => Path.GetRelativePath(RepositoryRoot, path)))
        {
            var content = Read(relativePath);

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
