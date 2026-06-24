namespace Aprs.Services;

public interface IAprsMessageIdGenerator
{
    /// <summary>
    /// Generates a short APRS-friendly message ID.
    /// </summary>
    string NextId();
}
