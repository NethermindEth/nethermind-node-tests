using NethermindNodeTests.Helpers;
using SedgeNodeFuzzer.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Tests.Resyncs
{
    internal class ResyncOnFullSyncedNode
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        [TestCase(10)]
        [Category("Resyncs")]
        public void ShouldResyncAfterFullSync(int repeatCount)
        {
            Logger.Info($"***Starting test: ShouldResyncAfterFullSync --- repeatCount: {repeatCount}***");
            for (int i = 0; i < repeatCount; i++)
            {
                //Waiting for proper start of node
                while (DockerCommands.CheckIfDockerContainerIsCreated("execution-client", Logger) == false)
                {
                    Logger.Info("Waiting for Execution to be started.");
                    Thread.Sleep(5000);
                }

                //Waiting for Full Sync
                while (!IsFullySynced())
                {
                    Logger.Info("Waiting for node to be fully synced.");
                    Thread.Sleep(5000);
                }

                //Stopping and clearing EL
                DockerCommands.StopDockerContainer("execution", Logger);
                while (DockerCommands.GetDockerContainerStatus("execution-client", Logger) != "exited")
                {
                    Logger.Info("Waiting for execution-client docker status to be \"exited\".");
                    Thread.Sleep(5000);
                }
                CommandExecutor.RemoveDirectory("/root/execution-client/nethermind_db", Logger);

                //Restarting Node - freshSync
                DockerCommands.StartDockerContainer("execution", Logger);
            }
        }

        private bool IsFullySynced()
        {
            var commandResult = CurlExecutor.ExecuteNethermindJsonRpcCommand("eth_syncing", "http://localhost:8545", Logger);
            var result = commandResult.Result;
            return result == null ? false : result.Contains("false");
        }
    }
}
