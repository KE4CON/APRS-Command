namespace Aprs.Services;

public sealed record NmeaParseResult(
    bool IsParsed,
    string SentenceType,
    GpsPosition? Position,
    bool? ChecksumValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public static NmeaParseResult Failed(
        string sentenceType,
        bool? checksumValid,
        IReadOnlyList<string> errors,
        IReadOnlyList<string>? warnings = null)
    {
        return new NmeaParseResult(
            IsParsed: false,
            sentenceType,
            Position: null,
            checksumValid,
            errors,
            warnings ?? []);
    }
}
