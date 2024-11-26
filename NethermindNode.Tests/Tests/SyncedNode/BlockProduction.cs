using NethermindNode.Core.Helpers;

namespace NethermindNode.Tests.SyncedNode;


[TestFixture]
[Parallelizable(ParallelScope.All)]
public class BlockProduction : BaseTest
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    [Test]
    [Category("BlockProductionSimulationVerification")]
    public async Task ShouldVerifyIfBlockProductionSimulationWorksAsync()
    {
        Logger.Info($"***Starting test: ShouldVerifyIfBlockProductionSimulationWorks***");

        NodeInfo.WaitForNodeToBeReady(Logger);
        NodeInfo.WaitForNodeToBeSynced(Logger);

        Thread.Sleep(120000); //Give it a time to warm up after sync (catching 32 blocks, starting oldBodies etc - it takes short time and in emantime first blocks should appear)

        var blockImprove = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Improved post-merge block");
        Assert.IsNotEmpty(blockImprove, "No block improvements after sync in simulation mode.");

        var blockProduction = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Produced block");
        Assert.IsNotEmpty(blockProduction, "No block production after sync in simulation mode.");
    }
}
