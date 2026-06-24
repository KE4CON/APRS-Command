using Aprs.Desktop;
using Xunit;

namespace Aprs.Tests;

public sealed class StartupDiagnosticsTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "APRSCommandStartupDiagnosticsTests", Guid.NewGuid().ToString("N"));

    public StartupDiagnosticsTests()
    {
        Directory.CreateDirectory(root);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StartupLogDirectoryUsesAprsCommandApplicationDataFolder()
    {
        var path = StartupDiagnostics.ResolveStartupLogDirectory();

        Assert.Contains("APRS Command", path, StringComparison.Ordinal);
        Assert.EndsWith("logs", path, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\APRS Command", path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FatalStartupErrorIsWrittenToAReadableLog()
    {
        var logPath = StartupDiagnostics.WriteFatalStartupError(new InvalidOperationException("startup test failure"), root);

        Assert.True(File.Exists(logPath), logPath);
        var content = File.ReadAllText(logPath);
        Assert.Contains("APRS Command startup failure", content, StringComparison.Ordinal);
        Assert.Contains("startup test failure", content, StringComparison.Ordinal);
        Assert.Contains("AppBaseDirectory", content, StringComparison.Ordinal);
    }
}
