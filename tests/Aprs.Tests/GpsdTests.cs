using System.Text;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class GpsdTests
{
    private static readonly DateTimeOffset ReceivedAtUtc = new(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    private const string TpvJson = "{\"class\":\"TPV\",\"device\":\"/dev/ttyUSB0\",\"mode\":3,\"time\":\"2026-06-10T12:00:01.000Z\",\"lat\":39.058333,\"lon\":-84.508333,\"alt\":250.0,\"speed\":6.430,\"track\":45.0}";
    private const string NoFixTpvJson = "{\"class\":\"TPV\",\"mode\":1,\"time\":\"2026-06-10T12:00:02.000Z\"}";
    private const string SkyJson = "{\"class\":\"SKY\",\"hdop\":0.9,\"satellites\":[{\"PRN\":1,\"used\":true},{\"PRN\":2,\"used\":false},{\"PRN\":3,\"used\":true}]}";

    [Fact]
    public void DefaultConfiguration_UsesSafeGpsdDefaults()
    {
        var configuration = GpsdConfiguration.Default;

        Assert.Equal("127.0.0.1", configuration.Host);
        Assert.Equal(2947, configuration.Port);
        Assert.False(configuration.Enabled);
        Assert.True(configuration.ReconnectEnabled);
        Assert.Equal("gpsd", configuration.SourceName);
    }

    [Fact]
    public void ParseVersionAndWatchReports_SucceedsWithoutPosition()
    {
        var parser = new GpsdJsonParser();

        var version = parser.Parse("{\"class\":\"VERSION\",\"release\":\"3.25\"}", receivedAtUtc: ReceivedAtUtc);
        var watch = parser.Parse("{\"class\":\"WATCH\",\"enable\":true,\"json\":true}", receivedAtUtc: ReceivedAtUtc);

        Assert.True(version.IsParsed);
        Assert.Equal(GpsdReportType.Version, version.ReportType);
        Assert.Null(version.Position);
        Assert.True(watch.IsParsed);
        Assert.Equal(GpsdReportType.Watch, watch.ReportType);
        Assert.Null(watch.Position);
    }

    [Fact]
    public void ParseTpv_ExtractsPositionAndMotion()
    {
        var parser = new GpsdJsonParser();

        var result = parser.Parse(TpvJson, receivedAtUtc: ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.Equal(GpsdReportType.Tpv, result.ReportType);
        Assert.NotNull(result.Position);
        Assert.True(result.Position.FixValid);
        Assert.Equal(39.058333, result.Position.Latitude);
        Assert.Equal(-84.508333, result.Position.Longitude);
        Assert.Equal(250.0, result.Position.AltitudeMeters);
        Assert.Equal(12.5, result.Position.SpeedKnots.GetValueOrDefault(), precision: 1);
        Assert.Equal(45.0, result.Position.CourseDegrees);
        Assert.Equal(3, result.Position.FixQuality);
        Assert.Equal("gpsd:/dev/ttyUSB0", result.Position.SourceName);
        Assert.Equal(new DateTimeOffset(2026, 6, 10, 12, 0, 1, TimeSpan.Zero), result.Position.TimestampUtc);
    }

    [Fact]
    public void ParseTpv_ModeWithoutFixMarksPositionInvalid()
    {
        var parser = new GpsdJsonParser();

        var result = parser.Parse(NoFixTpvJson, receivedAtUtc: ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.NotNull(result.Position);
        Assert.False(result.Position.FixValid);
        Assert.Equal(1, result.Position.FixQuality);
        Assert.Null(result.Position.Latitude);
        Assert.Null(result.Position.Longitude);
    }

    [Fact]
    public void ParseSky_ExtractsSatelliteCountsAndHdop()
    {
        var parser = new GpsdJsonParser();

        var result = parser.Parse(SkyJson, receivedAtUtc: ReceivedAtUtc);

        Assert.True(result.IsParsed);
        Assert.Equal(GpsdReportType.Sky, result.ReportType);
        Assert.Equal(3, result.SatelliteCount);
        Assert.Equal(2, result.UsedSatelliteCount);
        Assert.Equal(0.9, result.Hdop);
        Assert.Null(result.Position);
    }

    [Fact]
    public void ParseMalformedJson_DoesNotCrash()
    {
        var parser = new GpsdJsonParser();

        var result = parser.Parse("{bad json", receivedAtUtc: ReceivedAtUtc);

        Assert.False(result.IsParsed);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void GpsService_AcceptsGpsdTpvAndSkyUpdates()
    {
        var parser = new GpsdJsonParser();
        var service = new GpsService();

        service.AcceptGpsdReport(parser.Parse(TpvJson, receivedAtUtc: ReceivedAtUtc), receivedAtUtc: ReceivedAtUtc);
        service.AcceptGpsdReport(parser.Parse(SkyJson, receivedAtUtc: ReceivedAtUtc.AddSeconds(1)), receivedAtUtc: ReceivedAtUtc.AddSeconds(1));

        var position = service.CurrentPosition;
        Assert.NotNull(position);
        Assert.True(service.HasValidFix);
        Assert.Equal(39.058333, position.Latitude);
        Assert.Equal(-84.508333, position.Longitude);
        Assert.Equal(2, position.SatelliteCount);
        Assert.Equal(0.9, position.Hdop);
        Assert.Equal("gpsd", position.SourceName);
    }

    [Fact]
    public async Task GpsdClient_WhenDisabled_DoesNotConnect()
    {
        var called = false;
        var client = new GpsdClient(
            GpsdConfiguration.Default,
            streamFactory: (_, _) =>
            {
                called = true;
                return Task.FromResult<Stream>(new MemoryStream());
            });

        await client.ConnectAsync(CancellationToken.None);

        Assert.False(called);
        Assert.Equal(GpsdConnectionState.Disconnected, client.State);
    }

    [Fact]
    public async Task GpsdClient_WritesWatchCommandAndPublishesPositions()
    {
        var stream = new TestDuplexStream($"{TpvJson}\n");
        var gpsService = new GpsService();
        var client = new GpsdClient(
            GpsdConfiguration.Default with
            {
                Enabled = true,
                ReconnectEnabled = false,
                SourceName = "gpsd-test"
            },
            gpsService,
            streamFactory: (_, _) => Task.FromResult<Stream>(stream));
        var received = new List<GpsPosition>();
        client.PositionReceived += (_, position) => received.Add(position);

        await client.ConnectAsync(CancellationToken.None);
        await using var enumerator = client.ReadPositionsAsync(CancellationToken.None).GetAsyncEnumerator();
        var moved = await enumerator.MoveNextAsync();
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(moved);
        Assert.Equal($"{GpsdClient.WatchCommand}\n", stream.WrittenText);
        Assert.Single(received);
        Assert.True(gpsService.HasValidFix);
        Assert.Equal(39.058333, enumerator.Current.Latitude);
    }

    private sealed class TestDuplexStream : Stream
    {
        private readonly MemoryStream readStream;
        private readonly MemoryStream writeStream = new();

        public TestDuplexStream(string readText)
        {
            readStream = new MemoryStream(Encoding.ASCII.GetBytes(readText));
        }

        public string WrittenText => Encoding.ASCII.GetString(writeStream.ToArray());

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => readStream.Length;

        public override long Position
        {
            get => readStream.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return readStream.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return readStream.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            writeStream.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            writeStream.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
    }
}
