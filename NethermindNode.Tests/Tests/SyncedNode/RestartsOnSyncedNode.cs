using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;

namespace NethermindNode.Tests.SyncedNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RestartsOnSyncedNode : BaseTest
{
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
    [NethermindTestCaseSource(nameof(DelayForFuzzerTestCases))]
    [Order(0)]
    public void ShouldRestartNethermindClientWithIncreasingDelay(int currentDelay)
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = 1, Minimum = currentDelay, Maximum = currentDelay, ShouldForceGracefullCommand = true }, TestLoggerContext.Logger);
    }

    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("RestartOnFullSync")]
    [NonParallelizable]
    [NethermindTestCaseSource(nameof(DelayForFuzzerTestCases))]
    [Order(1)]
    public void ShouldRestartConsensusClientWithIncreasingDelay(int currentDelay)
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["consensus-container-name"], Count = 1, Minimum = currentDelay, Maximum = currentDelay, ShouldForceGracefullCommand = true }, TestLoggerContext.Logger);
    }

    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("RestartOnFullSync")]
    [NonParallelizable]
    [NethermindTestCaseSource(nameof(DelayForFuzzerTestCases))]
    [Order(0)]
    public void ShouldKillNethermindClientWithIncreasingDelay(int currentDelay)
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = 1, Minimum = currentDelay, Maximum = currentDelay, ShouldForceKillCommand = true }, TestLoggerContext.Logger);
    }

    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("RestartOnFullSync")]
    [NonParallelizable]
    [NethermindTestCaseSource(nameof(DelayForFuzzerTestCases))]
    [Order(1)]
    public void ShouldKillConsensusClientWithIncreasingDelay(int currentDelay)
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["consensus-container-name"], Count = 1, Minimum = currentDelay, Maximum = currentDelay, ShouldForceKillCommand = true }, TestLoggerContext.Logger);
    }

    [NethermindTestCase(0, 60, 120)]
    [Category("InfinityRestartGracefullyOnFullSync")]
    public void ShouldRestartGracefullyNodeForInfinityOnSyncedNode(int restartCount, int minimumWait, int maximumWait)
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = restartCount, Minimum = minimumWait, Maximum = maximumWait, ShouldForceGracefullCommand = true }, TestLoggerContext.Logger);
    }

    [NethermindTestCase(0, 60, 120)]
    [Category("InfinityKillOnFullSync")]
    public void ShouldKillNodeForInfinityOnSyncedNode(int restartCount, int minimumWait, int maximumWait)
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = restartCount, Minimum = minimumWait, Maximum = maximumWait, ShouldForceKillCommand = true }, TestLoggerContext.Logger);
    }

    [Repeat(10)]
    [Category("InMemoryFastKill")]
    [NethermindTest]
    public async Task ShouldKillNethermindClientAfterMemoryPruning()
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

        string expectedLog = "Executed memory prune";

        CancellationTokenSource cts = new CancellationTokenSource();

        await foreach (var line in DockerCommands.GetDockerLogsAsync(ConfigurationHelper.Instance["execution-container-name"], "", true, cts.Token)) 
        {
            Console.WriteLine(line);

            if (!line.Contains(expectedLog))
            {
                continue;
            }

            TestLoggerContext.Logger.Info($"Log found: \"{line}\" - Expected log: {expectedLog}");

            break;
        }

        FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = 1, ShouldForceKillCommand = true }, TestLoggerContext.Logger);
    }

    [Repeat(10)]
    [Category("InMemorySaveKill")]
    [NethermindTestCase(1, 1)]
    public async Task ShouldKillNethermindClientAfterMemoryPruningSavedReorgBoundary(int amountOfGracefullShutdowns, int amountOfKills)
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

        string expectedLog = "Saving reorg boundary";

        int executedGracefull = 0;
        int executedKills = 0;

        CancellationTokenSource cts = new CancellationTokenSource();

        await foreach (var line in DockerCommands.GetDockerLogsAsync(ConfigurationHelper.Instance["execution-container-name"], "", true, cts.Token)) //since to ensure that we will get only recent logs but including all from beggining of test
        {
            Console.WriteLine(line);

            if (!line.Contains(expectedLog))
            {
                continue;
            }

            if (executedGracefull < amountOfGracefullShutdowns)
            {
                FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = 1, ShouldForceGracefullCommand = true }, TestLoggerContext.Logger);
                executedGracefull++;
            }
            else if (executedKills < amountOfKills)
            {
                FuzzerHelper.Fuzz(new FuzzerCommandOptions { DockerContainerName = ConfigurationHelper.Instance["execution-container-name"], Count = 1, ShouldForceKillCommand = true }, TestLoggerContext.Logger);
                executedKills++;
            }

            TestLoggerContext.Logger.Info($"Log found: \"{line}\" - Expected log: {expectedLog}");

            break;
        }
    }
}
