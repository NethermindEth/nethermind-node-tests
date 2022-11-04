﻿using NethermindNodeTests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Tests.SyncedNode
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class RestartsOnSyncedNode
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        [TestCase(20, 60, 900)]
        [Category("SnapSync")]
        [Category("FastSync")]
        [Category("FullSync")]
        [Category("ArchiveSync")]
        public void ShouldRestartNodeMultipleTimesOnSyncedNode(int restartCount, int minimumWait, int maximumWait)
        {
            Logger.Info("***Starting test: ShouldRestartNodeMultipleTimesOnSyncedNode***");
            FuzzerHelper.Fuzz(new FuzzerCommandOptions { IsFullySyncedCheck = true, Count = restartCount, Minimum = minimumWait, Maximum = maximumWait });
        }
    }
}
