namespace Aprs.Mapping;

public interface IAprsSymbolLookupService
{
    /// <summary>
    /// Resolves an APRS symbol table identifier and symbol code into display metadata.
    /// </summary>
    AprsSymbol Resolve(char? symbolTableIdentifier, char? symbolCode);

    /// <summary>
    /// Gets known symbols for future selector UI surfaces.
    /// </summary>
    IReadOnlyCollection<AprsSymbol> GetKnownSymbols();
}
