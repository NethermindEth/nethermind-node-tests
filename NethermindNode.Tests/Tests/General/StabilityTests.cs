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
        TestLoggerContext.Logger.Info("=== PHASE 1: Waiting for initial sync ===");
        while (!isNodeSynced)
        {
            ForceStopWatcher.ThrowIfStopRequested();

            isNodeSynced = NodeInfo.IsFullySynced(TestLoggerContext.Logger);

            if (isNodeSynced)
            {
                TestLoggerContext.Logger.Info("Node is fully synced.");
                break;
            }

            bool verificationSucceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSucceeded == true, "Undesired log occurred: " + string.Join(", ", errors));

            Thread.Sleep(10000);
        }

        // Phase 2: Graceful restart
        TestLoggerContext.Logger.Info("=== PHASE 2: Graceful restart (docker stop) ===");
        DockerCommands.StopDockerContainer(ContainerName, TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("Container stopped gracefully. Starting again...");
        DockerCommands.StartDockerContainer(ContainerName, TestLoggerContext.Logger);

        TestLoggerContext.Logger.Info("Waiting for node to be ready after graceful restart...");
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("Node is ready. Verifying stability for 10 minutes...");
        VerifyStabilityForDuration(TimeSpan.FromMinutes(10), errors);

        // Phase 3: Ungraceful restart
        TestLoggerContext.Logger.Info("=== PHASE 3: Ungraceful restart (docker kill) ===");
        DockerCommands.KillDockerContainer(ContainerName, TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("Container killed. Starting again...");
        Thread.Sleep(5000); // Brief pause before restart
        DockerCommands.StartDockerContainer(ContainerName, TestLoggerContext.Logger);

        TestLoggerContext.Logger.Info("Waiting for node to be ready after ungraceful restart...");
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        TestLoggerContext.Logger.Info("Node is ready. Verifying stability for 10 minutes...");
        VerifyStabilityForDuration(TimeSpan.FromMinutes(10), errors);

        TestLoggerContext.Logger.Info("=== ALL PHASES PASSED ===");
    }

    private void VerifyStabilityForDuration(TimeSpan duration, List<string> errors)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while (stopwatch.Elapsed < duration)
        {
            ForceStopWatcher.ThrowIfStopRequested();

            bool verificationSucceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSucceeded == true,
                "Undesired log occurred during stability check: " + string.Join(", ", errors));

            Thread.Sleep(10000);
        }

        TestLoggerContext.Logger.Info($"Stability verified for {duration.TotalMinutes} minutes — no errors found.");
    }
}