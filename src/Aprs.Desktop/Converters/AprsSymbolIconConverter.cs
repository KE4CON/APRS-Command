using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Aprs.Mapping;

namespace Aprs.Desktop.Converters;

/// <summary>
/// Turns an <see cref="AprsSymbol"/> into a small bitmap of its icon by cropping the embedded
/// APRS symbol sheets — the same 16-column, 64px-per-cell sheets the map markers use. Used by the
/// object symbol picker so each choice shows its real map icon alongside the letter designation.
/// Returns null when the symbol has no drawable cell (the picker then shows just the letter).
/// </summary>
public sealed class AprsSymbolIconConverter : IValueConverter
{
    private const int CellSize = 64;
    private const int Columns = 16;

    // Primary table ('/') and alternate/secondary table ('\') sheets, loaded once.
    private static readonly Bitmap? PrimarySheet = LoadSheet("Aprs.Desktop.aprs-symbols-64-0.png");
    private static readonly Bitmap? SecondarySheet = LoadSheet("Aprs.Desktop.aprs-symbols-64-1.png");

    private static Bitmap? LoadSheet(string manifestName)
    {
        try
        {
            var asm = typeof(AprsSymbolIconConverter).Assembly;
            using var stream = asm.GetManifestResourceStream(manifestName);
            return stream is null ? null : new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not AprsSymbol symbol)
        {
            return null;
        }

        var sheet = symbol.IsPrimaryTable ? PrimarySheet : SecondarySheet;
        if (sheet is null)
        {
            return null;
        }

        var index = symbol.SymbolCode - '!'; // sheets are indexed from 0x21 ('!')
        if (index < 0)
        {
            return null;
        }

        var rect = new PixelRect((index % Columns) * CellSize, (index / Columns) * CellSize, CellSize, CellSize);
        if (rect.Right > sheet.PixelSize.Width || rect.Bottom > sheet.PixelSize.Height)
        {
            return null;
        }

        try
        {
            return new CroppedBitmap(sheet, rect);
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
