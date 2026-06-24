namespace Aprs.Transport;

public enum KissCommandType
{
    DataFrame = 0,
    TxDelay = 1,
    Persistence = 2,
    SlotTime = 3,
    TxTail = 4,
    FullDuplex = 5,
    SetHardware = 6,
    Return = 15,
    Unknown = 255
}
