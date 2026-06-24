using System.Text;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

public sealed class SerialKissTests
{
    [Fact]
    public void DefaultConfiguration_UsesSafeSerialKissDefaults()
    {
        var configuration = SerialKissConfiguration.Default;

        Assert.False(configuration.Enabled);
        Assert.True(configuration.ReceiveEnabled);
        Assert.False(configuration.TransmitEnabled);
        Assert.True(configuration.ReconnectEnabled);
        Assert.Equal(9600, configuration.BaudRate);
        Assert.Equal(8, configuration.DataBits);
        Assert.Equal(SerialKissParity.None, configuration.Parity);
        Assert.Equal(SerialKissStopBits.One, configuration.StopBits);
        Assert.Equal(SerialKissHandshake.None, configuration.Handshake);
        Assert.Equal("Serial KISS TNC", configuration.SourceName);
    }

    [Fact]
    public async Task FakeSerialPort_CanBeOpenedAndClosed()
    {
        var port = new FakeSerialPortConnection("TEST", 9600);

        await port.OpenAsync(CancellationToken.None);
        Assert.True(port.IsOpen);

        await port.CloseAsync(CancellationToken.None);
        Assert.False(port.IsOpen);
    }

    [Fact]
    public async Task SerialKissClient_WhenDisabled_DoesNotOpenPort()
    {
        var port = new FakeSerialPortConnection("TEST", 9600);
        var client = new SerialKissClient(SerialKissConfiguration.Default, new FakeSerialPortFactory(port));

        await client.ConnectAsync(CancellationToken.None);

        Assert.False(port.IsOpen);
        Assert.Equal(SerialKissConnectionState.Disconnected, client.State);
    }

    [Fact]
    public async Task SerialKissClient_ReceivesKissFrameAndPublishesAprsPacket()
    {
        var payload = BuildAx25UiPayload("N0CALL", "APRS", ">Online");
        var kissBytes = KissFrameCodec.Encode(0, KissCommandType.DataFrame, payload);
        var port = new FakeSerialPortConnection("TEST", 9600, kissBytes);
        var client = CreateClient(port);
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
        Assert.Equal("Serial KISS TNC", packet.SourceFrame.SourceName);
    }

    [Fact]
    public async Task SerialKissClient_MalformedSerialDataDoesNotCrash()
    {
        var port = new FakeSerialPortConnection("TEST", 9600, [KissFrameCodec.Fend, 0x00, KissFrameCodec.Fesc, KissFrameCodec.Fend]);
        var client = CreateClient(port);
        var frames = new List<KissFrame>();
        using var received = new SemaphoreSlim(0);
        client.FrameReceived += (_, frame) =>
        {
            frames.Add(frame.Frame);
            received.Release();
        };

        await client.ConnectAsync(CancellationToken.None);
        var signaled = await received.WaitAsync(TimeSpan.FromSeconds(2));
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(signaled);
        var frame = Assert.Single(frames);
        Assert.False(frame.IsValid);
        Assert.Null(client.LastError);
    }

    [Fact]
    public async Task SerialKissClient_DisconnectedClientBlocksTransmit()
    {
        var port = new FakeSerialPortConnection("TEST", 9600);
        var client = CreateClient(port, CreateTransmitConfiguration());

        var result = await client.SendFrameAsync(0, KissCommandType.DataFrame, [1, 2, 3], transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AprsTransmitDestinationTransport.SerialKiss, result.DestinationTransport);
        Assert.Contains("not connected", result.FailureReason);
    }

