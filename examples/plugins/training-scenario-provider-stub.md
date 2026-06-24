# Training Scenario Provider Stub

This is documentation-only pseudocode for future training content.

```csharp
public sealed class ExampleTrainingScenarioProvider
{
    public string PluginId => "example-training-provider";

    public TrainingScenarioDto CreateScenario()
    {
        return new TrainingScenarioDto
        {
            ScenarioId = "scenario-sim-001",
            Name = "Simulated Net Startup",
            SourceMetadata = new ExternalSourceMetadata(
                SourceName: "Example Training Provider",
                SourceType: ExternalSourceType.Training,
                SourceId: "example-training-provider",
                Origin: ContractDataOrigin.Training,
                TrustLevel: ExternalTrustLevel.External)
        };
    }
}
```
