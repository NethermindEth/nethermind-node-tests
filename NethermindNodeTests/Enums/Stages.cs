using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Enums
{
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
}
