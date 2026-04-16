using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NethermindNode.Tests.Enums;
using NethermindNode.Tests.Helpers;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class FullSyncTest : BaseTest
{
    /// <summary>
    /// Waits for archive/full sync to complete by monitoring debug_getSyncStage.
    /// Unlike StabilityCheck which uses eth_syncing (unreliable for archive sync),
    /// this checks for WaitingForBlock stage which means the node caught up to chain head.
    /// After sync: graceful restart + 10min stability, ungraceful restart + 10min stability.
    /// </summary>
    [NethermindTest]
    [Category("FullSyncComplete")]
    public void ShouldFullSyncUntilWaitingForBlock()
    {
        string containerName = ConfigurationHelper.Instance["execution-container-name"];

        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("=== FULL SYNC: Waiting for WaitingForBlock stage ===");

        List<string> errors = new List<string>();
        bool reachedTarget = false;

        while (!reachedTarget)
        {
            ForceStopWatcher.ThrowIfStopRequested();

            var stages = NodeInfo.GetCurrentStages(TestLoggerContext.Logger);

            // WaitingForBlock alone (not combined with FastBodies/FastReceipts) = fully synced
            if (stages.Contains(Stages.WaitingForBlock) &&
                !stages.Contains(Stages.FastBodies) &&
                !stages.Contains(Stages.FastReceipts) &&
                !stages.Contains(Stages.StateNodes) &&
                !stages.Contains(Stages.SnapSync))
            {
                TestLoggerContext.Logger.Info("Reached WaitingForBlock (alone) — full sync complete!");
                reachedTarget = true;
                break;
            }

            bool verificationSucceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSucceeded == true, "Undesired log occurred: " + string.Join(", ", errors));

            // Log progress
            int blockNumber = -1;
            try { blockNumber = NodeInfo.GetCurrentBlock(TestLoggerContext.Logger); } catch { }
            TestLoggerContext.Logger.Info($"Syncing... Block={blockNumber}, Stages={string.Join(",", stages)}");

            Thread.Sleep(30000); // Check every 30s
        }

        // Phase 2: Graceful restart
        TestLoggerContext.Logger.Info("=== FULL SYNC: Graceful restart (docker stop) ===");
        DockerCommands.StopDockerContainer(containerName, TestLoggerContext.Logger);
        DockerCommands.StartDockerContainer(containerName, TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("Verifying stability for 10 minutes...");
        VerifyStability(TimeSpan.FromMinutes(10), errors);

        // Phase 3: Ungraceful restart
        TestLoggerContext.Logger.Info("=== FULL SYNC: Ungraceful restart (docker kill) ===");
        DockerCommands.KillDockerContainer(containerName, TestLoggerContext.Logger);
        Thread.Sleep(5000);
        DockerCommands.StartDockerContainer(containerName, TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("Verifying stability for 10 minutes...");
        VerifyStability(TimeSpan.FromMinutes(10), errors);

        TestLoggerContext.Logger.Info("=== FULL SYNC TEST PASSED ===");
    }

    private void VerifyStability(TimeSpan duration, List<string> errors)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < duration)
        {
            ForceStopWatcher.ThrowIfStopRequested();
            bool ok = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(ok == true, "Undesired log during stability: " + string.Join(", ", errors));
            Thread.Sleep(10000);
        }
        TestLoggerContext.Logger.Info($"Stability verified for {duration.TotalMinutes} minutes.");
    }
}
