using System.Globalization;
using Avalonia.Data.Converters;
using Aprs.Desktop.Configuration;

namespace Aprs.Desktop.ViewModels;

/// <summary>
/// Converts a <see cref="ConnectionPortType"/> enum value to its operator-friendly display name
/// for use in the Connections type-picker ComboBox.
/// </summary>
public sealed class ConnectionPortTypeConverter : IValueConverter
{
    public static readonly ConnectionPortTypeConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ConnectionPortType t ? t switch
        {
            ConnectionPortType.AprsIs            => "APRS-IS",
            ConnectionPortType.NetworkTncKiss     => "Network TNC (KISS-TCP)",
            ConnectionPortType.Agwpe              => "AGWPE (TCP)",
            ConnectionPortType.SerialKiss         => "Serial KISS (hardware TNC)",
            ConnectionPortType.ManagedLocalModem  => "Managed local modem (sound card)",
            _                                     => t.ToString()
        } : value?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
