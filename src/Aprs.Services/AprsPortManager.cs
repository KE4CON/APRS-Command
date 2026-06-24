namespace Aprs.Services;

public sealed class AprsPortManager : IAprsPortManager
{
    private readonly Dictionary<string, AprsPortSnapshot> ports = new(StringComparer.OrdinalIgnoreCase);

    public AprsPortSnapshot RegisterPort(AprsPortSnapshot port)
    {
        if (string.IsNullOrWhiteSpace(port.PortId))
        {
            throw new ArgumentException("Port ID is required.", nameof(port));
        }

        var normalized = port with { PortId = NormalizePortId(port.PortId) };
        ports[normalized.PortId] = normalized;
        return normalized;
    }

    public bool RemovePort(string portId)
    {
        return ports.Remove(NormalizePortId(portId));
    }

    public bool SetPortEnabled(string portId, bool enabled, DateTimeOffset timestampUtc)
    {
        return UpdatePort(portId, port => port with
        {
            Enabled = enabled,
            ConnectionState = enabled ? port.ConnectionState : AprsPortConnectionState.Disabled,
            LastDisconnectedUtc = enabled ? port.LastDisconnectedUtc : timestampUtc
        });
    }

    public IReadOnlyCollection<AprsPortSnapshot> GetAllPorts()
    {
        return SortPorts(ports.Values);
    }

    public IReadOnlyCollection<AprsPortSnapshot> GetReceiveEnabledPorts()
    {
        return SortPorts(ports.Values.Where(port => port.IsReceiveAvailable));
    }

    public IReadOnlyCollection<AprsPortSnapshot> GetTransmitEnabledPorts()
    {
        return SortPorts(ports.Values.Where(port => port.IsTransmitConfigured));
    }

    public AprsPortSnapshot? GetPort(string portId)
    {
        return ports.TryGetValue(NormalizePortId(portId), out var port) ? port : null;
    }

    public bool UpdateConnectionState(string portId, AprsPortConnectionState connectionState, DateTimeOffset timestampUtc)
    {
        return UpdatePort(portId, port => port with
        {
            ConnectionState = connectionState,
            LastConnectedUtc = connectionState == AprsPortConnectionState.Connected ? timestampUtc : port.LastConnectedUtc,
            LastDisconnectedUtc = connectionState == AprsPortConnectionState.Disconnected || connectionState == AprsPortConnectionState.Disabled
                ? timestampUtc
                : port.LastDisconnectedUtc
        });
    }

    public bool RecordPacketReceived(string portId, DateTimeOffset timestampUtc)
    {
        return UpdatePort(portId, port => port with
        {
            LastPacketReceivedUtc = timestampUtc,
            PacketCountReceived = port.PacketCountReceived + 1
        });
    }

    public bool RecordPacketTransmitted(string portId, DateTimeOffset timestampUtc)
    {
        return UpdatePort(portId, port => port with
        {
            LastPacketTransmittedUtc = timestampUtc,
            PacketCountTransmitted = port.PacketCountTransmitted + 1
        });
    }

    public bool RecordError(string portId, string error, DateTimeOffset timestampUtc)
    {
        return UpdatePort(portId, port => port with
        {
            LastError = error,
            ConnectionState = AprsPortConnectionState.Faulted,
            LastDisconnectedUtc = timestampUtc
        });
    }

    public bool ClearCounters(string portId)
    {
        return UpdatePort(portId, port => port with
        {
            PacketCountReceived = 0,
            PacketCountTransmitted = 0,
            LastPacketReceivedUtc = null,
            LastPacketTransmittedUtc = null
        });
    }

    public void ClearAllCounters()
    {
        foreach (var portId in ports.Keys.ToArray())
        {
            ClearCounters(portId);
        }
    }

    public AprsPortHealthSummary GetHealthSummary()
    {
        var currentPorts = ports.Values.ToArray();
        var errors = currentPorts
            .Where(port => !string.IsNullOrWhiteSpace(port.LastError))
            .Select(port => $"{port.PortName}: {port.LastError}")
            .ToArray();

        return new AprsPortHealthSummary(
            currentPorts.Length,
            currentPorts.Count(port => port.Enabled),
            currentPorts.Count(port => port.ConnectionState == AprsPortConnectionState.Connected),
            currentPorts.Count(port => port.ConnectionState == AprsPortConnectionState.Faulted),
            currentPorts.Count(port => port.IsReceiveAvailable),
            currentPorts.Count(port => port.IsTransmitConfigured),
            currentPorts.Sum(port => port.PacketCountReceived),
            currentPorts.Sum(port => port.PacketCountTransmitted),
            errors.Length > 0,
            errors);
    }

    public AprsPortTransmitSafetyResult CheckTransmitSafety(string portId, bool globalTransmitSafetyEnabled)
    {
        if (!ports.TryGetValue(NormalizePortId(portId), out var port))
        {
            return new AprsPortTransmitSafetyResult(false, "Port is not registered.", null);
        }

        if (!globalTransmitSafetyEnabled)
        {
            return new AprsPortTransmitSafetyResult(false, "Global transmit safety is disabled.", port);
        }

        if (!port.Enabled)
        {
            return new AprsPortTransmitSafetyResult(false, "Port is disabled.", port);
        }

        if (!port.TransmitEnabled)
        {
            return new AprsPortTransmitSafetyResult(false, "Port transmit is disabled.", port);
        }

        if (port.ConnectionState != AprsPortConnectionState.Connected)
        {
            return new AprsPortTransmitSafetyResult(false, "Port is not connected.", port);
        }

        if (port.ReceiveEnabled && !port.TransmitEnabled)
        {
            return new AprsPortTransmitSafetyResult(false, "Port is receive-only.", port);
        }

        return new AprsPortTransmitSafetyResult(true, null, port);
    }

    public static AprsPortSnapshot CreateDefaultPort(
        string portId,
        string portName,
        AprsPortType portType,
        string sourceDescription,
        string? configurationReference = null)
    {
        return new AprsPortSnapshot(
            portId,
            portName,
            portType,
            Enabled: false,
            ReceiveEnabled: true,
            TransmitEnabled: false,
            AprsPortConnectionState.Disabled,
            LastConnectedUtc: null,
            LastDisconnectedUtc: null,
            LastPacketReceivedUtc: null,
            LastPacketTransmittedUtc: null,
            PacketCountReceived: 0,
            PacketCountTransmitted: 0,
            LastError: null,
            sourceDescription,
            configurationReference);
    }

    private bool UpdatePort(string portId, Func<AprsPortSnapshot, AprsPortSnapshot> update)
    {
        var normalized = NormalizePortId(portId);
        if (!ports.TryGetValue(normalized, out var port))
        {
            return false;
        }

        ports[normalized] = update(port);
        return true;
    }

    private static string NormalizePortId(string portId)
    {
        return portId.Trim().ToUpperInvariant();
    }

    private static IReadOnlyCollection<AprsPortSnapshot> SortPorts(IEnumerable<AprsPortSnapshot> portValues)
    {
        return portValues
            .OrderBy(port => port.PortName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(port => port.PortId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
