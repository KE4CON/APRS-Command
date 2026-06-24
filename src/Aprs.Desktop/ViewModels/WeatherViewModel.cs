using System.Collections.ObjectModel;
using Aprs.Core;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WeatherViewModel
{
    public WeatherViewModel(IWeatherDisplayService weatherService, DateTimeOffset now)
        : this(weatherService, now, WeatherBeaconSettingsViewModel.CreateDesignTime(), WeatherStationSetupViewModel.CreateDesignTime())
    {
    }

    public WeatherViewModel(
        IWeatherDisplayService weatherService,
        DateTimeOffset now,
        WeatherBeaconSettingsViewModel beaconSettings,
        WeatherStationSetupViewModel setup)
    {
        weatherService.UpdateStaleStates(now);
        Rows = new ObservableCollection<WeatherStationRowViewModel>(
            weatherService.GetAllWeatherStations().Select(station => new WeatherStationRowViewModel(station, now)));
        SelectedStation = Rows.FirstOrDefault();
        Summary = $"{Rows.Count} weather stations";
        BeaconSettings = beaconSettings;
        Setup = setup;
    }

    public ObservableCollection<WeatherStationRowViewModel> Rows { get; }

    public WeatherStationRowViewModel? SelectedStation { get; }

    public string Summary { get; }

    public bool HasStations => Rows.Count > 0;

    public WeatherBeaconSettingsViewModel BeaconSettings { get; }

    public WeatherStationSetupViewModel Setup { get; }

    public static WeatherViewModel CreateDesignTime()
    {
        var now = DateTimeOffset.UtcNow;
        var parser = new AprsParser();
        var service = new WeatherDisplayService();
        service.AcceptWeatherPacket(
            (WeatherAprsPacket)parser.Parse("WX9XYZ>APRS,TCPIP*:!3903.50N/08430.50W_180/005g010t072r000p000P000h50b10132Test weather station", now.AddMinutes(-8)),
            AprsPacketSource.Simulation);
        service.UpsertWeatherStation(new WeatherStationDisplayRecord(
            "LOCALWX",
            "Local Weather",
            WeatherStationSourceType.LocalWeatherStation,
            39.0583,
            -84.5083,
            225,
            8,
            15,
            68,
            2,
            12,
            18,
            61,
            1014.6,
            420,
            3.2,
            null,
            "No recent lightning events",
            now.AddMinutes(-25),
            TimeSpan.FromMinutes(25),
            WeatherDataState.Stale,
            "{\"source\":\"demo\"}",
            WeatherStationOrigin.LocalDriver));

        return new WeatherViewModel(service, now);
    }
}
