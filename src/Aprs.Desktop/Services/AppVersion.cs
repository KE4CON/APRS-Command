using System.Reflection;

namespace Aprs.Desktop.Services;

/// <summary>
/// Single source of truth for the running application version.
/// Reads from assembly metadata so version strings never go stale
/// when the csproj version is bumped for a release.
/// </summary>
public static class AppVersion
{
    /// <summary>Three-part numeric version, e.g. "0.3.0".</summary>
    public static string Numeric { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Informational version, e.g. "0.3.0-alpha". Falls back to the numeric version.</summary>
    public static string Informational { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? Numeric;

    /// <summary>Standard User-Agent string for all outbound HTTP requests.</summary>
    public static string UserAgent { get; } =
        $"APRSCommand/{Numeric} (github.com/KE4CON/APRS-Command)";

    /// <summary>
    /// AX.25 destination TOCALL — convenience accessor for AprsConstants.ToCall.
    /// </summary>
    public static string ToCall => Aprs.Core.AprsConstants.ToCall;
}
