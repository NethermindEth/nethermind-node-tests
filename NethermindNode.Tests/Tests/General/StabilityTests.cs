
using NethermindNode.Core.Helpers;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StabilityTests : BaseTest
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    [Test]
    [Category("StabilityCheck")]
    public void ShouldVerifyThatNodeSyncsWithoutErrors()
    {
        Logger.Info("***Starting test: ShouldVerifyThatNodeSyncsWithoutErrors***");

        NodeInfo.WaitForNodeToBeReady(Logger);

        bool isNodeSynced = false;
        bool hasErrors = false;

        List<string> exceptionLogs = new List<string>();
        List<string> corruptionLogs = new List<string>();

        while (!isNodeSynced && !hasErrors)
        {
            isNodeSynced = NodeInfo.IsFullySynced(Logger);

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
                Logger.Info("Node is fully synced. Waiting for 10 minutes to check for further errors...");
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

        Logger.Info("***Test finished: ShouldVerifyThatNodeSyncsWithoutErrors***");
    }

}
