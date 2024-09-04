
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
        while (!NodeInfo.IsFullySynced(Logger))
        {
            var exceptions = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Exception");
            Assert.IsEmpty(exceptions, "Exception occured: " + exceptions.Select(x => x));

            var corruption = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Corrupted");
            Assert.IsEmpty(corruption, "Corruption occured: " + exceptions.Select(x => x));
        }

        Logger.Info("***Test finished: ShouldVerifyThatNodeSyncsWithoutErrors***");
    }
}
