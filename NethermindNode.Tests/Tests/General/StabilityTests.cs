
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

        bool isNodeSynced = NodeInfo.IsFullySynced(Logger);
        while (!isNodeSynced)
        {
            isNodeSynced = NodeInfo.IsFullySynced(Logger);
            if (isNodeSynced)
        {
                isNodeSynced = true;
                Thread.Sleep(600000); // Maybe small hack - wait for 10 minutes after node is synced to let it process a few blocks and then check for exceptions as a very last check
                                      // Also it is nice because some real tests can be started in meantime and be relatively short so there is a chance that stability test will catch some exceptions caused by other tests - this wil still mean that node is not stable
            }
            var exceptions = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Exception");
            Assert.IsEmpty(exceptions, "Exception occured: " + exceptions.Select(x => x));

            var corruption = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Corrupted");
            Assert.IsEmpty(corruption, "Corruption occured: " + exceptions.Select(x => x));

        }

        Logger.Info("***Test finished: ShouldVerifyThatNodeSyncsWithoutErrors***");
    }
}
