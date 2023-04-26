using NethermindNode.Core.Helpers;

namespace NethermindNode.Tests.SyncedNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RestartsOnSyncedNode : BaseTest
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    [TestCase(20, 60, 3600)]
    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("ArchiveSync")]
    [Category("RestartOnFullSync")]
    public void ShouldRestartNodeMultipleTimesOnSyncedNode(int restartCount, int minimumWait, int maximumWait)
    {
        Logger.Info("***Starting test: ShouldRestartNodeMultipleTimesOnSyncedNode***");

        NodeInfo.WaitForNodeToBeReady(Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { IsFullySyncedCheck = true, Count = restartCount, Minimum = minimumWait, Maximum = maximumWait, ShouldForceGracefullCommand = true }, Logger);
    }

    [TestCase(0, 60, 120)]
    [Category("InfinityRestartGracefullyOnFullSync")]
    public void ShouldRestartGracefullyNodeForInfinityOnSyncedNode(int restartCount, int minimumWait, int maximumWait)
    {
        Logger.Info("***Starting test: ShouldRestartGracefullyNodeForInfinityOnSyncedNode***");

        NodeInfo.WaitForNodeToBeReady(Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { IsFullySyncedCheck = true, Count = restartCount, Minimum = minimumWait, Maximum = maximumWait, ShouldForceGracefullCommand = true }, Logger);
    }
}
