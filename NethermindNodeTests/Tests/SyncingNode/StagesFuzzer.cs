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
                Logger.Info("Waiting for Execution to be started.");
                Thread.Sleep(5000);
            }

            Logger.Info("***Starting test: ShouldKillNodeOnAllPossibleStages***");
            while (IsFullySynced())
            {
                var currentStage = GetCurrentStage();
                if (!_stagesFound.Contains(currentStage))
                {
                    _stagesFound.Add(currentStage);
                    Logger.Info("Fuzzing at stage: " + currentStage);
                    FuzzerHelper.Fuzz(new FuzzerCommandOptions { ShouldForceKillCommand = true });
                }
            }
            Logger.Info("Node is synced so test passed correctly");
        }

        private string GetCurrentStage()
        {
            try
            {
                var commandResult = CurlExecutor.ExecuteCommand("debug_getSyncsStage", "http://localhost:8545");
                dynamic output = JsonConvert.DeserializeObject(commandResult.Result.Content.ReadAsStringAsync().Result);
                Logger.Info("Current stage is: " + output.result.currentStage.ToString());
                return output.result.currentStage.ToString();
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                Logger.Error(e.InnerException?.Message);
                if (e.Message.Contains("No connection could be made because the target machine actively refused it."))
                    return "WaitingForConnection";
                else
                    throw e;
            }
        }

        private bool IsFullySynced()
        {
            try
            {
                var commandResult = CurlExecutor.ExecuteCommand("eth_syncing", "http://localhost:8545");
                var result = commandResult.Result.Content.ReadAsStringAsync().Result;
                return result.Contains("false");
            }
            catch (Exception e)
            {
                Logger.Error(e.Message);
                Logger.Error(e.InnerException?.Message);
                if (e.Message.Contains("No connection could be made because the target machine actively refused it."))
                    return false;
                else
                    throw e;
            }
        }
    }
}
