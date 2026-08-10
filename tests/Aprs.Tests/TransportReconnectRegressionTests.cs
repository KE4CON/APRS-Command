using System.Collections.Concurrent;
using System.Text;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// Regression tests for the transport reconnect fixes in the 2026-08-10 audit.
/// </summary>
public sealed class TransportReconnectRegressionTests
{
    // Transport M1: on reconnect, APRS-IS drained the packets that arrive bundled after the
    // "# logresp" line only on the very first connect, never after a reconnect — so every packet
    // the server pipelines immediately behind the reconnect's logresp was silently dropped.
    [Fact]
    public async Task AprsIs_Reconnect_PublishesPacketsBundledAfterLogresp()
    {
        // First stream: logresp, then EOF -> forces the receive loop into its reconnect branch.
        // Second stream: logresp with a real packet pipelined right behind it, then blocks.
        var streams = new ConcurrentQueue<Stream>();
        streams.Enqueue(new ScriptedReadStream("# logresp N0CALL verified, server test\r\n", eofAfterScript: true));
        streams.Enqueue(new ScriptedReadStream(
            "# logresp N0CALL verified, server test\r\nN0CALL>APRS,TCPIP*:>bundled after reconnect\r\n",
            eofAfterScript: false));

        var configuration = AprsIsClientConfiguration.Default with
        {
            Callsign = "N0CALL",
            Passcode = "12345",
            ApplicationVersion = "1.2.3",
            Filter = "m/50",
            ReconnectEnabled = true,
            ReconnectDelay = TimeSpan.FromMilliseconds(10),
        };

        var received = new List<string>();
        var gotBundled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new AprsIsClient(configuration, (_, _) =>
        {
            if (streams.TryDequeue(out var next))
            {
                return Task.FromResult(next);
            }

            // After both scripted streams are exhausted, hand back a stream that just blocks so the
            // reconnect loop parks instead of spinning.
            return Task.FromResult<Stream>(new ScriptedReadStream(string.Empty, eofAfterScript: false));
        });

        client.RawPacketReceived += (_, e) =>
        {
            lock (received)
            {
                received.Add(e.RawPacketLine);
            }

            if (e.RawPacketLine.Contains("bundled after reconnect"))
            {
                gotBundled.TrySetResult();
            }
        };

        await client.ConnectAsync(CancellationToken.None);

        var completed = await Task.WhenAny(gotBundled.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await client.DisconnectAsync(CancellationToken.None);

        Assert.True(completed == gotBundled.Task, "The packet bundled after the reconnect logresp was never published.");
        lock (received)
        {
            Assert.Contains("N0CALL>APRS,TCPIP*:>bundled after reconnect", received);
        }
    }

    /// <summary>
    /// A stream that returns a fixed script of bytes on first read, then either signals EOF
    /// (Read returns 0) or blocks forever until cancelled.
    /// </summary>
    private sealed class ScriptedReadStream : Stream
    {
        private readonly byte[] script;
        private readonly bool eofAfterScript;
        private readonly MemoryStream writeStream = new();
        private int position;

        public ScriptedReadStream(string script, bool eofAfterScript)
        {
            this.script = Encoding.ASCII.GetBytes(script);
            this.eofAfterScript = eofAfterScript;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => script.Length;
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var remaining = script.Length - position;
            if (remaining > 0)
            {
                var n = Math.Min(remaining, buffer.Length);
                script.AsSpan(position, n).CopyTo(buffer);
                position += n;
                return n;
            }

            if (eofAfterScript)
            {
                return 0;
            }

            Thread.Sleep(Timeout.Infinite);
            return 0;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var remaining = script.Length - position;
            if (remaining > 0)
            {
                var n = Math.Min(remaining, buffer.Length);
                script.AsMemory(position, n).CopyTo(buffer);
                position += n;
                return n;
            }

            if (eofAfterScript)
            {
                return 0;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        public override void Write(byte[] buffer, int offset, int count) => writeStream.Write(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
