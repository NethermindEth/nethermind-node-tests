using NethermindNode.Core.Helpers;
using NethermindNode.Helpers;
using NethermindNode.Tests.Helpers;

namespace NethermindNode.Tests.Resyncs
{
    internal class ResyncOnFullSyncedNode
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        [TestCase(10)]
        [Category("Resyncs")]
        public void ShouldResyncAfterFullSync(int repeatCount)
        {
            Logger.Info($"***Starting test: ShouldResyncAfterFullSync --- repeatCount: {repeatCount}***");
            for (int i = 0; i < repeatCount; i++)
            {
                //Waiting for proper start of node
                NodeOperations.WaitForNodeToBeReady(Logger);

                //Waiting for Full Sync
                while (!NodeOperations.IsFullySynced(Logger))
                {
                    Logger.Info("Waiting for node to be fully synced.");
                    Thread.Sleep(30000);
                }

                //Stopping and clearing EL
                NodeOperations.NodeStop("execution-client", Logger);
                NodeOperations.NodeResync("execution-client", Logger);

                Logger.Info($"Starting a FreshSync. Remaining fresh syncs to be executed: {repeatCount - i - 1}");
            }
        }
    }
}