using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
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
            new Stage(){ Stages = new List<Stages>(){ Stages.WaitingForBlock }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync }, ShouldNotOccurWith = Stages.FastReceipts, MissingOnNonValidatorNode = true }
        };

    int MaxWaitTimeForStageToCompleteInMilliseconds = 36 * 60 * 60 * 1000;

    [NethermindTestCase(Category = "SnapSync,FastSync,StabilityCheck")]
    public void VerifyCorrectnessOfSyncStages()
    {
        Enum.TryParse(ConfigurationHelper.Instance["sync-mode"], out SyncTypes syncType);
        
        foreach (var stage in correctOrderOfStages.Where(x => x.SyncTypesApplicable.Contains(syncType)))
        {
            bool isNonValidatorNode = Convert.ToBoolean(ConfigurationHelper.Instance["non-validator-node"]);

            if ((stage.Stages.ToJoinedString() == Stages.FastBodies.ToString() || stage.Stages.ToJoinedString() == Stages.FastReceipts.ToString()) && isNonValidatorNode)
            {
                TestLoggerContext.Logger.Info("Skipping stage: " + stage.Stages.ToJoinedString() + " because nonValidatorNode enabled.");
                continue;
            }

            TestLoggerContext.Logger.Info("Waiting for stage: " + stage.Stages.ToJoinedString());
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var currentStage = NodeInfo.GetCurrentStage(TestLoggerContext.Logger);
            while ((stage.ShouldOccureAlone ? currentStage != stage.Stages.ToJoinedString() : !currentStage.Contains(stage.Stages.ToJoinedString())) ||
                   (stage.ShouldNotOccurWith != null && currentStage.Contains(stage.ShouldNotOccurWith.Value.ToString())))
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
                currentStage = NodeInfo.GetCurrentStage(TestLoggerContext.Logger);
            }
            sw.Stop();
            TestLoggerContext.Logger.Info("Stage found! " + stage.Stages.ToJoinedString());
        }
    }
}
