using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ArchiveTests : BaseTest
{
    [Category("ArchiveSync")]
    [NethermindTest]
    public void ShouldSyncArchiveTillSpecifiedMillionBlock()
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        var blockNumber = NodeInfo.GetCurrentBlock(TestLoggerContext.Logger);

        while (blockNumber <= 1000000)
        {
            TestLoggerContext.Logger.Info("Waiting for block 1000000. Current block is: " + blockNumber);
            Thread.Sleep(10000);
        }
    }
}
