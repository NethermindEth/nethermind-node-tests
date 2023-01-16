using NethermindNode.Core.Helpers;
using NethermindNode.Tests.Helpers;

namespace NethermindNode.Tests.SyncedNode
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class RestartsOnSyncedNode : BaseTest
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        [TestCase(20, 60, 900)]
        [Category("SnapSync")]
        [Category("FastSync")]
        [Category("FullSync")]
        [Category("ArchiveSync")]
        public void ShouldRestartNodeMultipleTimesOnSyncedNode(int restartCount, int minimumWait, int maximumWait)
        {
            Logger.Info("***Starting test: ShouldRestartNodeMultipleTimesOnSyncedNode***");

            while (DockerCommands.CheckIfDockerContainerIsCreated("execution-client", Logger) == false)
            {
                Logger.Info(TestContext.CurrentContext.Test.MethodName + " ||| " + "Waiting for Execution to be started.");
                Thread.Sleep(5000);
            }
            
            FuzzerHelper.Fuzz(new FuzzerCommandOptions { IsFullySyncedCheck = true, Count = restartCount, Minimum = minimumWait, Maximum = maximumWait }, Logger);
        }
    }
}
