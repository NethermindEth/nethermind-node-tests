using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomObjects;
using NethermindNode.Tests.Enums;
using NethermindNode.Tests.Helpers;
using System.Diagnostics;

namespace NethermindNode.Tests.SyncingNode;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class StagesTests : BaseTest
{
    List<Stage> correctOrderOfStages = new List<Stage>()
        {
            new Stage(){ Stages = new List<Stages>(){ Stages.FastHeaders }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
            new Stage(){ Stages = new List<Stages>(){ Stages.FastSync }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
            new Stage(){ Stages = new List<Stages>(){ Stages.SnapSync }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync } },
            new Stage(){ Stages = new List<Stages>(){ Stages.StateNodes }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
            new Stage(){ Stages = new List<Stages>(){ Stages.WaitingForBlock }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
            new Stage(){ Stages = new List<Stages>(){ Stages.FastBodies }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync }, MissingOnNonValidatorNode = true },
            new Stage(){ Stages = new List<Stages>(){ Stages.FastReceipts }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync }, MissingOnNonValidatorNode = true },
            new Stage(){ Stages = new List<Stages>(){ Stages.WaitingForBlock }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync }, ShouldOccureAlone = true }
        };

    int MaxWaitTimeForStageToCompleteInMilliseconds = 36 * 60 * 60 * 1000;

    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    [TestCase(Category = "SnapSync,FastSync,StabilityCheck")]
    public void VerifyCorrectnessOfSyncStages()
    {
        Logger.Info("***Starting test: VerifyCorrectnessOfSyncStages***");
        Enum.TryParse(ConfigurationHelper.Instance["sync-mode"], out SyncTypes syncType);
        
        foreach (var stage in correctOrderOfStages.Where(x => x.SyncTypesApplicable.Contains(syncType)))
        {
            bool isNonValidatorNode = Convert.ToBoolean(ConfigurationHelper.Instance["non-validator-node"]);

            if ((stage.Stages.ToJoinedString() == Stages.FastBodies.ToString() || stage.Stages.ToJoinedString() == Stages.FastReceipts.ToString()) && isNonValidatorNode)
            {
                Logger.Info("Skipping stage: " + stage.Stages.ToJoinedString() + " because nonValidatorNode enabled.");
                continue;
            }

            Logger.Info("Waiting for stage: " + stage.Stages.ToJoinedString());
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var currentStage = NodeInfo.GetCurrentStage(Logger);
            while (stage.ShouldOccureAlone ? currentStage != stage.Stages.ToJoinedString() : !currentStage.Contains(stage.Stages.ToJoinedString()))
            {
                if (sw.ElapsedMilliseconds > MaxWaitTimeForStageToCompleteInMilliseconds)
                {
                    sw.Stop();
                    throw new AssertionException(
                        "Timout while waiting for stage to complete." + " \n" +
                        "Expected stage to be next: " + stage + " \n" +
                        "Current stage is: " + currentStage
                        );
                }
                Thread.Sleep(1000);
                currentStage = NodeInfo.GetCurrentStage(Logger);
            }
            sw.Stop();
            Logger.Info("Stage found! " + stage.Stages.ToJoinedString());
        }
    }
}
