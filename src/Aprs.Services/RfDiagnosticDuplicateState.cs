namespace Aprs.Services;

public enum RfDiagnosticDuplicateState
{
    NotDuplicate,
    PossibleDuplicate,
    ConfirmedDuplicate,
    DuplicateOfEarlierPacket
}
