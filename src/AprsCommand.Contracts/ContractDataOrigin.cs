namespace AprsCommand.Contracts;

public enum ContractDataOrigin
{
    Unknown,
    Received,
    Generated,
    Imported,
    Replayed,
    Simulated,
    Training,
    Manual,
    Plugin,
    LocalApi
}
