using System.Text;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class AgwpeTests
{
    [Fact]
    public void DefaultConfiguration_UsesSafeAgwpeDefaults()
    {
        var configuration = AgwpeConfiguration.Default;

        Assert.Equal("127.0.0.1", configuration.Host);
        Assert.Equal(8000, configuration.Port);
        Assert.False(configuration.Enabled);
        Assert.True(configuration.ReceiveEnabled);
        Assert.False(configuration.TransmitEnabled);
        Assert.True(configuration.ReconnectEnabled);
        Assert.Equal("AGWPE", configuration.SourceName);
        Assert.Equal(0, configuration.SelectedRadioPort);
    }

    [Fact]
    public void AgwpeFrameCodec_MalformedFrameDoesNotCrash()
    {
        var codec = new AgwpeFrameCodec();

        var exception = Record.Exception(() => codec.Decode([1, 2, 3], DateTimeOffset.UtcNow, "test"));
        var frame = codec.Decode([1, 2, 3], DateTimeOffset.UtcNow, "test");

        Assert.Null(exception);
        Assert.False(frame.IsValid);
        Assert.Contains(frame.ValidationErrors, error => error.Contains("header", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AgwpeFrameCodec_IncompleteFrameHandlingIsSafe()
    {
        var codec = new AgwpeFrameCodec();
        var complete = codec.Encode('K', 0, "N0CALL", "APRS", Encoding.ASCII.GetBytes("N0CALL>APRS:>Online"));
        var incomplete = complete.Take(complete.Length - 3).ToArray();

        var frame = codec.Decode(incomplete, DateTimeOffset.UtcNow, "test");
        var completeEnd = codec.FindLastCompleteFrameEnd(incomplete);

        Assert.False(frame.IsValid);
        Assert.Contains(frame.ValidationErrors, error => error.Contains("incomplete", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(-1, completeEnd);
    }

    [Fact]
    public void AgwpeFrameCodec_DecodesFakeReceivedAprsPacket()
    {
        var codec = new AgwpeFrameCodec();
        var payload = BuildAx25UiPayload("N0CALL", "APRS", ">Online");
        var bytes = codec.Encode('K', 2, "N0CALL", "APRS", payload);

        var frame = codec.Decode(bytes, DateTimeOffset.UtcNow, "AGWPE");

        Assert.True(frame.IsValid);
        Assert.Equal('K', frame.CommandType);
        Assert.Equal(2, frame.RadioPort);
        Assert.Equal("N0CALL", frame.SourceCallsign);
        Assert.Equal("APRS", frame.DestinationCallsign);
        Assert.Equal("N0CALL>APRS:>Online", frame.DecodedAprsPacketText);
    }

    [Fact]
    public void AgwpeFrameCodec_PreservesUnknownFrameTypes()
    {
        var codec = new AgwpeFrameCodec();
        var bytes = codec.Encode('X', 0, string.Empty, string.Empty, [1, 2, 3]);

        var frame = codec.Decode(bytes, DateTimeOffset.UtcNow, "AGWPE");

        Assert.True(frame.IsValid);
        Assert.Equal('X', frame.CommandType);
        Assert.Null(frame.DecodedAprsPacketText);
        Assert.Equal([1, 2, 3], frame.Payload);
    }

    [Fact]
    public async Task AgwpeClient_WhenDisabled_DoesNotConnect()
    {
        var called = false;
        var client = new AgwpeClient(
            AgwpeConfiguration.Default,
            (_, _) =>
            {
                called = true;
                return Task.FromResult<Stream>(new MemoryStream());
            });

        await client.ConnectAsync(CancellationToken.None);

        Assert.False(called);
        Assert.Equal(AgwpeConnectionState.Disconnected, client.State);
    }

    [Fact]
    public async Task AgwpeClient_ReceivesFrameAndPublishesAprsPacket()
    {
        var codec = new AgwpeFrameCodec();
        var payload = BuildAx25UiPayload("N0CALL", "APRS", ">Online");
        var bytes = codec.Encode('K', 1, "N0CALL", "APRS", payload);
        var client = CreateClient(new TestDuplexStream(bytes));
        var receivedPackets = new List<AgwpeRawPacketReceivedEventArgs>();
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
        Assert.Equal(1, packet.SourceFrame.RadioPort);
        Assert.Equal("AGWPE", packet.SourceFrame.PacketSource);
    }

    [Fact]
    public async Task AgwpeClient_DisconnectedClientBlocksTransmit()
    {
        var client = CreateClient(new WriteCapturingBlockingReadStream(), CreateTransmitConfiguration());

        var result = await client.SendPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AprsTransmitDestinationTransport.Agwpe, result.DestinationTransport);
        Assert.Contains("not connected", result.FailureReason);
    }

    [Fact]
    public async Task AgwpeClient_InvalidRadioPortBlocksTransmit()
    {
        var client = CreateClient(new WriteCapturingBlockingReadStream(), CreateTransmitConfiguration() with { SelectedRadioPort = 300 });

        var result = await client.SendPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("radio port", result.FailureReason);
    }

    [Fact]
    public async Task AgwpeClient_TransmitDisabledByDefaultFailsSafely()
    {
        var client = CreateClient(new WriteCapturingBlockingReadStream());

        var result = await client.SendPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("disabled", result.FailureReason);
    }

    [Fact]
    public async Task AgwpeClient_RfSafetyGateBlocksTransmit()
    {
        var stream = new WriteCapturingBlockingReadStream();
        var client = CreateClient(stream, CreateTransmitConfiguration() with { ReceiveEnabled = false });

        await client.ConnectAsync(CancellationToken.None);
        var result = await client.SendPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, rfSafetyEnabled: false, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("RF transmit safety", result.FailureReason);
        Assert.Empty(stream.WrittenBytes);
    }

    [Fact]
    public async Task AgwpeClient_RejectsEmptyLineBreakAndMalformedPackets()
    {
        var stream = new WriteCapturingBlockingReadStream();
        var client = CreateClient(stream, CreateTransmitConfiguration() with { ReceiveEnabled = false });

        await client.ConnectAsync(CancellationToken.None);
        var empty = await client.SendPacketAsync("", transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);
        var lineBreak = await client.SendPacketAsync("N0CALL>APRS:>Online\nBAD", transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);
        var malformed = await client.SendPacketAsync("BADPACKET", transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.False(empty.IsSuccess);
        Assert.Contains("empty", empty.FailureReason);
        Assert.False(lineBreak.IsSuccess);
        Assert.Contains("line breaks", lineBreak.FailureReason);
        Assert.False(malformed.IsSuccess);
        Assert.Contains("separator", malformed.FailureReason);
        Assert.Empty(stream.WrittenBytes);
    }

    [Fact]
    public async Task AgwpeClient_WhenExplicitlyEnabledAndSafeWritesFrameToFakeStream()
    {
        var stream = new WriteCapturingBlockingReadStream();
        var client = CreateClient(stream, CreateTransmitConfiguration() with { ReceiveEnabled = false, SelectedRadioPort = 3 });

        await client.ConnectAsync(CancellationToken.None);
        var result = await client.SendPacketAsync("N0CALL>APRS:>Online", transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(stream.WrittenBytes);
        Assert.Equal(3, result.Frame!.RadioPort);
        Assert.Equal('K', result.Frame.CommandType);
    }

    [Fact]
    public void ConfiguredAgwpePortProvider_ReturnsSelectedPort()
    {
        var provider = new ConfiguredAgwpePortProvider(AgwpeConfiguration.Default with { SelectedRadioPort = 2 });

        var port = Assert.Single(provider.GetKnownPorts());

        Assert.Equal(2, port.PortNumber);
        Assert.True(port.ReceiveEnabled);
        Assert.False(port.TransmitEnabled);
    }

    private static AgwpeClient CreateClient(Stream stream)
    {
        return CreateClient(stream, AgwpeConfiguration.Default with { Enabled = true, ReconnectEnabled = false });
    }

    private static AgwpeClient CreateClient(Stream stream, AgwpeConfiguration configuration)
    {
        return new AgwpeClient(configuration, (_, _) => Task.FromResult(stream));
    }

    private static AgwpeConfiguration CreateTransmitConfiguration()
    {
        return AgwpeConfiguration.Default with
        {
            Enabled = true,
            ReconnectEnabled = false,
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

        public override void Write(byte[] buffer, int offset, int count)
        {
            writeStream.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            writeStream.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class WriteCapturingBlockingReadStream : Stream
    {
        private readonly MemoryStream writeStream = new();

        public byte[] WrittenBytes => writeStream.ToArray();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }

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

        public override void Write(byte[] buffer, int offset, int count)
        {
            writeStream.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            writeStream.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
