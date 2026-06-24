# Alert Rule Provider Stub

This is documentation-only pseudocode for a future alert-rule provider.

```csharp
public sealed class ExampleAlertRuleProvider
{
    public string PluginId => "example-alert-rule-provider";
    public string[] RequestedPermissions => ["ReadOnly"];

    public AlertDto CreatePreviewAlert()
    {
        return new AlertDto
        {
            AlertId = "alert-sim-001",
            AlertType = "StationEnteredArea",
            Severity = "Warning",
            Summary = "SIM001 entered training area"
        };
    }
}
```
