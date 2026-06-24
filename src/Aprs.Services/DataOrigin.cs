namespace Aprs.Services;

public enum DataOrigin
{
    Unknown,
    Received,
    Generated,
    Imported,
    Replayed,
    Simulated,
    Training,
    Manual,
    LocalApi,
    Plugin
}
