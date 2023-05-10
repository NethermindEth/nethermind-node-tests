using NethermindNode.Core.Helpers;

namespace NethermindNode.Tests.SyncedNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RestartsOnSyncedNode : BaseTest
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    private int initialDelay = 60;

    public static IEnumerable<TestCaseData> DelayForFuzzerTestCases()
    {
        int restartCount = 10;

        for (int i = 1; i <= restartCount; i++)
        {
            int currentDelay = (int)(60 * Math.Pow(1.5, i));
            yield return new TestCaseData(currentDelay).SetName($"ShouldRestartNodeWith{currentDelay}sDelay");
        }
    }

    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("ArchiveSync")]
    [Category("RestartOnFullSync")]
    [NonParallelizable]
    [TestCaseSource(nameof(DelayForFuzzerTestCases))]
    public void ShouldRestartNethermindClientWithIncreasingDelay(int currentDelay)
    {
        Logger.Info($"Starting test: ShouldRestartNethermindClientWithIncreasingDelay: {currentDelay} Delay");

        NodeInfo.WaitForNodeToBeReady(Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { IsFullySyncedCheck = true, Count = 1, Minimum = currentDelay, Maximum = currentDelay, ShouldForceGracefullCommand = true }, Logger);
    }

    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("ArchiveSync")]
    [Category("RestartOnFullSync")]
    [NonParallelizable]
    [TestCaseSource(nameof(DelayForFuzzerTestCases))]
    public void ShouldRestartConsensusClientWithIncreasingDelay(int currentDelay)
    {
        Logger.Info($"Starting test: ShouldRestartConsensusClientWithIncreasingDelay: {currentDelay} Delay");

        NodeInfo.WaitForNodeToBeReady(Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = "sedge-consensus-client", IsFullySyncedCheck = true, Count = 1, Minimum = currentDelay, Maximum = currentDelay, ShouldForceGracefullCommand = true }, Logger);
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
