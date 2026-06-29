using Aprs.Services;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Routes simulated APRS packets into the live AprsIngestionService pipeline
/// so they appear on the map, station list, and raw packet log exactly like
/// real received packets — but tagged with AprsPacketSource.Simulation.
/// </summary>
public sealed class LiveSimulatedPacketSink : ISimulatedAprsPacketSink
{
    private readonly AprsIngestionService ingestionService;

    public LiveSimulatedPacketSink(AprsIngestionService ingestionService)
        => this.ingestionService = ingestionService
            ?? throw new ArgumentNullException(nameof(ingestionService));

    public Task PublishSimulatedPacketAsync(
        SimulatedAprsPacket packet,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(packet.RawPacket))
        {
            ingestionService.IngestReceivedLine(
                packet.RawPacket,
                AprsPacketSource.Simulation,
                packet.GeneratedAtUtc);
        }
        return Task.CompletedTask;
    }
}
