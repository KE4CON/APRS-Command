using System.Text.RegularExpressions;

namespace Aprs.Services;

public sealed partial class AprsBeaconFormatter : IAprsBeaconFormatter
{
    public AprsBeaconFormatResult FormatFixedPositionBeacon(AprsBeaconInput input)
    {
        return FormatPositionBeacon(input, positionType: '!');
    }

    public AprsBeaconFormatResult FormatMobilePositionBeacon(AprsBeaconInput input)
    {
        return FormatPositionBeacon(input, positionType: '=');
    }

    public AprsBeaconFormatResult FormatStatusBeacon(
        string sourceStationIdentifier,
        string destination,
        IReadOnlyList<string> path,
        string statusText)
    {
        var errors = new List<string>();
        ValidateStationIdentifier(sourceStationIdentifier, errors);
        ValidateDestination(destination, errors);
        ValidatePath(path, rfPathRequired: false, errors);
        ValidateText(statusText, "Status text", errors);

        if (errors.Count > 0)
        {
            return AprsBeaconFormatResult.Failed(errors);
        }

        var packet = $"{sourceStationIdentifier.Trim().ToUpperInvariant()}>{BuildDestinationAndPath(destination, path)}:>{statusText.Trim()}";
        return AprsBeaconFormatResult.Succeeded(packet);
    }

    public AprsBeaconInput CreateInputFromProfile(LocalStationProfile profile, string destination = "APRS", bool rfPathRequired = false)
    {
        var path = string.IsNullOrWhiteSpace(profile.BeaconPath)
            ? []
            : profile.BeaconPath.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return new AprsBeaconInput(
            profile.FullStationIdentifier,
            destination,
            path,
            profile.FixedLatitude,
            profile.FixedLongitude,
            profile.SymbolTableIdentifier,
            profile.SymbolCode,
            profile.StationComment,
            AltitudeFeet: null,
            CourseDegrees: null,
            SpeedKnots: null,
            profile.PhgData,
            UseTimestamp: false,
            UseCompressedPosition: false,
            rfPathRequired);
    }

    private static AprsBeaconFormatResult FormatPositionBeacon(AprsBeaconInput input, char positionType)
    {
        var errors = ValidatePositionInput(input);
        if (errors.Count > 0)
        {
            return AprsBeaconFormatResult.Failed(errors);
        }

        var body = $"{positionType}{AprsCoordinateFormatter.FormatLatitude(input.Latitude!.Value)}{input.SymbolTableIdentifier!.Value}{AprsCoordinateFormatter.FormatLongitude(input.Longitude!.Value)}{input.SymbolCode!.Value}";
        body += BuildPositionExtension(input);
        var packet = $"{input.SourceStationIdentifier.Trim().ToUpperInvariant()}>{BuildDestinationAndPath(input.Destination, input.Path)}:{body}";

        if (!packet.Contains('>') || !packet.Contains(':'))
        {
            return AprsBeaconFormatResult.Failed(["Generated APRS packet is malformed."]);
        }

        return AprsBeaconFormatResult.Succeeded(packet);
    }

    private static IReadOnlyList<string> ValidatePositionInput(AprsBeaconInput input)
    {
        var errors = new List<string>();
        ValidateStationIdentifier(input.SourceStationIdentifier, errors);
        ValidateDestination(input.Destination, errors);
        ValidatePath(input.Path, input.RfPathRequired, errors);
        ValidateText(input.Comment, "Comment", errors);
        ValidateText(input.PhgData, "PHG data", errors);

        if (input.UseCompressedPosition)
        {
            errors.Add("Compressed position beacon formatting is not implemented yet.");
        }

        if (input.UseTimestamp)
        {
            errors.Add("Timestamped beacon formatting is not implemented yet.");
        }

        if (input.Latitude is null or < -90 or > 90)
        {
            errors.Add("Latitude must be between -90 and 90 degrees.");
        }

        if (input.Longitude is null or < -180 or > 180)
        {
            errors.Add("Longitude must be between -180 and 180 degrees.");
        }

        if (input.SymbolTableIdentifier is null)
        {
            errors.Add("Symbol table identifier is required.");
        }

        if (input.SymbolCode is null)
        {
            errors.Add("Symbol code is required.");
        }

        if (input.CourseDegrees is < 0 or > 360)
        {
            errors.Add("Course must be between 0 and 360 degrees.");
        }

        if (input.SpeedKnots is < 0)
        {
            errors.Add("Speed cannot be negative.");
        }

        if (input.AltitudeFeet is < -99999 or > 999999)
        {
            errors.Add("Altitude is outside the supported APRS /A= range.");
        }

        return errors;
    }

    private static void ValidateStationIdentifier(string sourceStationIdentifier, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(sourceStationIdentifier) || !StationIdentifierRegex().IsMatch(sourceStationIdentifier.Trim()))
        {
            errors.Add("Source station identifier must be a valid callsign with optional SSID.");
        }
    }

    private static void ValidateDestination(string destination, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            errors.Add("Destination is required.");
            return;
        }

        ValidateText(destination, "Destination", errors);
    }

    private static void ValidatePath(IReadOnlyList<string> path, bool rfPathRequired, List<string> errors)
    {
        if (rfPathRequired && path.Count == 0)
        {
            errors.Add("RF path is required.");
        }

        foreach (var component in path)
        {
            if (string.IsNullOrWhiteSpace(component))
            {
                errors.Add("Path components cannot be empty.");
            }

            ValidateText(component, "Path component", errors);
        }
    }

    private static void ValidateText(string? value, string label, List<string> errors)
    {
        if (value is not null && (value.Contains('\r') || value.Contains('\n')))
        {
            errors.Add($"{label} cannot contain line breaks.");
        }
    }

    private static string BuildDestinationAndPath(string destination, IReadOnlyList<string> path)
    {
        var normalizedDestination = destination.Trim().ToUpperInvariant();
        if (path.Count == 0)
        {
            return normalizedDestination;
        }

        return $"{normalizedDestination},{string.Join(',', path.Select(component => component.Trim().ToUpperInvariant()))}";
    }

    private static string BuildPositionExtension(AprsBeaconInput input)
    {
        var extension = string.Empty;
        if (!string.IsNullOrWhiteSpace(input.PhgData))
        {
            extension += input.PhgData.Trim().ToUpperInvariant();
        }

        if (input.CourseDegrees is not null || input.SpeedKnots is not null)
        {
            extension += $"{input.CourseDegrees.GetValueOrDefault():000}/{input.SpeedKnots.GetValueOrDefault():000}";
        }

        if (input.AltitudeFeet is not null)
        {
            extension += $"/A={Math.Max(0, input.AltitudeFeet.Value):000000}";
        }

        if (!string.IsNullOrWhiteSpace(input.Comment))
        {
            extension += input.Comment.Trim();
        }

        return extension;
    }

    [GeneratedRegex("^[A-Z0-9]{1,6}(-([0-9]|1[0-5]))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex StationIdentifierRegex();
}
