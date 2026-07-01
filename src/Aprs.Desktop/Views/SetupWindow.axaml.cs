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
        MilesRadio.IsCheckedChanged += (_, _) => UpdateRadiusLabel();
        KmRadio.IsCheckedChanged    += (_, _) => UpdateRadiusLabel();
    }

    private void UpdateRadiusLabel()
    {
        var useMiles = MilesRadio.IsChecked == true;
        RadiusLabel.Text = useMiles ? "Receive filter radius (miles)" : "Receive filter radius (km)";
        // Convert the current value to the newly selected unit.
        if (int.TryParse(RadiusBox.Text?.Trim(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var current) && current > 0)
        {
            RadiusBox.Text = useMiles
                ? ((int)Math.Round(current * 0.621371)).ToString()
                : ((int)Math.Round(current / 0.621371)).ToString();
        }
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

        var unit = (MilesRadio.IsChecked == true) ? DistanceUnit.Miles : DistanceUnit.Kilometres;
        var unitLabel = unit == DistanceUnit.Miles ? "miles" : "kilometres";

        if (!int.TryParse(RadiusBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var radiusDisplay)
            || radiusDisplay <= 0)
        {
            ShowError($"Receive filter radius must be a whole number of {unitLabel} greater than zero.");
            return;
        }

        // Convert to km for storage — APRS-IS filter always requires km internally.
        var radiusKm = unit == DistanceUnit.Miles
            ? (int)Math.Round(radiusDisplay / 0.621371)
            : radiusDisplay;

        var profile = StationProfile.Default with
        {
            Callsign       = callsign.ToUpperInvariant(),
            Latitude       = latitude,
            Longitude      = longitude,
            FilterRadiusKm = Math.Max(1, radiusKm),
            DistanceUnit   = unit
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
