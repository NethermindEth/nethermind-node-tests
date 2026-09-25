using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StagesFuzzer : BaseTest
{
    private List<string> _stagesFound = new List<string>();

    [NethermindTest]
    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    [Category("StageFuzzerKiller")]
    public void ShouldKillNodeOnAllPossibleStages()
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

        while (!NodeInfo.IsFullySynced(TestLoggerContext.Logger))
        {
            var currentStage = NodeInfo.GetCurrentStage(TestLoggerContext.Logger);
            if (!_stagesFound.Contains(currentStage) && currentStage != "WaitingForConnection")
            {
                _stagesFound.Add(currentStage);
                TestLoggerContext.Logger.Info("Killing node at stage: " + currentStage);
                FuzzerHelper.Fuzz(new FuzzerCommandOptions { ShouldForceKillCommand = true, DockerContainerName = ConfigurationHelper.Instance["execution-container-name"] }, TestLoggerContext.Logger);
            }
            Thread.Sleep(1000);
        }
    }

    [NethermindTest]
    [Category("SnapSync")]
    [Category("FastSync")]
    [Category("FullSync")]
    public void ShouldStopGracefullyNodeOnAllPossibleStages()
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

        while (!NodeInfo.IsFullySynced(TestLoggerContext.Logger))
        {
            var currentStage = NodeInfo.GetCurrentStage(TestLoggerContext.Logger);
            if (!_stagesFound.Contains(currentStage) && currentStage != "WaitingForConnection")
            {
                _stagesFound.Add(currentStage);
                TestLoggerContext.Logger.Info("Stopping gracefully at stage: " + currentStage);
                FuzzerHelper.Fuzz(new FuzzerCommandOptions { ShouldForceGracefullCommand = true, DockerContainerName = ConfigurationHelper.Instance["execution-container-name"] }, TestLoggerContext.Logger);
            }
            Thread.Sleep(1000);
        }
    }
}
