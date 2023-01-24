using NethermindNode.Core.Helpers;
using NethermindNode.Helpers;
using NethermindNode.Tests.Helpers;
using Newtonsoft.Json;

namespace NethermindNode.Tests.SyncingNode
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class StagesFuzzer : BaseTest
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);
        private List<string> _stagesFound = new List<string>();

        [Test]
        [Category("SnapSync")]
        [Category("FastSync")]
        [Category("FullSync")]
        [Category("ArchiveSync")]
        public void ShouldKillNodeOnAllPossibleStages()
        {
            Logger.Info("***Starting test: ShouldKillNodeOnAllPossibleStages***");

            while (DockerCommands.CheckIfDockerContainerIsCreated("execution-client", Logger) == false)
            {
                Logger.Info("Waiting for Execution to be started.");
                Thread.Sleep(5000);
            }
            while (!NodeInfo.IsFullySynced(Logger))
            {
                var currentStage = NodeInfo.GetCurrentStage(Logger);
                if (!_stagesFound.Contains(currentStage) && currentStage != "WaitingForConnection")
                {
                    _stagesFound.Add(currentStage);
                    Logger.Info("Fuzzing at stage: " + currentStage);
                    FuzzerHelper.Fuzz(new FuzzerCommandOptions { ShouldForceKillCommand = true }, Logger);
                }
                Thread.Sleep(1000);
            }
            Logger.Info("Node is synced so test passed correctly");
        }
    }
}
