namespace Aprs.Services;

public static class WeatherUnitConverter
{
    public static double CelsiusToFahrenheit(double celsius)
    {
        return celsius * 9 / 5 + 32;
    }

    public static double FahrenheitToCelsius(double fahrenheit)
    {
        return (fahrenheit - 32) * 5 / 9;
    }

    public static double KilometersPerHourToMilesPerHour(double kilometersPerHour)
    {
        return kilometersPerHour * 0.6213711922;
    }

    public static double KnotsToMilesPerHour(double knots)
    {
        return knots * 1.150779448;
    }

    public static double MetersPerSecondToMilesPerHour(double metersPerSecond)
    {
        return metersPerSecond * 2.2369362921;
    }

    public static double MillimetersToInches(double millimeters)
    {
        return millimeters / 25.4;
    }

    public static double InchesOfMercuryToMillibars(double inchesOfMercury)
    {
        return inchesOfMercury * 33.8638866667;
    }

    public static double HectopascalsToMillibars(double hectopascals)
    {
        return hectopascals;
    }
}
