using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aprs.Core;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

// Regression tests for the 2026-08-10 audit Criticals C1/C2: StationDatabase and RawPacketLogService
// were mutated on background transport (KISS/AGWPE/serial) receive threads while the UI read them,
// with no synchronization -> InvalidOperationException / corruption on any hardware-TNC session.
// Without the per-instance locks these tests throw "Collection was modified" reliably within milliseconds.
public sealed class ConcurrencyRegressionTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);

    private static AprsPacket Parse(string raw) => new AprsParser().Parse(raw, Now);

    [Fact]
    public async Task StationDatabase_ConcurrentProcessAndRead_DoesNotThrow()
    {
        var database = new StationDatabase();
        var packets = new List<AprsPacket>();
        for (var i = 0; i < 60; i++)
        {
            packets.Add(Parse($"N0C{i:D2}>APRS,TCPIP*:!3903.50N/08430.50W-beacon {i}"));
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var writer = Task.Run(() =>
        {
            var random = new Random(1);
            while (!cts.IsCancellationRequested)
            {
                database.ProcessPacket(packets[random.Next(packets.Count)], AprsPacketSource.Rf);
            }
        });

        var reader = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                _ = database.GetVisibleStations();
                _ = database.GetAllStations();
                database.UpdateAgeStates(Now.AddMinutes(1));
                _ = database.GetTrail("N0C01");
            }
        });

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(writer, reader));
        Assert.Null(exception);
        Assert.NotEmpty(database.GetAllStations());
    }

    [Fact]
    public async Task RawPacketLog_ConcurrentAddAndRead_DoesNotThrow()
    {
        var log = new RawPacketLogService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        var writer = Task.Run(() =>
        {
            var i = 0;
            while (!cts.IsCancellationRequested)
            {
                log.AddReceivedRawPacket($"N0CALL>APRS:!3903.50N/08430.50W-{i++}", AprsPacketSource.Rf);
            }
        });

        var reader = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                _ = log.GetRecentEntries(100);
                _ = log.GetEntriesByDirection(RawPacketLogDirection.Received);
            }
        });

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(writer, reader));
        Assert.Null(exception);
    }
}
