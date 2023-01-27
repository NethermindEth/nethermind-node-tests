using NethermindNode.Core.Helpers;
using NethermindNode.Helpers;
using NethermindNode.Tests.Enums;
using NethermindNode.Tests.Helpers;

namespace NethermindNode.Tests.Resyncs
{
    internal class ResyncsAfterStages
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        [TestCase(10)]
        [Category("ResyncsAfterState")]
        public void ShouldResyncAfterStateSync(int repeatCount)
        {
            var desiredStage = Stages.FastBodies;

            Logger.Info($"***Starting test: ShouldResyncAfterStateSync --- repeatCount: {repeatCount}***");
            for (int i = 0; i < repeatCount; i++)
            {
                //Waiting for proper start of node
                NodeOperations.WaitForNodeToBeReady(Logger);

                //Waiting for OldBodie (stage after state sync)
                while (!NodeOperations.GetCurrentStages(Logger).Contains(desiredStage))
                {
                    Logger.Info("Waiting for node to be synced until stage :" + desiredStage);
                    Thread.Sleep(30000);
                }

                NodeOperations.NodeStop("execution-client", Logger);
                NodeOperations.NodeResync("execution-client", Logger);
                Logger.Info($"Starting a FreshSync. Remaining fresh syncs to be executed: {repeatCount - i - 1}");
            }
        }
    }
}
