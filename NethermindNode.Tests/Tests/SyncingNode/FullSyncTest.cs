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

        List<string> errors = new List<string>();
        bool reachedTarget = false;
        int iteration = 0;

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
                TestLoggerContext.Logger.Info("[FULLSYNC] \u2713 Reached WaitingForBlock \u2014 sync complete");
                reachedTarget = true;
                break;
            }

            bool verificationSucceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSucceeded == true, "Undesired log occurred: " + string.Join(", ", errors));

            // Log progress every 5 minutes (every 10th iteration of 30s sleep)
            if (iteration == 0 || iteration % 10 == 0)
            {
                long blockNumber = -1;
                try { blockNumber = NodeInfo.GetCurrentBlock(TestLoggerContext.Logger); } catch { }
                TestLoggerContext.Logger.Info($"[FULLSYNC] Waiting for WaitingForBlock stage \u2014 current: {string.Join(",", stages)}, block: {blockNumber}");
            }
            iteration++;

            Thread.Sleep(30000); // Check every 30s
        }

        // Phase 2: Graceful restart
        TestLoggerContext.Logger.Info("[RESTART] Graceful restart...");
        DockerCommands.StopDockerContainer(containerName, TestLoggerContext.Logger);
        DockerCommands.StartDockerContainer(containerName, TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("[RESTART] Waiting for node...");
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("[RESTART] \u2713 Node ready \u2014 verifying stability (10 min)");
        VerifyStability(TimeSpan.FromMinutes(10), errors);

        // Phase 3: Ungraceful restart
        TestLoggerContext.Logger.Info("[RESTART] Ungraceful restart (docker kill \u2192 docker start)");
        DockerCommands.KillDockerContainer(containerName, TestLoggerContext.Logger);
        Thread.Sleep(5000);
        DockerCommands.StartDockerContainer(containerName, TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("[RESTART] Waiting for node...");
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("[RESTART] \u2713 Node ready \u2014 verifying stability (10 min)");
        VerifyStability(TimeSpan.FromMinutes(10), errors);

        TestLoggerContext.Logger.Info("[RESULT] \u2713 ALL PHASES PASSED");
    }

    private void VerifyStability(TimeSpan duration, List<string> errors)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lastProgressLog = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < duration)
        {
            ForceStopWatcher.ThrowIfStopRequested();
            bool ok = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(ok == true, "Undesired log during stability: " + string.Join(", ", errors));

            if (lastProgressLog.Elapsed >= TimeSpan.FromMinutes(2))
            {
                var remaining = duration - sw.Elapsed;
                TestLoggerContext.Logger.Info($"[STABILITY] Monitoring... {remaining.TotalMinutes:F0} min remaining");
                lastProgressLog.Restart();
            }

            Thread.Sleep(10000);
        }
        TestLoggerContext.Logger.Info($"[STABILITY] \u2713 No errors for {duration.TotalMinutes:F0} minutes");
    }
}
