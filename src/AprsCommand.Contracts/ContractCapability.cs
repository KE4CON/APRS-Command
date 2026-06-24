namespace AprsCommand.Contracts;

public enum ContractCapability
{
    ReadData,
    SubmitLocalData,
    CreateLocalObjects,
    QueuePackets,
    ReceiveEvents,
    ExportData,
    ImportData,
    RequestAprsIsTransmit,
    RequestRfTransmit,
    Admin
}
