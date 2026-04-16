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

        int iteration = 0;
        var blockNumber = NodeInfo.GetCurrentBlock(TestLoggerContext.Logger);
        while (blockNumber <= blocksCount)
        {
            ForceStopWatcher.ThrowIfStopRequested();

            bool verificationSucceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSucceeded == true, "Undesired log occurred: " + string.Join(", ", errors));

            if (iteration == 0 || iteration % 30 == 0)
            {
                double pct = blocksCount > 0 ? (double)blockNumber / blocksCount * 100.0 : 0;
                TestLoggerContext.Logger.Info($"[ARCHIVE] Waiting for block {blocksCount:N0} \u2014 current: {blockNumber:N0} ({pct:F1}%)");
            }
            iteration++;
            Thread.Sleep(10000);
            blockNumber = NodeInfo.GetCurrentBlock(TestLoggerContext.Logger);
        }

        TestLoggerContext.Logger.Info($"[ARCHIVE] \u2713 Block {blocksCount:N0} reached");
    }
}
