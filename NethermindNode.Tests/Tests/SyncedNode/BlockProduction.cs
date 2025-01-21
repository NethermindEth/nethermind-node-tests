using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;

namespace NethermindNode.Tests.SyncedNode;


[TestFixture]
[Parallelizable(ParallelScope.All)]
public class BlockProduction : BaseTest
{
    [NethermindTest]
    [Category("BlockProductionSimulationVerification")]
    public async Task ShouldVerifyIfBlockProductionSimulationWorksAsync()
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

        Thread.Sleep(120000); //Give it a time to warm up after sync (catching 32 blocks, starting oldBodies etc - it takes short time and in emantime first blocks should appear)

        var blockImprove = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Improved post-merge block");
        Assert.That(blockImprove.Count() > 0 , "No block improvements after sync in simulation mode.");

        var blockProduction = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Produced block");
        Assert.That(blockProduction.Count() > 0, "No block production after sync in simulation mode.");
    }
}
