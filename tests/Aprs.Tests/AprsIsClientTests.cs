using System.Text;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class AprsIsClientTests
{
    [Fact]
    public void DefaultConfiguration_IsReceiveOnlyAndUsesSafeDefaults()
    {
        var configuration = AprsIsClientConfiguration.Default;

        Assert.Equal("rotate.aprs2.net", configuration.ServerHost);
        Assert.Equal(14580, configuration.ServerPort);
        Assert.Equal("APRSCommand", configuration.ApplicationName);
        Assert.True(configuration.ReceiveOnly);
        Assert.False(configuration.TransmitEnabled);
        Assert.True(configuration.RequireTransmitConfirmation);
        Assert.True(configuration.ReconnectEnabled);
        Assert.Equal("-1", configuration.Passcode);
    }

    [Fact]
    public void BuildLoginLine_GeneratesExpectedLogin()
    {
        var configuration = AprsIsClientConfiguration.Default with
        {
            Callsign = "N0CALL",
            Passcode = "12345",
            ApplicationVersion = "1.2.3",
            Filter = "m/50"
        };

        var loginLine = AprsIsLoginLineBuilder.Build(configuration);

        Assert.Equal("user N0CALL pass 12345 vers APRSCommand 1.2.3 filter m/50", loginLine);
    }

    [Fact]
    public void BuildLoginLine_RequiresCallsign()
    {
        var configuration = AprsIsClientConfiguration.Default with { Callsign = "" };

        Assert.Throws<ArgumentException>(() => AprsIsLoginLineBuilder.Build(configuration));
    }

    [Fact]
    public async Task ConnectAsync_WritesLoginLineToStream()
    {
        var stream = new TestDuplexStream("# logresp N0CALL verified, server test\r\n");
        var client = CreateClient(stream);

        await client.ConnectAsync(CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.Equal(
            "user N0CALL pass 12345 vers APRSCommand 1.2.3 filter m/50\r\n",
            stream.WrittenText);
    }

    [Fact]
    public async Task ConnectAsync_IgnoresServerCommentsAndPublishesRawPacketLines()
    {
        var stream = new TestDuplexStream("# logresp N0CALL verified, server test\r\n# aprsc server\r\nN0CALL>APRS:>Online\r\n");
        var client = CreateClient(stream);
        var receivedPackets = new List<AprsIsRawPacketReceivedEventArgs>();
        using var received = new SemaphoreSlim(0);
        client.RawPacketReceived += (_, packet) =>
        {
            receivedPackets.Add(packet);
            received.Release();
        };

        await client.ConnectAsync(CancellationToken.None);
        var signaled = await received.WaitAsync(TimeSpan.FromSeconds(2));
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(signaled);
        var packet = Assert.Single(receivedPackets);
        Assert.Equal("N0CALL>APRS:>Online", packet.RawPacketLine);
        Assert.True(packet.ReceivedAtUtc > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task ConnectAsync_PublishesMalformedRawLinesWithoutCrashing()
    {
        var stream = new TestDuplexStream("# logresp N0CALL verified, server test\r\nBADPACKETWITHOUTSEPARATOR\r\n");
        var client = CreateClient(stream);
        string? receivedLine = null;
        using var received = new SemaphoreSlim(0);
        client.RawPacketReceived += (_, packet) =>
        {
            receivedLine = packet.RawPacketLine;
            received.Release();
        };

        await client.ConnectAsync(CancellationToken.None);
        var signaled = await received.WaitAsync(TimeSpan.FromSeconds(2));
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(signaled);
        Assert.Equal("BADPACKETWITHOUTSEPARATOR", receivedLine);
        Assert.Null(client.LastError);
    }

    [Fact]
    public async Task ReadPacketsAsync_ReceivesPublishedPackets()
    {
        var stream = new TestDuplexStream("# logresp N0CALL verified, server test\r\nN0CALL>APRS:>Online\r\n");
        var client = CreateClient(stream);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await client.ConnectAsync(cancellation.Token);
        var enumerator = client.ReadPacketsAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var hasPacket = await enumerator.MoveNextAsync();
        var packet = enumerator.Current;
        await enumerator.DisposeAsync();
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(hasPacket);
        Assert.Equal("N0CALL>APRS:>Online", packet.RawPacketLine);
    }

    [Fact]
    public async Task DisconnectAsync_CancelsReceiveLoopCleanly()
    {
        var stream = new BlockingReadStream();
        var client = CreateClient(stream);

        await client.ConnectAsync(CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.Equal(AprsIsConnectionState.Disconnected, client.State);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenTransmitDisabled_FailsSafely()
    {
        var client = CreateClient(new LogrespThenBlockStream());

        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AprsTransmitDestinationTransport.AprsIs, result.DestinationTransport);
        Assert.Contains("transmit is disabled", result.FailureReason);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenReceiveOnly_FailsSafely()
    {
        var configuration = CreateTransmitConfiguration() with { ReceiveOnly = true };
        var client = CreateClient(new LogrespThenBlockStream(), configuration);

        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("receive-only", result.FailureReason);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenConfirmationMissing_FailsSafely()
    {
        var client = CreateClient(new LogrespThenBlockStream(), CreateTransmitConfiguration());

        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: false, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("confirmation", result.FailureReason);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenCallsignMissing_FailsSafely()
    {
        var configuration = CreateTransmitConfiguration() with { Callsign = "" };
        var client = CreateClient(new LogrespThenBlockStream(), configuration);

        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("callsign", result.FailureReason);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenPasscodeMissing_FailsSafely()
    {
        var configuration = CreateTransmitConfiguration() with { Passcode = "" };
        var client = CreateClient(new LogrespThenBlockStream(), configuration);

        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("passcode", result.FailureReason);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenPasscodeInvalid_FailsSafely()
    {
        var configuration = CreateTransmitConfiguration() with { Passcode = "-1" };
        var client = CreateClient(new LogrespThenBlockStream(), configuration);

        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("passcode", result.FailureReason);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenDisconnected_FailsSafely()
    {
        var client = CreateClient(new LogrespThenBlockStream(), CreateTransmitConfiguration());

        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AprsIsConnectionState.Disconnected, result.ConnectedStateAtRequest);
        Assert.Contains("not connected", result.FailureReason);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenPacketIsEmpty_FailsSafely()
    {
        var stream = new WriteCapturingBlockingReadStream();
        var client = CreateClient(stream, CreateTransmitConfiguration());

        await client.ConnectAsync(CancellationToken.None);
        var result = await client.SendRawPacketAsync("", transmitConfirmed: true, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.FailureReason);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenPacketContainsNewline_FailsSafely()
    {
        var stream = new WriteCapturingBlockingReadStream();
        var client = CreateClient(stream, CreateTransmitConfiguration());

        await client.ConnectAsync(CancellationToken.None);
        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online\nBAD>APRS:>No", transmitConfirmed: true, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("line breaks", result.FailureReason);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenPacketMalformed_FailsSafely()
    {
        var stream = new WriteCapturingBlockingReadStream();
        var client = CreateClient(stream, CreateTransmitConfiguration());

        await client.ConnectAsync(CancellationToken.None);
        var result = await client.SendRawPacketAsync("BADPACKETWITHOUTSEPARATOR", transmitConfirmed: true, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("missing ':'", result.FailureReason);
    }

    [Fact]
    public async Task SendRawPacketAsync_WhenTransmitEnabledAndConnected_WritesPacketToStream()
    {
        var stream = new WriteCapturingBlockingReadStream();
        var client = CreateClient(stream, CreateTransmitConfiguration());

        await client.ConnectAsync(CancellationToken.None);
        var result = await client.SendRawPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.FailureReason);
        Assert.Equal("N0CALL>APRS:>Online", result.RawPacket);
        Assert.Equal(AprsTransmitDestinationTransport.AprsIs, result.DestinationTransport);
        Assert.Equal(AprsIsConnectionState.Connected, result.ConnectedStateAtRequest);
        Assert.Equal(
            "user N0CALL pass 12345 vers APRSCommand 1.2.3 filter m/50\r\nN0CALL>APRS:>Online\r\n",
            stream.WrittenText);
    }

    private static AprsIsClient CreateClient(Stream stream)
    {
        var configuration = AprsIsClientConfiguration.Default with
        {
            Callsign = "N0CALL",
            Passcode = "12345",
            ApplicationVersion = "1.2.3",
            Filter = "m/50",
            ReconnectEnabled = false
        };

        return new AprsIsClient(configuration, (_, _) => Task.FromResult(stream));
    }

    private static AprsIsClient CreateClient(Stream stream, AprsIsClientConfiguration configuration)
    {
        return new AprsIsClient(configuration, (_, _) => Task.FromResult(stream));
    }

    private static AprsIsClientConfiguration CreateTransmitConfiguration()
    {
        return AprsIsClientConfiguration.Default with
        {
            Callsign = "N0CALL",
            Passcode = "12345",
            ApplicationVersion = "1.2.3",
            Filter = "m/50",
            ReconnectEnabled = false,
            ReceiveOnly = false,
            TransmitEnabled = true,
            RequireTransmitConfirmation = true,
            DefaultTransmitSource = "N0CALL"
        };
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

    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
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
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Sends an immediate logresp then blocks reads forever (until cancelled).
    /// Captures all written bytes for inspection.
    /// </summary>
    private sealed class LogrespThenBlockStream : Stream
    {
        private readonly MemoryStream writeStream = new();
        private readonly byte[] logrespBytes = Encoding.ASCII.GetBytes("# logresp N0CALL verified, server test\r\n");
        private bool logrespSent = false;

        public string WrittenText => Encoding.ASCII.GetString(writeStream.ToArray());

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count)
        {
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!logrespSent)
            {
                logrespSent = true;
                var n = Math.Min(buffer.Length, logrespBytes.Length);
                logrespBytes.AsMemory(0, n).CopyTo(buffer);
                return n;
            }
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override void Write(byte[] buffer, int offset, int count) => writeStream.Write(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }

        private sealed class WriteCapturingBlockingReadStream : Stream
    {
        private readonly MemoryStream writeStream = new();

        public string WrittenText => Encoding.ASCII.GetString(writeStream.ToArray());

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => 0;

        public override long Position
        {
            get => 0;
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
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
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
