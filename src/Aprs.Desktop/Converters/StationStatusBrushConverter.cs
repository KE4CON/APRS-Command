using System;
using System.Globalization;
using Aprs.Services;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Aprs.Desktop.Converters;

/// <summary>
/// Maps a station's lifecycle state to a status-dot color: green when it's actively being heard, amber
/// when it's gone quiet (stale), and a muted grey once it's old/expired. Theme-agnostic vivid colors so
/// the dot reads on both the light and dark surfaces.
/// </summary>
public sealed class StationStatusBrushConverter : IValueConverter
{
    public static readonly StationStatusBrushConverter Instance = new();

    private static readonly IBrush Active = new SolidColorBrush(Color.Parse("#22C55E"));
    private static readonly IBrush Stale = new SolidColorBrush(Color.Parse("#F59E0B"));
    private static readonly IBrush Old = new SolidColorBrush(Color.Parse("#7C8AA0"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            StationLifecycleState.Active => Active,
            StationLifecycleState.Stale => Stale,
            _ => Old,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
