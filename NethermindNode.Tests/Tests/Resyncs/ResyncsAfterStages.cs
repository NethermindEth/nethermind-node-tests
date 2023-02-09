using NethermindNode.Core.Helpers;
using NethermindNode.Tests.Enums;

namespace NethermindNode.Tests.Resyncs;

internal class ResyncsAfterStages
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    [TestCase(10)]
    [Category("ResyncsAfterState")]
    public void ShouldResyncAfterStateSync(int repeatCount)
    {
        var desiredStage = Stages.FastBodies.ToString();

        Logger.Info($"***Starting test: ShouldResyncAfterStateSync --- repeatCount: {repeatCount}***");
        for (int i = 0; i < repeatCount; i++)
        {
            //Waiting for proper start of node
            while (DockerCommands.CheckIfDockerContainerIsCreated("execution-client", Logger) == false)
            {
                Logger.Info("Waiting for Execution to be started.");
                Thread.Sleep(30000);
            }

            //Waiting for OldBodie (stage after state sync)
            while (!NodeInfo.GetCurrentStage(Logger).Contains(desiredStage))
            {
                Logger.Info("Waiting for node to be synced until stage :" + desiredStage);
                Thread.Sleep(30000);
            }

            StopAndResync();
            Logger.Info($"Starting a FreshSync. Remaining fresh syncs to be executed: {repeatCount - i - 1}");
        }
    }

    private void StopAndResync()
    {
        //Stopping and clearing EL
        DockerCommands.StopDockerContainer("execution-client", Logger);
        while (!DockerCommands.GetDockerContainerStatus("execution-client", Logger).Contains("exited"))
        {
            Logger.Info($"Waiting for execution-client docker status to be \"exited\". Current status: {DockerCommands.GetDockerContainerStatus("execution-client", Logger)}");
            Thread.Sleep(30000);
        }
        CommandExecutor.RemoveDirectory("/root/execution-data/nethermind_db", Logger);

        //Restarting Node - freshSync
        DockerCommands.StartDockerContainer("execution-client", Logger);
    }
}
