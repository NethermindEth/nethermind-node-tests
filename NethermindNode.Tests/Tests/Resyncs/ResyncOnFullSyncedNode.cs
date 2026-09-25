using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;

namespace NethermindNode.Tests.Resyncs;

internal class ResyncOnFullSyncedNode
{
    

    [NethermindTestCase(10)]
    [Category("Resyncs")]
    public void ShouldResyncAfterFullSync(int repeatCount)
    {
        for (int i = 0; i < repeatCount; i++)
        {
            //Waiting for proper start of node
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

            //Waiting for Full Sync
            while (!NodeInfo.IsFullySynced(TestLoggerContext.Logger))
            {
                TestLoggerContext.Logger.Debug("Waiting for node to be fully synced.");
                Thread.Sleep(30000);
            }

            //Stopping and clearing EL
            DockerCommands.StopDockerContainer(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger);
            while (!DockerCommands.GetDockerContainerStatus(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger).Contains("exited"))
            {
                TestLoggerContext.Logger.Debug($"Waiting for {ConfigurationHelper.Instance["execution-container-name"]} docker status to be \"exited\". Current status: {DockerCommands.GetDockerContainerStatus(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger)}");
                Thread.Sleep(30000);
            }
            CommandExecutor.RemoveDirectory("/root/execution-data/nethermind_db", TestLoggerContext.Logger);

            //Restarting Node - freshSync
            TestLoggerContext.Logger.Info($"Starting a FreshSync. Remaining fresh syncs to be executed: {repeatCount - i - 1}");
            DockerCommands.StartDockerContainer(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger);
        }
    }

    [NethermindTest]
    [Category("ResyncsNoLimit")]
    public void ShouldResyncAfterFullSyncNoLimit()
    {
        while (true)
        {
            //Waiting for proper start of node
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

            //Waiting for Full Sync
            while (!NodeInfo.IsFullySynced(TestLoggerContext.Logger))
            {
                TestLoggerContext.Logger.Debug("Waiting for node to be fully synced.");
                Thread.Sleep(30000);
            }

            //Stopping and clearing EL
            DockerCommands.StopDockerContainer(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger);
            while (!DockerCommands.GetDockerContainerStatus(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger).Contains("exited"))
            {
                TestLoggerContext.Logger.Debug($"Waiting for {ConfigurationHelper.Instance["execution-container-name"]} docker status to be \"exited\". Current status: {DockerCommands.GetDockerContainerStatus(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger)}");
                Thread.Sleep(30000);
            }
            CommandExecutor.RemoveDirectory("/root/execution-data/nethermind_db", TestLoggerContext.Logger);

            //Restarting Node - freshSync
            TestLoggerContext.Logger.Info($"Starting a FreshSync.");
            DockerCommands.StartDockerContainer(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger);
        }
    }
}
