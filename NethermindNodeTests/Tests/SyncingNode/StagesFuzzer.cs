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
            var commandResult = CurlExecutor.ExecuteCommand("debug_getSyncsStage", "http://localhost:8545");
            try
            {
                dynamic output = JsonConvert.DeserializeObject(commandResult.Result.Content.ReadAsStringAsync().Result);
                Logger.Info("Current stage is: " + output.result.currentStage.ToString());
                return output.result.currentStage.ToString();
            }
            catch (Exception e)
            {
                if (e.Message.Contains("No connection could be made because the target machine actively refused it."))
                    return "WaitingForConnection";
            }
            return string.Empty;
        }

        private bool IsFullySynced()
        {
            var commandResult = CurlExecutor.ExecuteCommand("eth_syncing", "http://localhost:8545");
            var result = commandResult.Result.Content.ReadAsStringAsync().Result;
            return result.Contains("false");
        }
    }
}
