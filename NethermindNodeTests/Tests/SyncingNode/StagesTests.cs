﻿using NethermindNodeTests.Enums;
using NethermindNodeTests.Helpers;
using Newtonsoft.Json;
using SedgeNodeFuzzer.Helpers;
using System.Diagnostics;

namespace NethermindNodeTests.Tests.SyncingNode
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class StagesTests
    {
        List<Stage> correctOrderOfStages = new List<Stage>()
            {
                new Stage(){ Stages = new List<Stages>(){ Stages.FastHeaders }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
                new Stage(){ Stages = new List<Stages>(){ Stages.BeaconHeaders }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
                new Stage(){ Stages = new List<Stages>(){ Stages.FastSync }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
                new Stage(){ Stages = new List<Stages>(){ Stages.SnapSync }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync } },
                new Stage(){ Stages = new List<Stages>(){ Stages.StateNodes }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
                new Stage(){ Stages = new List<Stages>(){ Stages.WaitingForBlock }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
                new Stage(){ Stages = new List<Stages>(){ Stages.FastBodies }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
                new Stage(){ Stages = new List<Stages>(){ Stages.FastReceipts }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync } },
                new Stage(){ Stages = new List<Stages>(){ Stages.WaitingForBlock }, SyncTypesApplicable = new List<SyncTypes>(){ SyncTypes.SnapSync, SyncTypes.FastSync }, ShouldOccureAlone = true }
            };

        int MaxWaitTimeForStageToCompleteInMilliseconds = 36 * 60 * 60 * 1000;

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        [TestCase(SyncTypes.SnapSync, Category = "SnapSync")]
        [TestCase(SyncTypes.FastSync, Category = "FastSync")]
        public void VerfiyCorrectnessOfSnapSyncStages(SyncTypes syncType)
        {
            Logger.Info("***Starting test: VerfiyCorrectnessOfSnapSyncStages --- syncType: " + syncType.ToString() + "***");
            foreach (var stage in correctOrderOfStages.Where(x => x.SyncTypesApplicable.Contains(syncType)))
            {
                Logger.Info("Waiting stage: " + stage.Stages.ToJoinedString());
                Stopwatch sw = new Stopwatch();
                sw.Start();

                var currentStage = GetCurrentStage();
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
                    currentStage = GetCurrentStage();
                }
                sw.Stop();
                Logger.Info("Stage found! " + stage.Stages.ToJoinedString());
            }
        }


        private string GetCurrentStage()
        {
            try
            {
                var commandResult = CurlExecutor.ExecuteCommand("debug_getSyncsStage", "http://localhost:8545");
                dynamic output = JsonConvert.DeserializeObject(commandResult.Result.Content.ReadAsStringAsync().Result);
                Logger.Info("Current stage is: " + output.result.currentStage.ToString());
                return output.result.currentStage.ToString();
            }
            catch (Exception e)
            {
                if (e.Message.Contains("No connection could be made because the target machine actively refused it."))
                    return "WaitingForConnection";
                else
                    throw e;
            }
        }
    }
}
