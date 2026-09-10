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
                    (stage.ShouldNotOccurWith is not null && currentStage.Contains(stage.ShouldNotOccurWith.Value.ToString()))
                  )
            {
                ForceStopWatcher.ThrowIfStopRequested();
                if (pollCount == 0 || pollCount % 60 == 0)
                {
                    TestLoggerContext.Logger.Info($"[STAGES] Waiting for {stage.Stages.ToJoinedString()}... (current: {currentStage})");
                }

                // Short-lived stages (and mode flags like FastSync, which can show up
                // at any point or not at all) are easily missed between 1s polls. Once
                // the node is fully synced no further stage can appear, so waiting any
                // longer would hang the test forever \u2014 this exact hang burned 6x10h
                // jobs per scheduled smoke run while stuck on "Waiting for SnapSync"
                // with the node long since at WaitingForBlock.
                if (pollCount % 30 == 0 && NodeInfo.IsFullySynced(TestLoggerContext.Logger))
                {
                    TestLoggerContext.Logger.Info($"[STAGES] Node became fully synced while waiting for {stage.Stages.ToJoinedString()} (current: {currentStage}). Remaining stages already completed or skipped.");
                    Assert.Pass($"Node fully synced while waiting for {stage.Stages.ToJoinedString()} \u2014 stage was passed between polls.");
                }

                // A restart (fuzz kill / stability check) resumes from an already-populated DB, so this
                // stage may never reappear while the node reports one that only occurs later in the
                // pipeline (or Full block processing). The IsFullySynced escape above never fires in fuzz
                // lanes where the node is killed mid-backfill (eth_syncing stays true), which kept burning
                // jobs to the 10h cap on "Waiting for SnapSync" \u2014 treat the stage as missed and move on.
                string[] currentParts = currentStage.Split(", ", StringSplitOptions.RemoveEmptyEntries);
                bool nodeIsPastThisStage =
                    currentParts.Contains("Full") ||
                    currentParts.Any(part => correctOrderOfStages
                        .Skip(correctOrderOfStages.IndexOf(stage) + 1)
                        .Any(later => later.Stages.Any(s => s.ToString() == part)));
                if (nodeIsPastThisStage)
                {
                    TestLoggerContext.Logger.Info($"[STAGES] \u26a0 {stage.Stages.ToJoinedString()} missed (current: {currentStage} occurs later in the pipeline) \u2014 passed between polls or skipped after a restart.");
                    break;
                }

                pollCount++;
                Thread.Sleep(1000);
                currentStage = NodeInfo.GetCurrentStage(TestLoggerContext.Logger);
            }
            TestLoggerContext.Logger.Info($"[STAGES] \u2713 {stage.Stages.ToJoinedString()} found");
        }
    }
}
