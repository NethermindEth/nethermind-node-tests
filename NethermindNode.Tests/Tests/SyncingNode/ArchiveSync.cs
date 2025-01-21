using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ArchiveTests : BaseTest
{
    [NethermindTestCase(1000000), Category("ArchiveMilionBlocks")]
    public void ShouldSyncArchiveTillSpecifiedBlock(int blocksCount)
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        var blockNumber = NodeInfo.GetCurrentBlock(TestLoggerContext.Logger);
        List<string> errors = new List<string>();
        bool verificationSuceeded;

        while (blockNumber <= blocksCount)
        {
            verificationSuceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSuceeded == true, "Undesired log occurred: " + string.Join(", ", errors));
            TestLoggerContext.Logger.Info($"Waiting for block {blocksCount}. Current block is: " + blockNumber);
            Thread.Sleep(10000);
        }

        TestLoggerContext.Logger.Info($"Block {blocksCount} reached.");
    }
}
