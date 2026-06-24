using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class TempestUdpWeatherInputDriverTests
{
    private static readonly DateTimeOffset ReceivedAt = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultConfiguration_IsSafeAndUsesTempestUdpPort()
    {
        var configuration = TempestUdpConfiguration.Default;

        Assert.False(configuration.Enabled);
        Assert.Equal(50222, configuration.ListenPort);
        Assert.Equal("0.0.0.0", configuration.BindAddress);
        Assert.Equal("WeatherFlow Tempest UDP", configuration.SourceName);
        Assert.Contains("Receive only", configuration.Notes);
    }

    [Fact]
    public void DefaultDriver_IsDisabled()
    {
        var driver = new TempestUdpWeatherInputDriver(TempestUdpConfiguration.Default);

        Assert.False(driver.Enabled);
        Assert.Equal(WeatherInputDriverStatus.Disabled, driver.Status);
        Assert.Equal(WeatherInputDriverType.UdpNetwork, driver.DriverType);
    }

    [Fact]
    public void ObsStJson_ParsesIntoCommonWeatherObservation()
    {
        var parser = new TempestUdpJsonParser();

        var result = parser.Parse(ObsStJson, EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal("obs_st", result.MessageType);
        Assert.NotNull(result.Observation);
        var observation = result.Observation;
        Assert.Equal("WeatherFlow Tempest UDP", observation.SourceName);
        Assert.Equal(WeatherObservationSourceType.WeatherFlowTempest, observation.SourceType);
        Assert.Equal("ST-00000512", observation.StationDeviceId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_493_322_445), observation.TimestampUtc);
        Assert.Equal("HB-00000001", observation.Diagnostics["hub_sn"]);
        Assert.Equal(ObsStJson, observation.RawSourcePayload);
    }

    [Fact]
    public void ObsStJson_MapsWeatherFieldsAndConvertsUnits()
    {
        var parser = new TempestUdpJsonParser();

        var observation = parser.Parse(ObsStJson, EnabledConfiguration(), ReceivedAt).Observation!;

        Assert.Equal(187, observation.WindDirectionDegrees);
        Assert.Equal(7.74, observation.WindSpeedMph!.Value, 2);
        Assert.Equal(9.51, observation.WindGustMph!.Value, 2);
        Assert.Equal(72.27, observation.TemperatureFahrenheit!.Value, 2);
        Assert.Equal(53, observation.HumidityPercent);
        Assert.Equal(1017.57, observation.BarometricPressureMillibars);
        Assert.Equal(0.47, observation.RainLastHourInches!.Value, 2);
        Assert.Equal(0, observation.UvIndex);
        Assert.Equal(0, observation.LuminosityWattsPerSquareMeter);
        Assert.Equal(0, observation.LightningCount);
        Assert.Equal(0, observation.LightningDistanceMiles);
    }

    [Fact]
    public void RapidWindJson_IsHandledSafely()
    {
        var parser = new TempestUdpJsonParser();

        var result = parser.Parse(RapidWindJson, EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal("rapid_wind", result.MessageType);
        Assert.NotNull(result.Observation);
        var observation = result.Observation;
        Assert.Equal(128, observation.WindDirectionDegrees);
        Assert.Equal(5.14, observation.WindSpeedMph!.Value, 2);
        Assert.Null(observation.TemperatureFahrenheit);
    }

    [Theory]
    [InlineData("""{"serial_number":"ST-00000512","type":"evt_precip","hub_sn":"HB-00000001","evt":[1493322445]}""", "evt_precip")]
    [InlineData("""{"serial_number":"ST-00000512","type":"evt_strike","hub_sn":"HB-00000001","evt":[1493322445,12,400]}""", "evt_strike")]
    [InlineData("""{"serial_number":"ST-00000512","type":"device_status","hub_sn":"HB-00000001","timestamp":1493322445,"uptime":12345}""", "device_status")]
    [InlineData("""{"serial_number":"HB-00000001","type":"hub_status","firmware_revision":"123","uptime":54321}""", "hub_status")]
    public void EventAndStatusJson_IsHandledAsDiagnostics(string json, string expectedType)
    {
        var parser = new TempestUdpJsonParser();

        var result = parser.Parse(json, EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal(expectedType, result.MessageType);
        Assert.Null(result.Observation);
        Assert.Equal(expectedType, result.Diagnostics["message_type"]);
    }

    [Fact]
    public void MalformedJson_DoesNotCrash()
    {
        var parser = new TempestUdpJsonParser();

        var result = parser.Parse("{not json", EnabledConfiguration(), ReceivedAt);

        Assert.False(result.IsHandled);
        Assert.Null(result.Observation);
        Assert.Contains("valid JSON", result.Error);
    }

    [Fact]
    public void UnknownMessageType_IsIgnoredSafely()
    {
        var parser = new TempestUdpJsonParser();

        var result = parser.Parse("""{"serial_number":"ST-00000512","type":"future_type","hub_sn":"HB-00000001"}""", EnabledConfiguration(), ReceivedAt);

        Assert.True(result.IsHandled);
        Assert.Equal("future_type", result.MessageType);
        Assert.Null(result.Observation);
        Assert.Equal("ST-00000512", result.Diagnostics["serial_number"]);
    }

    [Fact]
    public void DriverPublishesObsStThroughWeatherDriverFramework()
    {
        var displayService = new WeatherDisplayService();
        var manager = new WeatherInputDriverManager(displayService);
        var driver = new TempestUdpWeatherInputDriver(EnabledConfiguration());
        manager.RegisterDriver(driver);

        driver.ProcessPayload(ObsStJson, ReceivedAt);

        var snapshot = manager.GetDriver(driver.DriverId);
        Assert.NotNull(snapshot);
        Assert.True(snapshot.LastValidationResult.IsValid);
        Assert.NotNull(snapshot.LastObservation);
        Assert.Equal("ST-00000512", snapshot.LastObservation.StationDeviceId);

        var displayRecord = displayService.GetWeatherStation("ST-00000512");
        Assert.NotNull(displayRecord);
        Assert.Equal(WeatherStationSourceType.Tempest, displayRecord.SourceType);
        Assert.Equal(WeatherStationOrigin.LocalDriver, displayRecord.Origin);
        Assert.Equal(72, displayRecord.TemperatureFahrenheit);
        Assert.Equal(8, displayRecord.WindSpeedMph);
        Assert.Equal(10, displayRecord.WindGustMph);
        Assert.Equal(47, displayRecord.RainLastHourHundredthsInch);
        Assert.Equal(ObsStJson, displayRecord.RawPayload);
    }

    [Fact]
    public void DriverRecordsMalformedJsonAsLastErrorWithoutThrowing()
    {
        var driver = new TempestUdpWeatherInputDriver(EnabledConfiguration());

        driver.ProcessPayload("{not json", ReceivedAt);

        Assert.Equal(WeatherInputDriverStatus.Faulted, driver.Status);
        Assert.NotNull(driver.LastError);
        Assert.False(driver.LastValidationResult.IsValid);
    }

    private static TempestUdpConfiguration EnabledConfiguration()
    {
        return TempestUdpConfiguration.Default with { Enabled = true };
    }

    private const string ObsStJson =
        """{"serial_number":"ST-00000512","type":"obs_st","hub_sn":"HB-00000001","obs":[[1493322445,0.18,3.46,4.25,187,3,1017.57,22.37,53,760,0.0,0.0,12.0,0,0.0,0,2.410,1]]}""";

    private const string RapidWindJson =
        """{"serial_number":"ST-00000512","type":"rapid_wind","hub_sn":"HB-00000001","ob":[1493322446,2.3,128]}""";
}
