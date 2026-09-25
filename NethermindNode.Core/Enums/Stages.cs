namespace NethermindNode.Tests.Enums;

public enum Stages
{
    UpdatingPivot,
    FastHeaders,
    BeaconHeaders,
    FastSync,
    SnapSync,
    StateNodes,
    FastBodies,
    FastReceipts,
    Disconnected,
    WaitingForBlock,
    None
}
