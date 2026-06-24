namespace AprsCommand.Contracts;

public enum ExtensionPermission
{
    ReadOnly,
    SubmitLocalData,
    CreateLocalObjects,
    QueuePackets,
    TransmitAprsIs,
    TransmitRf,
    Admin
}
