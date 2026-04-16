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

    [NethermindTestCase(Category = "SnapSync,FastSync,StabilityCheck")]
    public void VerifyCorrectnessOfSyncStages()
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

        // If the node is already fully synced (e.g. test re-run after session timeout),
        // skip stage verification — stages have already passed and won't appear again.
        if (NodeInfo.IsFullySynced(TestLoggerContext.Logger))
        {
            TestLoggerContext.Logger.Info("Node is already fully synced. Skipping stage verification.");
            Assert.Pass("Node already synced — stages already completed in a previous run.");
            return;
        }

        Enum.TryParse(ConfigurationHelper.Instance["sync-mode"], out SyncTypes syncType);

        foreach (var stage in correctOrderOfStages.Where(x => x.SyncTypesApplicable.Contains(syncType)))
        {
            bool isNonValidatorNode = Convert.ToBoolean(ConfigurationHelper.Instance["non-validator-node"]);

            if (stage.MissingOnNonValidatorNode && isNonValidatorNode)
            {
                TestLoggerContext.Logger.Info("[STAGES] Skipping " + stage.Stages.ToJoinedString() + " (nonValidatorNode enabled)");
                continue;
            }

            var currentStage = NodeInfo.GetCurrentStage(TestLoggerContext.Logger);
            int pollCount = 0;
            while (
                    (stage.ShouldOccureAlone ? currentStage != stage.Stages.ToJoinedString() : !currentStage.Contains(stage.Stages.ToJoinedString()))
                    ||
                    (stage.ShouldNotOccurWith != null && currentStage.Contains(stage.ShouldNotOccurWith.Value.ToString()))
                  )
            {
                ForceStopWatcher.ThrowIfStopRequested();
                if (pollCount == 0 || pollCount % 60 == 0)
                {
                    TestLoggerContext.Logger.Info($"[STAGES] Waiting for {stage.Stages.ToJoinedString()}... (current: {currentStage})");
                }
                pollCount++;
                Thread.Sleep(1000);
                currentStage = NodeInfo.GetCurrentStage(TestLoggerContext.Logger);
            }
            TestLoggerContext.Logger.Info($"[STAGES] \u2713 {stage.Stages.ToJoinedString()} found");
        }
    }
}
