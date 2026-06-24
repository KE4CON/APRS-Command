namespace Aprs.Mapping;

public sealed record AprsSymbol(
    char SymbolTableIdentifier,
    char SymbolCode,
    char? Overlay,
    string Description,
    AprsSymbolCategory Category,
    bool IsPrimaryTable,
    bool IsAlternateTable,
    string MarkerIconKey,
    string FallbackDisplayText,
    bool IsKnown);
