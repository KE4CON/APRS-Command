using System.Collections.ObjectModel;
using Aprs.Core;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

public sealed class WeatherViewModel
{
    private readonly IWeatherDisplayService weatherService;

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
        this.weatherService = weatherService;
        weatherService.UpdateStaleStates(now);
        Rows = new ObservableCollection<WeatherStationRowViewModel>(
            weatherService.GetAllWeatherStations().Select(station => new WeatherStationRowViewModel(station, now)));
        SelectedStation = Rows.FirstOrDefault();
        Summary = $"{Rows.Count} weather stations";
        BeaconSettings = beaconSettings;
        Setup = setup;
        Graph = new WeatherGraphViewModel(weatherService);
    }

    /// <summary>Feeds a live weather packet into the service and refreshes the display.</summary>
    public void AcceptWeatherPacket(Aprs.Core.WeatherAprsPacket packet, AprsPacketSource source)
    {
        var now = DateTimeOffset.UtcNow;
        weatherService.AcceptWeatherPacket(packet, source);
        weatherService.UpdateStaleStates(now);
        Rows.Clear();
        foreach (var station in weatherService.GetAllWeatherStations())
            Rows.Add(new WeatherStationRowViewModel(station, now));
        Graph.Refresh();
    }

    public ObservableCollection<WeatherStationRowViewModel> Rows { get; }

    public WeatherStationRowViewModel? SelectedStation { get; }

    public string Summary { get; }

    public bool HasStations => Rows.Count > 0;

    public WeatherBeaconSettingsViewModel BeaconSettings { get; private set; }

    public WeatherGraphViewModel Graph { get; }

    public IEnumerable<string> Stations =>
        weatherService.GetAllWeatherStations().Select(s => s.StationId);

    /// <summary>Called post-construction to inject the live weather beacon scheduler.</summary>
    public void SetBeaconScheduler(IWeatherBeaconScheduler scheduler)
    {
        BeaconSettings = new WeatherBeaconSettingsViewModel(scheduler);
    }

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
