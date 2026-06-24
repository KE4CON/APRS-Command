namespace Aprs.Services;

public sealed record SimulationConfiguration
{
    public bool SimulationEnabled { get; init; }

    public string SimulationSourceName { get; init; } = "APRS Simulation";

    public int FixedStationCount { get; init; } = 3;

    public int MobileStationCount { get; init; } = 2;

    public int WeatherStationCount { get; init; } = 1;

    public int ObjectCount { get; init; } = 1;

    public bool GenerateMessages { get; init; } = true;

    public bool GenerateBulletins { get; init; } = true;

    public TimeSpan UpdateInterval { get; init; } = TimeSpan.FromSeconds(30);

    public bool MovementEnabled { get; init; } = true;

    public double MaximumSimulatedSpeedKnots { get; init; } = 45;

    public double AreaCenterLatitude { get; init; } = 39.05833;

    public double AreaCenterLongitude { get; init; } = -84.50833;

    public double AreaRadiusMeters { get; init; } = 10000;

    public bool LoopRandomize { get; init; } = true;

    public bool TransmitDisabled => true;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public static SimulationConfiguration Default { get; } = new();
}
