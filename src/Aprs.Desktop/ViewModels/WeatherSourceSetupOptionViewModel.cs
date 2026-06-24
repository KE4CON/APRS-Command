namespace Aprs.Desktop.ViewModels;

public sealed class WeatherSourceSetupOptionViewModel
{
    public WeatherSourceSetupOptionViewModel(string key, string displayName, string category)
    {
        Key = key;
        DisplayName = displayName;
        Category = category;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string Category { get; }

    public override string ToString()
    {
        return DisplayName;
    }
}
