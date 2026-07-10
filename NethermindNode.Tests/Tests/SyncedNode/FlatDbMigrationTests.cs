using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NLog;

namespace NethermindNode.Tests.Tests.SyncedNode
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class FlatDbMigrationTests : BaseTest
    {
        // Log markers emitted by the ImportFlatDb step / flat Importer (Nethermind.Init/Steps/ImportFlatDb.cs,
        // Nethermind.State.Flat/Importer.cs).
        private const string ImportStartedMarker = "Copying state";
        private const string ImportCompletedMarker = "Flat db copy completed";
        private const string ImportSkippedExistsMarker = "Flat db already exist";
        private const string ImportSkippedNoRootMarker = "skipping flat DB import";
        // TxPool crash reading head state from an empty flat DB — the failure mode of
        // https://github.com/NethermindEth/nethermind/pull/12383 when the import is wrongly skipped.
        private const string EmptyFlatDbCrashMarker = "no longer exists; concurrently removed";

        [Timeout(86400000)] // 24 hours
        [NonParallelizable]
        [Category("FlatDbMigration")]
        [NethermindTest]
        [Description("Migrate a HalfPath-synced node to FlatDb via FlatDb.ImportFromPruningTrieState and verify the import runs, persists and the node recovers. Regression test for the fresh-flat-DB import skip (nethermind#12383).")]
        public void ShouldMigrateHalfPathNodeToFlatDb()
        {
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

            string composeFilePath = GetDockerComposeFilePath();
            EnableFlatDbMigrationInComposeFile(composeFilePath);

            TestLoggerContext.Logger.Info("Recreating execution client with FlatDb migration flags...");
            string executionContainerName = ConfigurationHelper.Instance["execution-container-name"];
            DockerCommands.StopDockerContainer(executionContainerName, TestLoggerContext.Logger);
            DockerCommands.ComposeUp("", composeFilePath, TestLoggerContext.Logger);

            WaitForMigrationToComplete(executionContainerName);

            TestLoggerContext.Logger.Info("Migration completed; waiting for node to catch up on FlatDb...");
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

            VerifyFlatDbIsEnabled();
            VerifyNoUndesiredLogs(maxIterations: 10, intervalMs: 60000);
        }

        private string GetDockerComposeFilePath()
        {
            string dataPath = DockerCommands.GetExecutionDataPath(TestLoggerContext.Logger);
            string parentDirectory = Directory.GetParent(dataPath)?.FullName
                                     ?? throw new DirectoryNotFoundException("Parent directory not found.");

            string composeFilePath = Path.Combine(parentDirectory, "docker-compose.yml");

            if (!File.Exists(composeFilePath))
            {
                throw new FileNotFoundException("The docker-compose.yml file was not found.", composeFilePath);
            }

            return composeFilePath;
        }

        private void EnableFlatDbMigrationInComposeFile(string composeFilePath)
        {
            // Sedge renders EL extra flags as command list items ("- --FlatDb.Enabled=false").
            // The test plan starts the node with stateDesign=HalfPath, so the flag is guaranteed present.
            var lines = File.ReadAllLines(composeFilePath).ToList();
            int flagIndex = lines.FindIndex(l => l.Trim() == "- --FlatDb.Enabled=false");

            Assert.That(flagIndex, Is.GreaterThanOrEqualTo(0),
                $"Expected '- --FlatDb.Enabled=false' in {composeFilePath} (node must start as HalfPath); " +
                "check the test plan's stateDesign parameter.");

            string indentation = lines[flagIndex].Substring(0, lines[flagIndex].IndexOf('-'));
            lines[flagIndex] = lines[flagIndex].Replace("--FlatDb.Enabled=false", "--FlatDb.Enabled=true");
            lines.Insert(flagIndex + 1, $"{indentation}- --FlatDb.ImportFromPruningTrieState=true");

            File.WriteAllLines(composeFilePath, lines);
            TestLoggerContext.Logger.Info("Enabled FlatDb migration flags in docker-compose.yml");
        }

        /// <summary>
        /// Watches the recreated execution container through the three migration stages:
        /// import boot (must log "Copying state"), import completion ("Flat db copy completed",
        /// after which the node exits and docker restarts it), and second boot (skips the import
        /// with "Flat db already exist" and continues as a FlatDb node).
        /// </summary>
        /// <remarks>
        /// The pre-#12383 bug inverts stage one: a fresh flat DB is misdetected as populated, the node
        /// logs "Flat db already exist" without ever importing and later crashes constructing TxPool
        /// ("State ... no longer exists; concurrently removed"). Both signatures fail the test immediately.
        /// </remarks>
        private void WaitForMigrationToComplete(string executionContainerName)
        {
            TimeSpan pollInterval = TimeSpan.FromSeconds(30);
            TimeSpan importStartTimeout = TimeSpan.FromMinutes(30);
            TimeSpan importCompleteTimeout = TimeSpan.FromHours(8);
            TimeSpan secondBootTimeout = TimeSpan.FromMinutes(20);
            TimeSpan prematureSkipGracePeriod = TimeSpan.FromMinutes(3);

            bool importStarted = false;
            bool importCompleted = false;
            DateTime phaseDeadline = DateTime.UtcNow + importStartTimeout;
            DateTime? prematureSkipFirstSeen = null;

            while (true)
            {
                bool skipMarkerSeen = ContainerLogsContain(executionContainerName, ImportSkippedExistsMarker);

                if (ContainerLogsContain(executionContainerName, EmptyFlatDbCrashMarker))
                {
                    Assert.Fail(
                        "Node crashed reading head state from an empty flat DB " +
                        $"(\"{EmptyFlatDbCrashMarker}\") - the import was skipped on a fresh flat DB. " +
                        "This is the regression fixed by nethermind#12383.");
                }

                if (ContainerLogsContain(executionContainerName, ImportSkippedNoRootMarker))
                {
                    Assert.Fail(
                        "Flat DB import was skipped because the pruning trie state does not contain the head " +
                        "state root. This is a test environment issue (not the #12383 regression) - the node " +
                        "should have its head state persisted after a graceful shutdown.");
                }

                if (!importStarted)
                {
                    if (ContainerLogsContain(executionContainerName, ImportStartedMarker))
                    {
                        importStarted = true;
                        phaseDeadline = DateTime.UtcNow + importCompleteTimeout;
                        TestLoggerContext.Logger.Info("Flat DB import started.");
                    }
                    else if (skipMarkerSeen)
                    {
                        prematureSkipFirstSeen ??= DateTime.UtcNow;
                        if (DateTime.UtcNow - prematureSkipFirstSeen > prematureSkipGracePeriod)
                        {
                            Assert.Fail(
                                $"Node logged \"{ImportSkippedExistsMarker}\" on a fresh flat DB without ever " +
                                "starting the import. This is the regression fixed by nethermind#12383 " +
                                "(fresh flat DB misdetected as populated).");
                        }
                    }
                }
                else if (!importCompleted)
                {
                    if (ContainerLogsContain(executionContainerName, ImportCompletedMarker))
                    {
                        importCompleted = true;
                        phaseDeadline = DateTime.UtcNow + secondBootTimeout;
                        TestLoggerContext.Logger.Info("Flat DB import completed; waiting for post-import restart.");
                    }
                }
                else if (skipMarkerSeen)
                {
                    // First occurrence of the skip marker is the post-import boot: the import ran to
                    // completion, the node exited and docker restarted it onto the populated flat DB.
                    TestLoggerContext.Logger.Info("Post-import boot detected the populated flat DB; migration persisted.");
                    return;
                }

                if (DateTime.UtcNow > phaseDeadline)
                {
                    string phase = !importStarted ? $"import start (\"{ImportStartedMarker}\")"
                        : !importCompleted ? $"import completion (\"{ImportCompletedMarker}\")"
                        : $"post-import boot (\"{ImportSkippedExistsMarker}\")";
                    Assert.Fail($"Timed out waiting for {phase}.");
                }

                Thread.Sleep(pollInterval);
            }
        }

        private bool ContainerLogsContain(string containerName, string marker)
        {
            return DockerCommands.GetDockerLogs(containerName, marker).Any(l => !string.IsNullOrEmpty(l));
        }

        private void VerifyFlatDbIsEnabled()
        {
            var configValue = NodeInfo.GetConfigValue(TestLoggerContext.Logger, "FlatDb", "Enabled").Result;
            Assert.That(configValue?.Result?.ToLowerInvariant(), Does.Contain("true"),
                "FlatDb.Enabled should be true after migration.");
        }

        private void VerifyNoUndesiredLogs(int maxIterations, int intervalMs)
        {
            int currentAttempt = 0;
            var errors = new List<string>();

            while (currentAttempt < maxIterations)
            {
                bool verificationSucceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
                Assert.That(
                    verificationSucceeded,
                    $"Undesired log occurred: {string.Join(", ", errors)}"
                );

                currentAttempt++;
                Thread.Sleep(intervalMs);
            }
        }
    }
}
