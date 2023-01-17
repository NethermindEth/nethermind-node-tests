using NethermindNode.Core.Helpers;
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
                Logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Waiting for Execution to be started.");
                Thread.Sleep(5000);
            }
            while (!IsFullySynced())
            {
                var currentStage = GetCurrentStage();
                if (!_stagesFound.Contains(currentStage) && currentStage != "WaitingForConnection")
                {
                    _stagesFound.Add(currentStage);
                    Logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Fuzzing at stage: " + currentStage);
                    FuzzerHelper.Fuzz(new FuzzerCommandOptions { ShouldForceKillCommand = true }, Logger);
                }
                Thread.Sleep(1000);
            }
            Logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Node is synced so test passed correctly");
        }

        private string GetCurrentStage()
        {
            var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("debug_getSyncStage", "", "http://localhost:8545", Logger);
            string output = commandResult.Result == null ? "WaitingForConnection" : ((dynamic)JsonConvert.DeserializeObject(commandResult.Result.Item1)).result.currentStage.ToString();
            Logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Current stage is: " + output);
            return output;
        }

        private bool IsFullySynced()
        {
            var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("eth_syncing", "", "http://localhost:8545", Logger);
            var result = commandResult.Result;
            return result == null ? false : result.Item1.Contains("false");
        }
    }
}
