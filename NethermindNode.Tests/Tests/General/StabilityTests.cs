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
        bool hasErrors = false;

        List<string> exceptionLogs = new List<string>();
        List<string> corruptionLogs = new List<string>();

        while (!isNodeSynced && !hasErrors)
        {
            isNodeSynced = NodeInfo.IsFullySynced(TestLoggerContext.Logger);

            var exceptions = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Exception");
            if (exceptions.Any())
            {
                hasErrors = true;
                exceptionLogs.AddRange(exceptions); 
            }

            // Capture any corruption logs
            var corruption = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Corruption");
            if (corruption.Any())
            {
                hasErrors = true;
                corruptionLogs.AddRange(corruption); 
            }

            if (isNodeSynced)
            {
                TestLoggerContext.Logger.Info("Node is fully synced. Waiting for 10 minutes to check for further errors...");
                Thread.Sleep(600000); 
                break;
            }

            Thread.Sleep(10000);
        }

        if (hasErrors)
        {
            if (exceptionLogs.Any())
            {
                Assert.Fail("Exceptions occurred: " + string.Join(", ", exceptionLogs));
            }

            if (corruptionLogs.Any())
            {
                Assert.Fail("Corruption occurred: " + string.Join(", ", corruptionLogs));
            }
        }
        else
        {
            Assert.IsTrue(isNodeSynced, "Node did not sync successfully.");
        }
    }
}