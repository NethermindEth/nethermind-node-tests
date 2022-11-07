using NethermindNodeTests.Helpers;
using Newtonsoft.Json;
using SedgeNodeFuzzer.Helpers;

namespace NethermindNodeTests.Tests.SyncingNode
{

    public class StagesFuzzer
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private List<string> _stagesFound = new List<string>();

        [Test]
        [Category("SnapSync")]
        [Category("FastSync")]
        [Category("FullSync")]
        [Category("ArchiveSync")]
        public void ShouldKillNodeOnAllPossibleStages()
        {
            while (DockerCommands.CheckIfDockerContainerIsCreated("execution-client") == false)
            {
                Logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Waiting for Execution to be started.");
                Thread.Sleep(5000);
            }

            Logger.Info("***Starting test: ShouldKillNodeOnAllPossibleStages***");
            while (!IsFullySynced())
            {
                var currentStage = GetCurrentStage();
                if (!_stagesFound.Contains(currentStage))
                {
                    _stagesFound.Add(currentStage);
                    Logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Fuzzing at stage: " + currentStage);
                    FuzzerHelper.Fuzz(new FuzzerCommandOptions { ShouldForceKillCommand = true });
                }
                Thread.Sleep(1000);
            }
            Logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Node is synced so test passed correctly");
        }

        private string GetCurrentStage()
        {
            var commandResult = CurlExecutor.ExecuteCommand("debug_getSyncStage", "http://localhost:8545");
            string output = commandResult.Result == null ? "WaitingForConnection" : ((dynamic)JsonConvert.DeserializeObject(commandResult.Result)).result.currentStage.ToString();
            Logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Current stage is: " + output);
            return output;
        }

        private bool IsFullySynced()
        {
            var commandResult = CurlExecutor.ExecuteCommand("eth_syncing", "http://localhost:8545");
            var result = commandResult.Result;
            return result == null ? false : result.Contains("false");
        }
    }
}
