using System.Collections.ObjectModel;
using System.ComponentModel;
using Aprs.Services;

namespace Aprs.Desktop.ViewModels;

/// <summary>A single data point for the weather history chart.</summary>
public sealed record WeatherGraphPoint(string TimeLabel, double? Value);

/// <summary>
/// Provides weather history data for the temperature/humidity/pressure chart
/// in the Weather window. Populated from WeatherDisplayService.GetHistory().
/// </summary>
public sealed class WeatherGraphViewModel : INotifyPropertyChanged
{
    private readonly IWeatherDisplayService weatherService;
    private string selectedStationId = string.Empty;

    public WeatherGraphViewModel(IWeatherDisplayService weatherService)
    {
        this.weatherService = weatherService;
        TemperaturePoints = [];
        HumidityPoints    = [];
        PressurePoints    = [];
        WindSpeedPoints   = [];
    }

    public ObservableCollection<WeatherGraphPoint> TemperaturePoints { get; }
    public ObservableCollection<WeatherGraphPoint> HumidityPoints    { get; }
    public ObservableCollection<WeatherGraphPoint> PressurePoints    { get; }
    public ObservableCollection<WeatherGraphPoint> WindSpeedPoints   { get; }

    public string SelectedStationId
    {
        get => selectedStationId;
        set
        {
            selectedStationId = value;
            Refresh();
            OnPropertyChanged(nameof(SelectedStationId));
        }
    }

    public string ChartTitle { get; private set; } = "Select a station to view history";
    public bool   HasData    { get; private set; }

    public void Refresh()
    {
        TemperaturePoints.Clear();
        HumidityPoints.Clear();
        PressurePoints.Clear();
        WindSpeedPoints.Clear();

        if (string.IsNullOrWhiteSpace(selectedStationId))
        {
            HasData    = false;
            ChartTitle = "Select a station to view history";
            OnPropertyChanged(nameof(ChartTitle));
            OnPropertyChanged(nameof(HasData));
            return;
        }

        var records = weatherService.GetHistory(selectedStationId);
        HasData     = records.Count > 0;
        ChartTitle  = HasData
            ? $"{selectedStationId}  —  last {records.Count} readings"
            : $"{selectedStationId}  —  no history yet";

        foreach (var r in records)
        {
            var label = r.Timestamp.ToLocalTime().ToString("HH:mm");
            TemperaturePoints.Add(new WeatherGraphPoint(label, r.TemperatureFahrenheit));
            HumidityPoints.Add(   new WeatherGraphPoint(label, r.HumidityPercent));
            PressurePoints.Add(   new WeatherGraphPoint(label, r.PressureMillibars));
            WindSpeedPoints.Add(  new WeatherGraphPoint(label, r.WindSpeedMph));
        }

        OnPropertyChanged(nameof(ChartTitle));
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(TemperaturePoints));
        OnPropertyChanged(nameof(HumidityPoints));
        OnPropertyChanged(nameof(PressurePoints));
        OnPropertyChanged(nameof(WindSpeedPoints));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
