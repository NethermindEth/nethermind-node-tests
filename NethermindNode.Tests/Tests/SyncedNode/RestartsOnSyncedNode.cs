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
        int restartCount = 6; //Reducing slightly to fit more tests

        for (int i = 1; i <= restartCount; i++)
        {
            int currentDelay = (int)(60 * Math.Pow(1.5, i));
            yield return new TestCaseData(currentDelay);
        }
    }

    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("ArchiveSync")]
    [Category("RestartOnFullSync")]
    [NonParallelizable]
    [TestCaseSource(nameof(DelayForFuzzerTestCases))]
    [Order(0)]
    public void ShouldRestartNethermindClientWithIncreasingDelay(int currentDelay)
    {
        Logger.Info($"***Starting test: ShouldRestartNethermindClientWithIncreasingDelay: {currentDelay} Delay***");

        NodeInfo.WaitForNodeToBeReady(Logger);
        NodeInfo.WaitForNodeToBeSynced(Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = 1, Minimum = currentDelay, Maximum = currentDelay, ShouldForceGracefullCommand = true }, Logger);
    }

    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("ArchiveSync")]
    [Category("RestartOnFullSync")]
    [NonParallelizable]
    [TestCaseSource(nameof(DelayForFuzzerTestCases))]
    [Order(1)]
    public void ShouldRestartConsensusClientWithIncreasingDelay(int currentDelay)
    {
        Logger.Info($"***Starting test: ShouldRestartConsensusClientWithIncreasingDelay: {currentDelay} Delay***");

        NodeInfo.WaitForNodeToBeReady(Logger);
        NodeInfo.WaitForNodeToBeSynced(Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["consensus-container-name"], Count = 1, Minimum = currentDelay, Maximum = currentDelay, ShouldForceGracefullCommand = true }, Logger);
    }

    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("ArchiveSync")]
    [Category("RestartOnFullSync")]
    [NonParallelizable]
    [TestCaseSource(nameof(DelayForFuzzerTestCases))]
    [Order(0)]
    public void ShouldKillNethermindClientWithIncreasingDelay(int currentDelay)
    {
        Logger.Info($"***Starting test: ShouldKillNethermindClientWithIncreasingDelay: {currentDelay} Delay***");

        NodeInfo.WaitForNodeToBeReady(Logger);
        NodeInfo.WaitForNodeToBeSynced(Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = 1, Minimum = currentDelay, Maximum = currentDelay, ShouldForceKillCommand = true }, Logger);
    }

    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("ArchiveSync")]
    [Category("RestartOnFullSync")]
    [NonParallelizable]
    [TestCaseSource(nameof(DelayForFuzzerTestCases))]
    [Order(1)]
    public void ShouldKillConsensusClientWithIncreasingDelay(int currentDelay)
    {
        Logger.Info($"***Starting test: ShouldKillConsensusClientWithIncreasingDelay: {currentDelay} Delay***");

        NodeInfo.WaitForNodeToBeReady(Logger);
        NodeInfo.WaitForNodeToBeSynced(Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["consensus-container-name"], Count = 1, Minimum = currentDelay, Maximum = currentDelay, ShouldForceKillCommand = true }, Logger);
    }

    [TestCase(0, 60, 120)]
    [Category("InfinityRestartGracefullyOnFullSync")]
    public void ShouldRestartGracefullyNodeForInfinityOnSyncedNode(int restartCount, int minimumWait, int maximumWait)
    {
        Logger.Info("***Starting test: ShouldRestartGracefullyNodeForInfinityOnSyncedNode***");

        NodeInfo.WaitForNodeToBeReady(Logger);
        NodeInfo.WaitForNodeToBeSynced(Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = restartCount, Minimum = minimumWait, Maximum = maximumWait, ShouldForceGracefullCommand = true }, Logger);
    }
}
