namespace Aprs.Services;

public sealed record GpsdParseResult(
    bool IsParsed,
    GpsdReportType ReportType,
    GpsPosition? Position,
    int? SatelliteCount,
    int? UsedSatelliteCount,
    double? Hdop,
    string RawJson,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static GpsdParseResult Failed(string rawJson, IReadOnlyList<string> errors)
    {
        return new GpsdParseResult(
            IsParsed: false,
            GpsdReportType.Unknown,
            Position: null,
            SatelliteCount: null,
            UsedSatelliteCount: null,
            Hdop: null,
            rawJson,
            errors,
            Warnings: []);
    }
}
