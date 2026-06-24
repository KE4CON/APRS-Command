namespace Aprs.Transport;

public interface IAprsIsServerManager
{
    /// <summary>
    /// Gets every known APRS-IS server, including disabled entries.
    /// </summary>
    IReadOnlyList<AprsIsServerDefinition> GetAllServers();

    /// <summary>
    /// Gets the enabled APRS-IS servers that can be offered for connection.
    /// </summary>
    IReadOnlyList<AprsIsServerDefinition> GetEnabledServers();

    /// <summary>
    /// Gets the current default APRS-IS server.
    /// </summary>
    AprsIsServerDefinition GetDefaultServer();

    /// <summary>
    /// Sets the default APRS-IS server by server name.
    /// </summary>
    AprsIsServerDefinition SetDefaultServer(string name, DateTimeOffset updatedAtUtc);

    /// <summary>
    /// Adds a custom APRS-IS server entry.
    /// </summary>
    AprsIsServerDefinition AddCustomServer(
        string name,
        string hostName,
        int port,
        string description,
        string? region,
        string? notes,
        DateTimeOffset createdAtUtc);

    /// <summary>
    /// Updates an existing APRS-IS server entry without changing its name.
    /// </summary>
    AprsIsServerDefinition UpdateServer(
        string name,
        string hostName,
        int port,
        string description,
        bool isEnabled,
        string? region,
        string? notes,
        DateTimeOffset updatedAtUtc);

    /// <summary>
    /// Removes a custom APRS-IS server entry.
    /// </summary>
    void RemoveCustomServer(string name);

    /// <summary>
    /// Enables or disables an APRS-IS server entry.
    /// </summary>
    AprsIsServerDefinition SetServerEnabled(string name, bool isEnabled, DateTimeOffset updatedAtUtc);

    /// <summary>
    /// Restores the built-in APRS-IS server list.
    /// </summary>
    void ResetToDefaults(DateTimeOffset updatedAtUtc);
}
