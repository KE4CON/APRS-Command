using Aprs.Core;
using Aprs.Desktop.ViewModels;
using Aprs.Mapping;
using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class WeatherDisplayServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AcceptWeatherPacket_CreatesWeatherStationDisplayRecord()
    {
        var service = new WeatherDisplayService();

        var record = service.AcceptWeatherPacket(ParseWeather("WX9XYZ>APRS,TCPIP*:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", Now), AprsPacketSource.AprsIs);

        Assert.NotNull(record);
        Assert.Equal("WX9XYZ", record.StationId);
        Assert.Equal(WeatherStationSourceType.AprsWeatherStation, record.SourceType);
        Assert.Equal(39.058333, record.Latitude!.Value, 6);
        Assert.Equal(-84.508333, record.Longitude!.Value, 6);
        Assert.Equal(180, record.WindDirectionDegrees);
        Assert.Equal(5, record.WindSpeedMph);
        Assert.Equal(10, record.WindGustMph);
        Assert.Equal(72, record.TemperatureFahrenheit);
        Assert.Equal(50, record.HumidityPercent);
        Assert.Equal(1013.2, record.BarometricPressureMillibars);
        Assert.Equal(WeatherStationOrigin.AprsIs, record.Origin);
        Assert.Equal("WX9XYZ>APRS,TCPIP*:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", record.RawPayload);
    }

    [Fact]
    public void LaterWeatherPacket_UpdatesSameStationAndPreservesPosition()
    {
        var service = new WeatherDisplayService();
        service.AcceptWeatherPacket(ParseWeather("WX9XYZ>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", Now), AprsPacketSource.Rf);

        service.AcceptWeatherPacket(ParseWeather("WX9XYZ>APRS:_111111c225s008g015t068r002p012P018h61b10146", Now.AddMinutes(5)), AprsPacketSource.TcpKiss);

        var record = service.GetWeatherStation("wx9xyz");
        Assert.NotNull(record);
        Assert.Equal(39.058333, record.Latitude!.Value, 6);
        Assert.Equal(-84.508333, record.Longitude!.Value, 6);
        Assert.Equal(225, record.WindDirectionDegrees);
        Assert.Equal(8, record.WindSpeedMph);
        Assert.Equal(15, record.WindGustMph);
        Assert.Equal(68, record.TemperatureFahrenheit);
        Assert.Equal(WeatherStationOrigin.Rf, record.Origin);
        Assert.Equal(Now.AddMinutes(5), record.LastUpdateUtc);
        Assert.Single(service.GetAllWeatherStations());
    }

    [Fact]
    public void UpdateStaleStates_MarksStaleAndExpiredWeatherData()
    {
        var service = new WeatherDisplayService();
        service.AcceptWeatherPacket(ParseWeather("STALE>APRS:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", Now), AprsPacketSource.Simulation);
        service.AcceptWeatherPacket(ParseWeather("OLDWX>APRS:!3904.50N/08431.50W_180/005g010t072r000p000P000h50b10132", Now.AddMinutes(-125)), AprsPacketSource.Simulation);

        service.UpdateStaleStates(Now.AddMinutes(16));

        var stale = service.GetWeatherStation("STALE");
        var expired = service.GetWeatherStation("OLDWX");
        Assert.NotNull(stale);
        Assert.NotNull(expired);
        Assert.Equal(WeatherDataState.Stale, stale.DataState);
        Assert.Equal(WeatherDataState.Expired, expired.DataState);
        Assert.Contains(service.GetStaleWeatherStations(), station => station.StationId == "STALE");
        Assert.DoesNotContain(service.GetCurrentWeatherStations(), station => station.StationId == "STALE");
    }

    [Fact]
    public void MissingOptionalWeatherFields_DoNotCrashDisplayLogic()
    {
        var record = new WeatherStationDisplayRecord(
            "LOCALWX",
            "Local Weather",
            WeatherStationSourceType.LocalWeatherStation,
            Latitude: null,
            Longitude: null,
            WindDirectionDegrees: null,
            WindSpeedMph: null,
            WindGustMph: null,
            TemperatureFahrenheit: null,
            RainLastHourHundredthsInch: null,
            RainLast24HoursHundredthsInch: null,
            RainSinceMidnightHundredthsInch: null,
            HumidityPercent: null,
            BarometricPressureMillibars: null,
            LuminosityWattsPerSquareMeter: null,
            UvIndex: null,
            SnowHundredthsInch: null,
            LightningEventInformation: null,
            Now,
            TimeSpan.Zero,
            WeatherDataState.Current,
            RawPayload: null,
            WeatherStationOrigin.LocalDriver);

        var row = new WeatherStationRowViewModel(record, Now.AddMinutes(1));

        Assert.Equal("-", row.Coordinates);
        Assert.Equal("-", row.Temperature);
        Assert.Equal("-", row.Wind);
        Assert.Equal("-", row.Rain);
        Assert.Equal("-", row.RawPayload);
    }

    [Fact]
    public void WeatherStationMarker_CanRepresentPositionedWeatherStation()
    {
        var record = serviceRecord("WX9XYZ", 39.058333, -84.508333);

        var created = WeatherStationMarker.TryCreate(record, out var marker);

        Assert.True(created);
        Assert.NotNull(marker);
        Assert.Equal("WX9XYZ", marker.StationId);
        Assert.Equal(39.058333, marker.Latitude);
        Assert.Equal(-84.508333, marker.Longitude);
        Assert.Equal(WeatherDataState.Current, marker.DataState);
    }

    [Fact]
    public void WeatherStationMarker_RejectsStationWithoutPosition()
    {
        var record = serviceRecord("WX9XYZ", latitude: null, longitude: null);

        var created = WeatherStationMarker.TryCreate(record, out var marker);

        Assert.False(created);
        Assert.Null(marker);
    }

    [Fact]
    public void WeatherViewModel_LoadsServiceBackedWeatherStations()
    {
        var service = new WeatherDisplayService();
        service.AcceptWeatherPacket(ParseWeather("WX9XYZ>APRS,TCPIP*:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132", Now), AprsPacketSource.AprsIs);

        var viewModel = new WeatherViewModel(service, Now.AddMinutes(2));

        var row = Assert.Single(viewModel.Rows);
        Assert.Equal("WX9XYZ", row.StationId);
        Assert.Equal("72 F", row.Temperature);
        Assert.Equal("180 deg / 5 mph / gust 10 mph", row.Wind);
        Assert.Equal("1h 0.00 in, 24h 0.00 in, mid 0.00 in", row.Rain);
        Assert.Equal("AprsWeatherStation", row.SourceType);
        Assert.Equal("AprsIs", row.Origin);
        Assert.Contains("WX9XYZ>APRS", row.RawPayload);
    }

    [Fact]
    public void MapViewModel_LoadsWeatherMarkers()
    {
        var viewModel = new MapViewModel([]);
        viewModel.LoadWeatherStations([serviceRecord("WX9XYZ", 39.058333, -84.508333)]);

        var marker = Assert.Single(viewModel.WeatherMarkers);
        Assert.Equal("WX9XYZ", marker.StationId);
        Assert.Equal(1, viewModel.WeatherMarkerCount);
        Assert.Equal(1, viewModel.TotalMarkerCount);
    }

    private static WeatherAprsPacket ParseWeather(string rawLine, DateTimeOffset timestamp)
    {
        return Assert.IsType<WeatherAprsPacket>(new AprsParser().Parse(rawLine, timestamp));
    }

    private static WeatherStationDisplayRecord serviceRecord(string stationId, double? latitude, double? longitude)
    {
        return new WeatherStationDisplayRecord(
            stationId,
            stationId,
            WeatherStationSourceType.AprsWeatherStation,
            latitude,
            longitude,
            180,
            5,
            10,
            72,
            0,
            0,
            0,
            50,
            1013.2,
            null,
            null,
            null,
            null,
            Now,
            TimeSpan.Zero,
            WeatherDataState.Current,
            "raw",
            WeatherStationOrigin.Simulation);
    }
}
