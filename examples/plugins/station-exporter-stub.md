# Station Data Exporter Stub

This is documentation-only pseudocode for a read-only exporter.

```csharp
public sealed class ExampleStationExporter
{
    public string PluginId => "example-station-exporter";
    public string[] RequestedPermissions => ["ReadOnly"];

    public Task ExportAsync(IReadOnlyList<StationUpdateDto> stations, CancellationToken cancellationToken)
    {
        // Write DTOs to a local file or dashboard.
        // Do not mutate internal station state.
        // Do not transmit.
        return Task.CompletedTask;
    }
}
```
