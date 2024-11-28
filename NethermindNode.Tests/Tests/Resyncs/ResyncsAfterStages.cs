using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NethermindNode.Tests.Enums;

namespace NethermindNode.Tests.Resyncs;

internal class ResyncsAfterStages
{
    [NethermindTestCase(10)]
    [Category("ResyncsAfterState")]
    public void ShouldResyncAfterStateSync(int repeatCount)
    {
        var desiredStage = Stages.FastBodies.ToString();
        for (int i = 0; i < repeatCount; i++)
        {
            //Waiting for proper start of node
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

            //Waiting for OldBodie (stage after state sync)
            while (!NodeInfo.GetCurrentStage(TestLoggerContext.Logger).Contains(desiredStage))
            {
                TestLoggerContext.Logger.Debug("Waiting for node to be synced until stage :" + desiredStage);
                Thread.Sleep(30000);
            }

            //Add wait for 60 seconds just to ensure we didn't crashed anything right after sync
            Thread.Sleep(60000);

            StopAndResync();
            TestLoggerContext.Logger.Info($"Starting a FreshSync. Remaining fresh syncs to be executed: {repeatCount - i - 1}");
        }
    }

    [NethermindTest]
    [Category("ResyncsAfterStateNoLimit")]
    public void ShouldResyncAfterStateSyncsNoLimit()
    {
        var desiredStage = Stages.FastBodies.ToString();
        int iterator = 0;
        while (true)
        {
            //Waiting for proper start of node
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

            //Waiting for OldBodie (stage after state sync)
            while (!NodeInfo.GetCurrentStage(TestLoggerContext.Logger).Contains(desiredStage))
            {
                TestLoggerContext.Logger.Debug("Waiting for node to be synced until stage :" + desiredStage);
                Thread.Sleep(30000);
            }

            //Add wait for 60 seconds just to ensure we didn't crashed anything right after sync
            Thread.Sleep(60000);

            StopAndResync();
            iterator++;
            TestLoggerContext.Logger.Info($"Starting a FreshSync. Currently synced {iterator} times.");
        }
    }

    private void StopAndResync()
    {
        //Stopping and clearing EL
        DockerCommands.StopDockerContainer(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger);
        while (!DockerCommands.GetDockerContainerStatus(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger).Contains("exited"))
        {
            TestLoggerContext.Logger.Debug($"Waiting for {ConfigurationHelper.Instance["execution-container-name"]} docker status to be \"exited\". Current status: {DockerCommands.GetDockerContainerStatus(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger)}");
            Thread.Sleep(30000);
        }
        CommandExecutor.RemoveDirectory("/root/execution-data/nethermind_db", TestLoggerContext.Logger);

        //Restarting Node - freshSync
        DockerCommands.StartDockerContainer(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger);
    }
}
