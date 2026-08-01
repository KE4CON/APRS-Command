using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class LogServiceTests
{
    [Fact]
    public void Log_RecordsEntry_AndRaisesEvent()
    {
        ILogService log = new LogService();
        LogEntry? raised = null;
        log.EntryLogged += (_, e) => raised = e;

        log.Warning("Net", "Connection flaky", new InvalidOperationException("boom"));

        var recent = log.GetRecent();
        var entry = Assert.Single(recent);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Net", entry.Category);
        Assert.Equal("Connection flaky", entry.Message);
        Assert.NotNull(entry.ExceptionDetail);
        Assert.Contains("boom", entry.ExceptionDetail);
        Assert.Equal(entry, raised);
    }

    [Fact]
    public void GetRecent_IsBoundedToCapacity_KeepingNewest()
    {
        ILogService log = new LogService(capacity: 3);

        for (var i = 0; i < 10; i++)
        {
            log.Information("Cat", $"msg{i}");
        }

        var recent = log.GetRecent();
        Assert.Equal(3, recent.Count);
        Assert.Equal("msg7", recent[0].Message);
        Assert.Equal("msg9", recent[^1].Message);
    }

    [Fact]
    public void Log_IsSafeFromMultipleThreads()
    {
        ILogService log = new LogService(capacity: 1000);

        Parallel.For(0, 500, i => log.Information("Cat", $"msg{i}"));

        Assert.Equal(500, log.GetRecent().Count);
    }
}
