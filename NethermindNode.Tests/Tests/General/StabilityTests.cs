using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NethermindNode.Tests.Helpers;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StabilityTests : BaseTest
{
    private string ContainerName => ConfigurationHelper.Instance["execution-container-name"];

    [NethermindTest]
    [Category("StabilityCheck")]
    public void ShouldVerifyThatNodeSyncsWithoutErrors()
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

        bool isNodeSynced = false;
        List<string> errors = new List<string>();

        // Phase 1: Wait for initial sync
        TestLoggerContext.Logger.Info("[SYNC] Waiting for initial sync...");
        while (!isNodeSynced)
        {
            ForceStopWatcher.ThrowIfStopRequested();

            isNodeSynced = NodeInfo.IsFullySynced(TestLoggerContext.Logger);

            if (isNodeSynced)
            {
                TestLoggerContext.Logger.Info("[SYNC] \u2713 Node fully synced");
                break;
            }

            bool verificationSucceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSucceeded == true, "Undesired log occurred: " + string.Join(", ", errors));

            Thread.Sleep(10000);
        }

        // Phase 2: Graceful restart
        TestLoggerContext.Logger.Info("[RESTART] Graceful restart (docker stop \u2192 docker start)");
        DockerCommands.StopDockerContainer(ContainerName, TestLoggerContext.Logger);
        DockerCommands.StartDockerContainer(ContainerName, TestLoggerContext.Logger);

        TestLoggerContext.Logger.Info("[RESTART] Waiting for node...");
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("[RESTART] \u2713 Node ready \u2014 verifying stability (10 min)");
        VerifyStabilityForDuration(TimeSpan.FromMinutes(10), errors);

        // Phase 3: Ungraceful restart
        TestLoggerContext.Logger.Info("[RESTART] Ungraceful restart (docker kill \u2192 docker start)");
        DockerCommands.KillDockerContainer(ContainerName, TestLoggerContext.Logger);
        Thread.Sleep(5000); // Brief pause before restart
        DockerCommands.StartDockerContainer(ContainerName, TestLoggerContext.Logger);

        TestLoggerContext.Logger.Info("[RESTART] Waiting for node...");
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("[RESTART] \u2713 Node ready \u2014 verifying stability (10 min)");
        VerifyStabilityForDuration(TimeSpan.FromMinutes(10), errors);

        TestLoggerContext.Logger.Info("[RESULT] \u2713 ALL PHASES PASSED");
    }

    private void VerifyStabilityForDuration(TimeSpan duration, List<string> errors)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var lastProgressLog = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < duration)
        {
            ForceStopWatcher.ThrowIfStopRequested();

            bool verificationSucceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSucceeded == true,
                "Undesired log occurred during stability check: " + string.Join(", ", errors));

            if (lastProgressLog.Elapsed >= TimeSpan.FromMinutes(2))
            {
                var remaining = duration - stopwatch.Elapsed;
                TestLoggerContext.Logger.Info($"[STABILITY] Monitoring... {remaining.TotalMinutes:F0} min remaining");
                lastProgressLog.Restart();
            }

            Thread.Sleep(10000);
        }

        TestLoggerContext.Logger.Info($"[STABILITY] \u2713 No errors for {duration.TotalMinutes:F0} minutes");
    }
}