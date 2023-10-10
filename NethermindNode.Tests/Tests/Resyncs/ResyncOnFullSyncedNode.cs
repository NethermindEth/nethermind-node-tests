using NethermindNode.Core.Helpers;

namespace NethermindNode.Tests.Resyncs;

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
            NodeInfo.WaitForNodeToBeReady(Logger);

            //Waiting for Full Sync
            while (!NodeInfo.IsFullySynced(Logger))
            {
                Logger.Debug("Waiting for node to be fully synced.");
                Thread.Sleep(30000);
            }

            //Stopping and clearing EL
            DockerCommands.StopDockerContainer(ConfigurationHelper.Instance["execution-container-name"], Logger);
            while (!DockerCommands.GetDockerContainerStatus(ConfigurationHelper.Instance["execution-container-name"], Logger).Contains("exited"))
            {
                Logger.Debug($"Waiting for {ConfigurationHelper.Instance["execution-container-name"]} docker status to be \"exited\". Current status: {DockerCommands.GetDockerContainerStatus(ConfigurationHelper.Instance["execution-container-name"], Logger)}");
                Thread.Sleep(30000);
            }
            CommandExecutor.RemoveDirectory("/root/execution-data/nethermind_db", Logger);

            //Restarting Node - freshSync
            Logger.Info($"Starting a FreshSync. Remaining fresh syncs to be executed: {repeatCount - i - 1}");
            DockerCommands.StartDockerContainer(ConfigurationHelper.Instance["execution-container-name"], Logger);
        }
    }
}
