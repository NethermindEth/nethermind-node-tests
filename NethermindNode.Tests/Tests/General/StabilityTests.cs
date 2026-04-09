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

        bool isNodeSynced = false;

        List<string> errors = new List<string>();

        while (!isNodeSynced)
        {
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