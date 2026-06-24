namespace Aprs.Transport;

public sealed class AprsIsServerManager : IAprsIsServerManager
{
    private readonly Dictionary<string, AprsIsServerDefinition> servers = new(StringComparer.OrdinalIgnoreCase);

    public AprsIsServerManager()
        : this(DateTimeOffset.UtcNow)
    {
    }

    public AprsIsServerManager(DateTimeOffset createdAtUtc)
    {
        ResetToDefaults(createdAtUtc);
    }

    public IReadOnlyList<AprsIsServerDefinition> GetAllServers()
    {
        return servers.Values.OrderBy(server => server.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<AprsIsServerDefinition> GetEnabledServers()
    {
        return servers.Values
            .Where(server => server.IsEnabled)
            .OrderBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public AprsIsServerDefinition GetDefaultServer()
    {
        return servers.Values.First(server => server.IsDefault);
    }

    public AprsIsServerDefinition SetDefaultServer(string name, DateTimeOffset updatedAtUtc)
    {
        var selected = FindServer(name);
        if (!selected.IsEnabled)
        {
            throw new InvalidOperationException("Disabled APRS-IS servers cannot be selected as the default.");
        }

        foreach (var server in servers.Values.ToArray())
        {
            servers[server.Name] = server with
            {
                IsDefault = server.Name.Equals(selected.Name, StringComparison.OrdinalIgnoreCase),
                UpdatedAtUtc = updatedAtUtc
            };
        }

        return servers[selected.Name];
    }

    public AprsIsServerDefinition AddCustomServer(
        string name,
        string hostName,
        int port,
        string description,
        string? region,
        string? notes,
        DateTimeOffset createdAtUtc)
    {
        ValidateServer(name, hostName, port);

        if (servers.ContainsKey(name))
        {
            throw new InvalidOperationException($"An APRS-IS server named '{name}' already exists.");
        }

        EnsureUniqueHostPort(hostName, port, exceptName: null);

        var server = new AprsIsServerDefinition(
            Name: name.Trim(),
            HostName: hostName.Trim(),
            Port: port,
            Description: description.Trim(),
            IsEnabled: true,
            IsDefault: false,
            Region: NormalizeOptional(region),
            Notes: NormalizeOptional(notes),
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: createdAtUtc,
            IsCustom: true);

        servers.Add(server.Name, server);
        return server;
    }

    public AprsIsServerDefinition UpdateServer(
        string name,
        string hostName,
        int port,
        string description,
        bool isEnabled,
        string? region,
        string? notes,
        DateTimeOffset updatedAtUtc)
    {
        var existing = FindServer(name);
        ValidateServer(existing.Name, hostName, port);
        EnsureUniqueHostPort(hostName, port, existing.Name);

        var updated = existing with
        {
            HostName = hostName.Trim(),
            Port = port,
            Description = description.Trim(),
            IsEnabled = isEnabled,
            Region = NormalizeOptional(region),
            Notes = NormalizeOptional(notes),
            UpdatedAtUtc = updatedAtUtc
        };

        if (!updated.IsEnabled && updated.IsDefault)
        {
            updated = updated with { IsDefault = false };
        }

        servers[updated.Name] = updated;
        EnsureDefaultExists(updatedAtUtc);
        return servers[updated.Name];
    }

    public void RemoveCustomServer(string name)
    {
        var existing = FindServer(name);
        if (!existing.IsCustom)
        {
            throw new InvalidOperationException("Built-in APRS-IS servers cannot be removed.");
        }

        servers.Remove(existing.Name);
        EnsureDefaultExists(DateTimeOffset.UtcNow);
    }

    public AprsIsServerDefinition SetServerEnabled(string name, bool isEnabled, DateTimeOffset updatedAtUtc)
    {
        var existing = FindServer(name);
        var updated = existing with
        {
            IsEnabled = isEnabled,
            IsDefault = isEnabled && existing.IsDefault,
            UpdatedAtUtc = updatedAtUtc
        };

        servers[updated.Name] = updated;
        EnsureDefaultExists(updatedAtUtc);
        return servers[updated.Name];
    }

    public void ResetToDefaults(DateTimeOffset updatedAtUtc)
    {
        servers.Clear();

        foreach (var server in CreateDefaultServers(updatedAtUtc))
        {
            servers.Add(server.Name, server);
        }
    }

    private AprsIsServerDefinition FindServer(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Server name is required.", nameof(name));
        }

        if (!servers.TryGetValue(name.Trim(), out var server))
        {
            throw new KeyNotFoundException($"APRS-IS server '{name}' was not found.");
        }

        return server;
    }

    private void EnsureDefaultExists(DateTimeOffset updatedAtUtc)
    {
        if (servers.Values.Any(server => server.IsDefault && server.IsEnabled))
        {
            return;
        }

        var replacement = servers.Values
            .Where(server => server.IsEnabled)
            .OrderBy(server => server.IsCustom)
            .ThenBy(server => server.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (replacement is null)
        {
            return;
        }

        servers[replacement.Name] = replacement with
        {
            IsDefault = true,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    private void EnsureUniqueHostPort(string hostName, int port, string? exceptName)
    {
        var trimmedHostName = hostName.Trim();
        var duplicate = servers.Values.Any(server =>
            !server.Name.Equals(exceptName, StringComparison.OrdinalIgnoreCase)
            && server.Port == port
            && server.HostName.Equals(trimmedHostName, StringComparison.OrdinalIgnoreCase));

        if (duplicate)
        {
            throw new InvalidOperationException("An APRS-IS server with the same host and port already exists.");
        }
    }

    private static void ValidateServer(string name, string hostName, int port)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Server name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(hostName))
        {
            throw new ArgumentException("Server hostname is required.", nameof(hostName));
        }

        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Server port must be between 1 and 65535.");
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static IEnumerable<AprsIsServerDefinition> CreateDefaultServers(DateTimeOffset timestamp)
    {
        yield return CreateDefault("Rotate APRS2", "rotate.aprs2.net", "Round-robin APRS2 server pool.", "Global", true, timestamp);
        yield return CreateDefault("North America APRS2", "noam.aprs2.net", "North America APRS2 server pool.", "North America", false, timestamp);
        yield return CreateDefault("Europe APRS2", "euro.aprs2.net", "Europe APRS2 server pool.", "Europe", false, timestamp);
        yield return CreateDefault("South America APRS2", "soam.aprs2.net", "South America APRS2 server pool.", "South America", false, timestamp);
        yield return CreateDefault("Australia New Zealand APRS2", "aunz.aprs2.net", "Australia and New Zealand APRS2 server pool.", "Australia/New Zealand", false, timestamp);
        yield return CreateDefault("Asia APRS2", "asia.aprs2.net", "Asia APRS2 server pool.", "Asia", false, timestamp);
    }

    private static AprsIsServerDefinition CreateDefault(
        string name,
        string hostName,
        string description,
        string region,
        bool isDefault,
        DateTimeOffset timestamp)
    {
        return new AprsIsServerDefinition(
            Name: name,
            HostName: hostName,
            Port: 14580,
            Description: description,
            IsEnabled: true,
            IsDefault: isDefault,
            Region: region,
            Notes: null,
            CreatedAtUtc: timestamp,
            UpdatedAtUtc: timestamp,
            IsCustom: false);
    }
}
