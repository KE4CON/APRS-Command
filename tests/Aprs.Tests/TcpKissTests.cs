using System.Text;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class TcpKissTests
{
    [Fact]
    public void DefaultConfiguration_UsesDirewolfSafeDefaults()
    {
        var configuration = TcpKissConfiguration.Default;

        Assert.Equal("127.0.0.1", configuration.Host);
        Assert.Equal(8001, configuration.Port);
        Assert.False(configuration.Enabled);
        Assert.True(configuration.ReceiveEnabled);
        Assert.False(configuration.TransmitEnabled);
        Assert.True(configuration.ReconnectEnabled);
        Assert.Equal("Direwolf TCP KISS", configuration.SourceName);
    }

    [Fact]
    public void KissFrameCodec_EncodesAndDecodesEscapedPayload()
    {
        var payload = new byte[] { 0x01, KissFrameCodec.Fend, KissFrameCodec.Fesc, 0x02 };

        var encoded = KissFrameCodec.Encode(2, KissCommandType.DataFrame, payload);
        var frame = KissFrameCodec.Decode(encoded, DateTimeOffset.UtcNow, "test", new DirectPayloadDecoder());

        Assert.Equal(KissFrameCodec.Fend, encoded[0]);
        Assert.Equal(KissFrameCodec.Fend, encoded[^1]);
        Assert.Contains(KissFrameCodec.Tfend, encoded);
        Assert.Contains(KissFrameCodec.Tfesc, encoded);
        Assert.Equal(2, frame.PortNumber);
        Assert.Equal(KissCommandType.DataFrame, frame.CommandType);
        Assert.Equal(payload, frame.Payload);
    }

    [Fact]
    public void KissFrameCodec_MalformedFrameReturnsValidationErrors()
    {
        var frame = KissFrameCodec.Decode([KissFrameCodec.Fend, 0x00, KissFrameCodec.Fesc, KissFrameCodec.Fend], DateTimeOffset.UtcNow, "test");

        Assert.False(frame.IsValid);
        Assert.Contains(frame.ValidationErrors, error => error.Contains("escape", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ax25AprsPayloadDecoder_DecodesUiFrameToAprsText()
    {
        var payload = BuildAx25UiPayload("N0CALL", "APRS", ">Online");

        var result = Ax25AprsPayloadDecoder.Default.Decode(payload);

        Assert.Empty(result.ValidationErrors);
        Assert.Equal("N0CALL>APRS:>Online", result.AprsPacketText);
    }

    [Fact]
    public void Ax25AprsPayloadDecoder_MalformedFrameDoesNotThrow()
    {
        var exception = Record.Exception(() => Ax25AprsPayloadDecoder.Default.Decode([0x01, 0x02]));

        Assert.Null(exception);
        var result = Ax25AprsPayloadDecoder.Default.Decode([0x01, 0x02]);
        Assert.Null(result.AprsPacketText);
        Assert.NotEmpty(result.ValidationErrors);
    }

    [Fact]
    public async Task TcpKissClient_WhenDisabled_DoesNotConnect()
    {
        var called = false;
        var client = new TcpKissClient(
            TcpKissConfiguration.Default,
            (_, _) =>
            {
                called = true;
                return Task.FromResult<Stream>(new MemoryStream());
            });

        await client.ConnectAsync(CancellationToken.None);

        Assert.False(called);
        Assert.Equal(TcpKissConnectionState.Disconnected, client.State);
    }

    [Fact]
    public async Task TcpKissClient_ReceivesKissFrameAndPublishesAprsPacket()
    {
        var payload = BuildAx25UiPayload("N0CALL", "APRS", ">Online");
        var kissBytes = KissFrameCodec.Encode(0, KissCommandType.DataFrame, payload);
        var stream = new TestDuplexStream(kissBytes);
        var client = CreateClient(stream);
        var receivedPackets = new List<TcpKissRawPacketReceivedEventArgs>();
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
        Assert.Equal(KissCommandType.DataFrame, packet.SourceFrame.CommandType);
        Assert.Equal("Direwolf TCP KISS", packet.SourceFrame.SourceName);
    }

    [Fact]
    public async Task TcpKissClient_ReadFramesAsyncReceivesPublishedFrames()
    {
        var kissBytes = KissFrameCodec.Encode(0, KissCommandType.DataFrame, Encoding.ASCII.GetBytes("N0CALL>APRS:>Online"));
        var client = CreateClient(new TestDuplexStream(kissBytes));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await client.ConnectAsync(cancellation.Token);
        var enumerator = client.ReadFramesAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        var hasFrame = await enumerator.MoveNextAsync();
        var frame = enumerator.Current;
        await enumerator.DisposeAsync();
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(hasFrame);
        Assert.Equal("N0CALL>APRS:>Online", frame.DecodedAprsPacketText);
    }

    [Fact]
    public async Task TcpKissClient_DisconnectCancelsReceiveLoopCleanly()
    {
        var client = CreateClient(new BlockingReadStream());

        await client.ConnectAsync(CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.Equal(TcpKissConnectionState.Disconnected, client.State);
    }

    [Fact]
    public async Task TcpKissClient_TransmitDisabledFailsSafely()
    {
        var client = CreateClient(new WriteCapturingBlockingReadStream());

        var result = await client.SendFrameAsync(0, KissCommandType.DataFrame, [1, 2, 3], transmitConfirmed: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AprsTransmitDestinationTransport.TcpKiss, result.DestinationTransport);
        Assert.Contains("disabled", result.FailureReason);
    }

    [Fact]
    public async Task TcpKissClient_TransmitRequiresConnectionAndConfirmation()
    {
        var client = CreateClient(new WriteCapturingBlockingReadStream(), CreateTransmitConfiguration());

        var disconnected = await client.SendFrameAsync(0, KissCommandType.DataFrame, [1, 2, 3], transmitConfirmed: true, CancellationToken.None);
        var unconfirmed = await client.SendFrameAsync(0, KissCommandType.DataFrame, [1, 2, 3], transmitConfirmed: false, CancellationToken.None);

        Assert.False(disconnected.IsSuccess);
        Assert.Contains("not connected", disconnected.FailureReason);
        Assert.False(unconfirmed.IsSuccess);
        Assert.Contains("confirmation", unconfirmed.FailureReason);
    }

    [Fact]
    public async Task TcpKissClient_WhenTransmitEnabledWritesEncodedFrame()
    {
        var stream = new WriteCapturingBlockingReadStream();
        var client = CreateClient(stream, CreateTransmitConfiguration() with { ReceiveEnabled = false });

        await client.ConnectAsync(CancellationToken.None);
        var result = await client.SendFrameAsync(1, KissCommandType.DataFrame, Encoding.ASCII.GetBytes("N0CALL>APRS:>Online"), transmitConfirmed: true, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(stream.WrittenBytes);
        Assert.Equal(KissFrameCodec.Fend, stream.WrittenBytes[0]);
        Assert.Equal(KissFrameCodec.Fend, stream.WrittenBytes[^1]);
        Assert.Equal(1, result.Frame!.PortNumber);
    }

    private static TcpKissClient CreateClient(Stream stream)
    {
        return CreateClient(stream, TcpKissConfiguration.Default with { Enabled = true });
    }

    private static TcpKissClient CreateClient(Stream stream, TcpKissConfiguration configuration)
    {
        return new TcpKissClient(configuration, (_, _) => Task.FromResult(stream));
    }

    private static TcpKissConfiguration CreateTransmitConfiguration()
    {
        return TcpKissConfiguration.Default with
        {
            Enabled = true,
            TransmitEnabled = true
        };
    }

    private static byte[] BuildAx25UiPayload(string source, string destination, string information)
    {
        var bytes = new List<byte>();
        bytes.AddRange(EncodeAddress(destination, ssid: 0, isLast: false));
        bytes.AddRange(EncodeAddress(source, ssid: 0, isLast: true));
        bytes.Add(0x03);
        bytes.Add(0xF0);
        bytes.AddRange(Encoding.ASCII.GetBytes(information));
        return bytes.ToArray();
    }

    private static byte[] EncodeAddress(string callsign, int ssid, bool isLast)
    {
        var normalized = callsign.ToUpperInvariant().PadRight(6)[..6];
        var bytes = normalized.Select(character => (byte)(character << 1)).ToList();
        bytes.Add((byte)(0x60 | ((ssid & 0x0F) << 1) | (isLast ? 0x01 : 0x00)));
        return bytes.ToArray();
    }

    private sealed class DirectPayloadDecoder : IAx25AprsPayloadDecoder
    {
        public Ax25AprsPayloadDecodeResult Decode(IReadOnlyList<byte> payload)
        {
            return new Ax25AprsPayloadDecodeResult(null, []);
        }
    }

    private sealed class TestDuplexStream : Stream
    {
        private readonly MemoryStream readStream;
        private readonly MemoryStream writeStream = new();

        public TestDuplexStream(byte[] readBytes)
        {
            readStream = new MemoryStream(readBytes);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => readStream.Length;
        public override long Position { get => readStream.Position; set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return readStream.Read(buffer, offset, count);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return readStream.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

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

    private class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource<int> readCompletion = new();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return await readCompletion.Task;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) { }
    }

    private sealed class WriteCapturingBlockingReadStream : BlockingReadStream
    {
        private readonly MemoryStream writeStream = new();

        public byte[] WrittenBytes => writeStream.ToArray();

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
