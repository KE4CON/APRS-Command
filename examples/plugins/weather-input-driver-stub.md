# Weather Input Driver Stub

This is documentation-only pseudocode for a future plugin/driver.

```csharp
public sealed class ExampleWeatherInputDriver
{
    public string DriverId => "example-weather-driver";
    public string DriverName => "Example Weather Driver";
    public bool Enabled => false;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Read local test data, normalize it, attach source metadata, and publish through approved services.
        // Do not transmit APRS weather packets here.
        return Task.CompletedTask;
    }
}
```
