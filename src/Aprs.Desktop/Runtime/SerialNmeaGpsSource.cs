using System.IO.Ports;
using Aprs.Services;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Reads raw NMEA sentences from a serial port and yields them one at a time.
/// Designed for USB GPS receivers (e.g. GlobalTop, u-blox) that output standard NMEA-0183.
/// </summary>
public sealed class SerialNmeaGpsSource : IGpsSentenceSource
{
    private readonly string portName;
    private readonly int baudRate;

    public SerialNmeaGpsSource(string portName, int baudRate = 4800)
    {
        this.portName = portName;
        this.baudRate = baudRate;
    }

    public async IAsyncEnumerable<string> ReadSentencesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        SerialPort? port = null;
        StreamReader? reader = null;

        string? pendingSentence = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Yield any sentence from the previous iteration before trying to read more.
            if (pendingSentence is not null)
            {
                yield return pendingSentence;
                pendingSentence = null;
            }

            try
            {
                if (port is null || !port.IsOpen)
                {
                    port?.Dispose();
                    port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout  = 5000,
                        WriteTimeout = 1000,
                        NewLine      = "\r\n"
                    };
                    port.Open();
                    reader = new StreamReader(port.BaseStream);
                }

                var line = await reader!.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is not null && line.StartsWith('$'))
                {
                    pendingSentence = line.Trim();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                port?.Dispose();
                port = null;
                reader = null;

                try { await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }

        port?.Dispose();
    }
}
