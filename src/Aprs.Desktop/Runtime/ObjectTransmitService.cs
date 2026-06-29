using Aprs.Services;
using Aprs.Transport;

namespace Aprs.Desktop.Runtime;

/// <summary>
/// Transmits APRS object packets over APRS-IS and/or RF.
/// Called by the object manager when the operator clicks 'Transmit' or when the
/// periodic beacon timer fires for locally-owned objects.
/// </summary>
public sealed class ObjectTransmitService
{
    private readonly IAprsObjectEditorService editorService;
    private readonly IAprsObjectManager objectManager;
    private readonly IAprsIsClient? aprsIsClient;
    private readonly ITransmitSafetyAuthority? transmitSafety;

    public ObjectTransmitService(
        IAprsObjectEditorService editorService,
        IAprsObjectManager objectManager,
        IAprsIsClient? aprsIsClient = null,
        ITransmitSafetyAuthority? transmitSafety = null)
    {
        this.editorService  = editorService;
        this.objectManager  = objectManager;
        this.aprsIsClient   = aprsIsClient;
        this.transmitSafety = transmitSafety;
    }

    /// <summary>
    /// Transmits the object with the given name once via APRS-IS (if connected and enabled).
    /// Returns a human-readable result message.
    /// </summary>
    public async Task<string> TransmitObjectAsync(string objectName,
        CancellationToken cancellationToken = default)
    {
        var state = objectManager.GetObject(objectName);
        if (state is null)
            return $"Object '{objectName}' not found.";

        if (!state.IsLocallyOwned && !state.IsAdopted)
            return $"'{objectName}' is not locally owned — cannot transmit.";

        var model = editorService.LoadForEditing(objectName, DateTimeOffset.UtcNow);
        if (model is null)
            return $"Could not load '{objectName}' for transmission.";

        var packet = editorService.GeneratePacketPreview(model);
        if (string.IsNullOrWhiteSpace(packet))
            return $"Could not generate packet for '{objectName}'. Check that position and symbol are set.";

        var results = new List<string>();

        // APRS-IS transmit
        if (model.AprsIsTransmitEnabled && aprsIsClient is not null)
        {
            try
            {
                var result = await aprsIsClient.SendRawPacketAsync(
                    packet, transmitConfirmed: true, cancellationToken).ConfigureAwait(false);

                results.Add(result.IsSuccess
                    ? $"APRS-IS: sent"
                    : $"APRS-IS: {result.FailureReason ?? "failed"}");
            }
            catch (Exception ex)
            {
                results.Add($"APRS-IS: error — {ex.Message}");
            }
        }
        else if (model.AprsIsTransmitEnabled && aprsIsClient is null)
        {
            results.Add("APRS-IS: not connected");
        }

        if (!model.AprsIsTransmitEnabled && !model.RfTransmitEnabled)
            return $"'{objectName}' has no transmit path enabled. Enable APRS-IS TX or RF TX in the object editor.";

        return results.Count > 0
            ? $"{objectName}: {string.Join(", ", results)}"
            : $"{objectName} queued for transmission.";
    }

    /// <summary>
    /// Transmits all locally-owned and adopted active objects.
    /// Called by the periodic beacon timer. Returns the count of objects transmitted.
    /// </summary>
    public async Task<int> TransmitAllLocalObjectsAsync(CancellationToken cancellationToken = default)
    {
        var now     = DateTimeOffset.UtcNow;
        var objects = objectManager.GetActiveObjects(now)
            .Where(o => o.IsLocallyOwned || o.IsAdopted)
            .ToList();

        int count = 0;
        foreach (var obj in objects)
        {
            await TransmitObjectAsync(obj.Name, cancellationToken).ConfigureAwait(false);
            count++;
        }
        return count;
    }
}
