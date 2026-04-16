using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NethermindNode.Tests.Helpers;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ArchiveTests : BaseTest
{
    [NethermindTestCase(1000000), Category("ArchiveMilionBlocks")]
    public void ShouldSyncArchiveTillSpecifiedBlock(int blocksCount)
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        List<string> errors = new List<string>();

        var blockNumber = NodeInfo.GetCurrentBlock(TestLoggerContext.Logger);
        while (blockNumber <= blocksCount)
        {
            ForceStopWatcher.ThrowIfStopRequested();

            bool verificationSucceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSucceeded == true, "Undesired log occurred: " + string.Join(", ", errors));

            TestLoggerContext.Logger.Info($"Waiting for block {blocksCount}. Current block is: {blockNumber}");
            Thread.Sleep(10000);
            blockNumber = NodeInfo.GetCurrentBlock(TestLoggerContext.Logger);
        }

        TestLoggerContext.Logger.Info($"Block {blocksCount} reached.");
    }
}
