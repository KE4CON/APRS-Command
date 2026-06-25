using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.Views;

public sealed partial class SetupWindow : Window
{
    /// <summary>Raised after a valid station profile has been entered and saved.</summary>
    public event Action? SetupCompleted;

    public SetupWindow()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var callsign = CallsignBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(callsign))
        {
            ShowError("Please enter your callsign.");
            return;
        }

        if (!TryParseCoordinate(LatitudeBox.Text, -90, 90, out var latitude))
        {
            ShowError("Latitude must be a number between -90 and 90.");
            return;
        }

        if (!TryParseCoordinate(LongitudeBox.Text, -180, 180, out var longitude))
        {
            ShowError("Longitude must be a number between -180 and 180.");
            return;
        }

        if (!int.TryParse(RadiusBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var radius)
            || radius <= 0)
        {
            ShowError("Range filter must be a whole number of kilometers greater than zero.");
            return;
        }

        var profile = StationProfile.Default with
        {
            Callsign = callsign.ToUpperInvariant(),
            Latitude = latitude,
            Longitude = longitude,
            FilterRadiusKm = radius
        };
        profile.Save();

        SetupCompleted?.Invoke();
    }

    private static bool TryParseCoordinate(string? text, double min, double max, out double value)
    {
        if (double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && value >= min
            && value <= max)
        {
            return true;
        }

        value = 0;
        return false;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
