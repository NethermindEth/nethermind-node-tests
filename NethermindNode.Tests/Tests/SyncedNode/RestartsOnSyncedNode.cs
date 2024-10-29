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

    [TestCase(0, 60, 120)]
    [Category("InfinityKillOnFullSync")]
    public void ShouldKillNodeForInfinityOnSyncedNode(int restartCount, int minimumWait, int maximumWait)
    {
        Logger.Info("***Starting test: ShouldKillNodeForInfinityOnSyncedNode***");

        NodeInfo.WaitForNodeToBeReady(Logger);
        NodeInfo.WaitForNodeToBeSynced(Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = restartCount, Minimum = minimumWait, Maximum = maximumWait, ShouldForceKillCommand = true }, Logger);
    }

    [Repeat(10)]
    [Category("InMemoryFastKill")]
    [Test]
    public void ShouldKillNethermindClientAfterMemoryPruning()
    {
        Logger.Info($"***Starting test: ShouldKillNethermindClientAfterMemoryPruning***");

        NodeInfo.WaitForNodeToBeReady(Logger);
        NodeInfo.WaitForNodeToBeSynced(Logger);

        string expectedLog = "Executed memory prune";

        foreach (var line in DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "", true, null, "--since 2m")) //since to ensure that we will get only recent logs but including all from beggining of test
        {
            Console.WriteLine(line);

            if (!line.Contains(expectedLog))
            {
                continue;
            }

            Logger.Info($"Log found: \"{line}\" - Expected log: {expectedLog}");

            break;
        }

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = 1, ShouldForceKillCommand = true }, Logger);
    }

    [Repeat(10)]
    [Category("InMemorySaveKill")]
    [TestCase(1, 1)]
    public void ShouldKillNethermindClientAfterMemoryPruningSavedReorgBoundary(int amountOfGracefullShutdowns, int amountOfKills)
    {
        Logger.Info($"***Starting test: ShouldKillNethermindClientAfterMemoryPruningSavedReorgBoundary***");

        NodeInfo.WaitForNodeToBeReady(Logger);
        NodeInfo.WaitForNodeToBeSynced(Logger);

        string expectedLog = "Saving reorg boundary";

        int executedGracefull = 0;
        int executedKills = 0;

        foreach (var line in DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "", true, null, "--since 2m")) //since to ensure that we will get only recent logs but including all from beggining of test
        {
            Console.WriteLine(line);

            if (!line.Contains(expectedLog))
            {
                continue;
            }

            if (executedGracefull < amountOfGracefullShutdowns)
            {
                FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = 1, ShouldForceGracefullCommand = true }, Logger);
                executedGracefull++;
            }
            else if (executedKills < amountOfKills)
            {
                FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = 1, ShouldForceKillCommand = true }, Logger);
                executedKills++;
            }

            Logger.Info($"Log found: \"{line}\" - Expected log: {expectedLog}");

            break;
        }
    }
}
