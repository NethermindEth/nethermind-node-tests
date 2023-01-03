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
                    Thread.Sleep(30000);
                }

                //Waiting for Full Sync
                while (!NodeInfo.IsFullySynced(Logger))
                {
                    Logger.Info("Waiting for node to be fully synced.");
                    Thread.Sleep(30000);
                }

                //Stopping and clearing EL
                DockerCommands.StopDockerContainer("execution-client", Logger);
                while (!DockerCommands.GetDockerContainerStatus("execution-client", Logger).Contains("exited"))
                {
                    Logger.Info($"Waiting for execution-client docker status to be \"exited\". Current status: {DockerCommands.GetDockerContainerStatus("execution-client", Logger)}");
                    Thread.Sleep(30000);
                }
                CommandExecutor.RemoveDirectory("/root/execution-data/nethermind_db", Logger);

                //Restarting Node - freshSync
                Logger.Info($"Starting a FreshSync. Remaining fresh syncs to be executed: {repeatCount - i - 1}");
                DockerCommands.StartDockerContainer("execution-client", Logger);
            }
        }
    }
}
