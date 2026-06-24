using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class ReplayServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultConfigurationIsDisabledAndNeverTransmits()
    {
        var service = CreateService();

        var status = service.GetStatus();

        Assert.False(service.Configuration.ReplayEnabled);
        Assert.True(service.Configuration.TransmitDisabled);
        Assert.Equal(ReplaySessionState.Stopped, status.State);
    }

    [Fact]
    public async Task SimplePacketPerLineFileLoadsReplayEntries()
    {
        var path = CreateTempFile(
        [
            "N0CALL>APRS,TCPIP*:!3903.50N/08430.50W-Test replay",
            "W1AW>APRS:>Status replay"
        ]);
        var service = CreateService();

        try
        {
            var entries = await service.LoadFromFileAsync(path);

            Assert.Equal(2, entries.Count);
            Assert.All(entries, entry => Assert.Equal(AprsPacketSource.Replay, entry.PacketSource));
            Assert.Equal(AprsPacketSource.Unknown, entries[0].OriginalPacketSource);
            Assert.Equal("N0CALL", entries[0].SourceCallsign);
            Assert.Equal(ReplaySessionState.Ready, service.GetStatus().State);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RawPacketLogCsvFileLoadsReplayEntries()
    {
        var path = CreateTempFile(
        [
            "TimestampUtc,RawPacketText,PacketSource,Direction,Notes",
            "2026-06-12T12:00:00Z,\"WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132\",AprsIs,Received,weather"
        ]);
        var service = CreateService();

        try
        {
            var entries = await service.LoadFromFileAsync(path);

            var entry = Assert.Single(entries);
            Assert.Equal(AprsPacketSource.Replay, entry.PacketSource);
            Assert.Equal(AprsPacketSource.AprsIs, entry.OriginalPacketSource);
            Assert.Equal("Weather", entry.ParsedPacketType);
            Assert.Equal("weather", entry.Notes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task TimestampedTextFilePreservesOriginalTimestamp()
    {
        var path = CreateTempFile(["2026-06-12T12:34:00Z N0CALL>APRS:>Timestamped"]);
        var service = CreateService();

        try
        {
            var entries = await service.LoadFromFileAsync(path);

            var entry = Assert.Single(entries);
            Assert.Equal(new DateTimeOffset(2026, 6, 12, 12, 34, 0, TimeSpan.Zero), entry.OriginalTimestampUtc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task InvalidFilePathFaultsSafely()
    {
        var service = CreateService();

        var entries = await service.LoadFromFileAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".log"));

        Assert.Empty(entries);
        var status = service.GetStatus();
        Assert.Equal(ReplaySessionState.Faulted, status.State);
        Assert.Contains("not found", status.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MalformedPacketsLoadWithValidationErrors()
    {
        var path = CreateTempFile(["BADPACKETWITHOUTSEPARATOR"]);
        var service = CreateService();

        try
        {
            var entries = await service.LoadFromFileAsync(path);

            var entry = Assert.Single(entries);
            Assert.NotEmpty(entry.ValidationErrors);
            Assert.Equal("Raw", entry.ParsedPacketType);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task PlayNextPublishesReplaySource()
    {
        var sink = new FakeReplaySink();
        var service = CreateService(sink);
        service.LoadEntries([CreateRawLogEntry("N0CALL>APRS:>Replay me", AprsPacketSource.Rf)]);

        var played = await service.PlayNextAsync();

        Assert.True(played);
        var dispatch = Assert.Single(sink.Dispatches);
        Assert.Equal(AprsPacketSource.Replay, dispatch.PacketSource);
        Assert.Equal(AprsPacketSource.Replay, dispatch.Entry.PacketSource);
        Assert.Equal(AprsPacketSource.Rf, dispatch.Entry.OriginalPacketSource);
    }

    [Fact]
    public void PauseResumeAndStopUpdateState()
    {
        var service = CreateService();
        service.LoadEntries([CreateRawLogEntry("N0CALL>APRS:>Replay", AprsPacketSource.AprsIs)]);

        service.Pause();
        Assert.Equal(ReplaySessionState.Paused, service.GetStatus().State);

        service.Resume();
        Assert.Equal(ReplaySessionState.Ready, service.GetStatus().State);

        service.Stop();
        var status = service.GetStatus();
        Assert.Equal(ReplaySessionState.Stopped, status.State);
        Assert.Equal(0, status.CurrentIndex);
    }

    [Fact]
    public void SpeedSettingIsExposedInStatus()
    {
        var service = CreateService(configuration: ReplaySessionConfiguration.Default with { SpeedMultiplier = 4 });

        Assert.Equal(4, service.GetStatus().SpeedMultiplier);
    }

    [Fact]
    public async Task LoopReplayAllowsEntryToBePublishedAgain()
    {
        var sink = new FakeReplaySink();
        var service = CreateService(sink, ReplaySessionConfiguration.Default with { LoopReplay = true });
        service.LoadEntries([CreateRawLogEntry("N0CALL>APRS:>Loop", AprsPacketSource.Simulation)]);

        await service.PlayNextAsync();
        await service.PlayNextAsync();

        Assert.Equal(2, sink.Dispatches.Count);
        Assert.Equal(ReplaySessionState.Ready, service.GetStatus().State);
    }

    [Fact]
    public async Task ReplayServiceHasNoTransmitGateCalls()
    {
        var sink = new FakeReplaySink();
        var service = CreateService(sink);
        service.LoadEntries([CreateRawLogEntry("N0CALL>APRS:>Receive only", AprsPacketSource.External)]);

        await service.PlayNextAsync();

        Assert.Equal(0, sink.TransmitCallCount);
        Assert.Equal(0, sink.IGateTransmitCallCount);
        Assert.Equal(0, sink.DigipeaterTransmitCallCount);
    }

    private static ReplayService CreateService(
        FakeReplaySink? sink = null,
        ReplaySessionConfiguration? configuration = null)
    {
        return new ReplayService(sink ?? new FakeReplaySink(), configuration, clock: new FakeClock { UtcNow = Now });
    }

    private static RawPacketLogEntry CreateRawLogEntry(string rawPacket, AprsPacketSource originalSource)
    {
        return new RawPacketLogEntry(
            Guid.NewGuid(),
            Now,
            rawPacket,
            null,
            null,
            null,
            [],
            originalSource,
            RawPacketLogDirection.Received,
            null,
            null,
            RawPacketValidationStatus.Valid,
            [],
            [],
            true,
            null,
            null);
    }

    private static string CreateTempFile(IReadOnlyList<string> lines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"aprs-replay-{Guid.NewGuid():N}.log");
        File.WriteAllLines(path, lines);
        return path;
    }

    private sealed class FakeReplaySink : IReplayPacketSink
    {
        public List<ReplayPacketDispatch> Dispatches { get; } = [];

        public int TransmitCallCount { get; }

        public int IGateTransmitCallCount { get; }

        public int DigipeaterTransmitCallCount { get; }

        public Task PublishReplayPacketAsync(ReplayPacketDispatch dispatch, CancellationToken cancellationToken = default)
        {
            Dispatches.Add(dispatch);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock : IBeaconSchedulerClock
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
