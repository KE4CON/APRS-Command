namespace Aprs.Services;

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
