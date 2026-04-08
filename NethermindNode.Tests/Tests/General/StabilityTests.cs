using System.Diagnostics;
using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StabilityTests : BaseTest
{
    [NethermindTest]
    [Category("StabilityCheck")]
    public void ShouldVerifyThatNodeSyncsWithoutErrors()
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

        // Use TESTS_TIMEOUT env var as a safety timeout (in milliseconds)
        long maxWaitMs = int.TryParse(Environment.GetEnvironmentVariable("TESTS_TIMEOUT"), out var t) && t > 0
            ? t
            : long.MaxValue;
        var sw = Stopwatch.StartNew();

        bool isNodeSynced = false;

        List<string> errors = new List<string>();

        while (!isNodeSynced)
        {
            Assert.That(sw.ElapsedMilliseconds < maxWaitMs,
                $"Sync timed out after {sw.Elapsed.TotalHours:F1}h - node did not sync within allowed time. " +
                $"Last stage: {NodeInfo.GetCurrentStage(TestLoggerContext.Logger)}, " +
                $"Peers: {NodeInfo.GetPeerCount(TestLoggerContext.Logger)}");

            isNodeSynced = NodeInfo.IsFullySynced(TestLoggerContext.Logger);

            if (isNodeSynced)
            {
                TestLoggerContext.Logger.Info("Node is fully synced. Waiting for 10 minutes to check for further errors...");
                Thread.Sleep(600000);
                break;
            }

            bool verificationSuceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSuceeded == true, "Undesired log occurred: " + string.Join(", ", errors));

            Thread.Sleep(10000);
        }
    }
}