    [Fact]
    public async Task SerialKissClient_ClosedSerialPortBlocksTransmit()
    {
        var port = new FakeSerialPortConnection("TEST", 9600);
        var client = CreateClient(port, CreateTransmitConfiguration() with { ReceiveEnabled = false });

        await client.ConnectAsync(CancellationToken.None);
        await port.CloseAsync(CancellationToken.None);
        var result = await client.SendFrameAsync(0, KissCommandType.DataFrame, [1, 2, 3], transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("not open", result.FailureReason);
    }

    [Fact]
    public async Task SerialKissClient_TransmitDisabledByDefaultFailsSafely()
    {
        var port = new FakeSerialPortConnection("TEST", 9600);
        var client = CreateClient(port);

        var result = await client.SendFrameAsync(0, KissCommandType.DataFrame, [1, 2, 3], transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("disabled", result.FailureReason);
    }

    [Fact]
    public async Task SerialKissClient_RfSafetyGateBlocksTransmit()
    {
        var port = new FakeSerialPortConnection("TEST", 9600);
        var client = CreateClient(port, CreateTransmitConfiguration() with { ReceiveEnabled = false });

        await client.ConnectAsync(CancellationToken.None);
        var result = await client.SendFrameAsync(0, KissCommandType.DataFrame, [1, 2, 3], transmitConfirmed: true, rfSafetyEnabled: false, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("RF transmit safety", result.FailureReason);
    }

    [Fact]
    public async Task SerialKissClient_EmptyPayloadBlocksTransmit()
    {
        var port = new FakeSerialPortConnection("TEST", 9600);
        var client = CreateClient(port, CreateTransmitConfiguration() with { ReceiveEnabled = false });

        await client.ConnectAsync(CancellationToken.None);
        var result = await client.SendFrameAsync(0, KissCommandType.DataFrame, [], transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.FailureReason);
    }

    [Fact]
    public async Task SerialKissClient_WhenExplicitlyEnabledWritesEncodedKissFrame()
    {
        var port = new FakeSerialPortConnection("TEST", 9600);
        var client = CreateClient(port, CreateTransmitConfiguration() with { ReceiveEnabled = false });

        await client.ConnectAsync(CancellationToken.None);
        var result = await client.SendFrameAsync(1, KissCommandType.DataFrame, Encoding.ASCII.GetBytes("N0CALL>APRS:>Online"), transmitConfirmed: true, rfSafetyEnabled: true, CancellationToken.None);
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(port.WrittenBytes);
        Assert.Equal(KissFrameCodec.Fend, port.WrittenBytes[0]);
        Assert.Equal(KissFrameCodec.Fend, port.WrittenBytes[^1]);
        Assert.Equal(1, result.Frame!.PortNumber);
    }

    [Fact]
    public void SerialPortDiscovery_CanBeMocked()
    {
        ISerialPortDiscovery discovery = new FakeSerialPortDiscovery(["COM3", "/dev/ttyUSB0"]);

        var ports = discovery.GetAvailablePortNames();

        Assert.Contains("COM3", ports);
        Assert.Contains("/dev/ttyUSB0", ports);
    }

    private static SerialKissClient CreateClient(FakeSerialPortConnection port)
    {
        return CreateClient(port, SerialKissConfiguration.Default with { Enabled = true, PortName = port.PortName });
    }

    private static SerialKissClient CreateClient(FakeSerialPortConnection port, SerialKissConfiguration configuration)
    {
        return new SerialKissClient(configuration, new FakeSerialPortFactory(port));
    }

    private static SerialKissConfiguration CreateTransmitConfiguration()
    {
        return SerialKissConfiguration.Default with
        {
            Enabled = true,
            PortName = "TEST",
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

    private sealed class FakeSerialPortFactory : ISerialPortConnectionFactory
    {
        private readonly FakeSerialPortConnection port;

        public FakeSerialPortFactory(FakeSerialPortConnection port)
        {
            this.port = port;
        }

        public ISerialPortConnection Create(SerialKissConfiguration configuration)
        {
            return port;
        }
    }

    private sealed class FakeSerialPortDiscovery : ISerialPortDiscovery
    {
        private readonly IReadOnlyList<string> ports;

        public FakeSerialPortDiscovery(IReadOnlyList<string> ports)
        {
            this.ports = ports;
        }

        public IReadOnlyList<string> GetAvailablePortNames()
        {
            return ports;
        }
    }

    private sealed class FakeSerialPortConnection : ISerialPortConnection
    {
        private readonly MemoryStream readStream;
        private readonly MemoryStream writeStream = new();

        public FakeSerialPortConnection(string portName, int baudRate, byte[]? readBytes = null)
        {
            PortName = portName;
            BaudRate = baudRate;
            readStream = new MemoryStream(readBytes ?? []);
        }

        public string PortName { get; }

        public int BaudRate { get; }

        public bool IsOpen { get; private set; }

        public byte[] WrittenBytes => writeStream.ToArray();

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            IsOpen = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken)
        {
            IsOpen = false;
            return Task.CompletedTask;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!IsOpen)
            {
                return ValueTask.FromResult(0);
            }

            return readStream.ReadAsync(buffer, cancellationToken);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("Serial port is closed.");
            }

            writeStream.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            readStream.Dispose();
            writeStream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
