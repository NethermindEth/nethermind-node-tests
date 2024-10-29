using NethermindNode.Core.Helpers;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ArchiveTests : BaseTest
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    [Category("ArchiveSync")]
    [Test]
    public void ShouldSyncArchiveTillSpecifiedMillionBlock()
    {
        Logger.Info("***Starting test: ShouldSyncArchiveTillSpecifiedMillionBlock***");

        NodeInfo.WaitForNodeToBeReady(Logger);
        var blockNumber = NodeInfo.GetCurrentBlock(Logger);

        while (blockNumber <= 1000000)
        {
            Logger.Info("Waiting for block 1000000. Current block is: " + blockNumber);
            Thread.Sleep(10000);
        }

        Logger.Info("***Test finished: ShouldSyncArchiveTillSpecifiedMillionBlock***");
    }
}
