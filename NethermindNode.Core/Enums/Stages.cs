namespace NethermindNode.Tests.Enums;

public enum Stages
{
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
