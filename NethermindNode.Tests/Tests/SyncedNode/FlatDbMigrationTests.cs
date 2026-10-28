using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NethermindNode.Tests.Helpers;
using NLog;

namespace NethermindNode.Tests.Tests.SyncedNode
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class FlatDbMigrationTests : BaseTest
    {
        private const string ImportFlagLine = "- --FlatDb.ImportFromPruningTrieState=true";

        // Log markers emitted by the ImportFlatDb step / flat Importer (Nethermind.Init/Steps/ImportFlatDb.cs,
        // Nethermind.State.Flat/Importer.cs).
        private const string ImportStartedMarker = "Copying state";
        private const string ImportCompletedMarker = "Flat db copy completed";
        // Since nethermind#13715 the step refuses to run instead of skipping: every refusal is an
        // InvalidConfigurationException whose message starts with this, and the process exits non-zero.
        private const string ImportRefusedMarker = "Cannot import:";
        private const string ImportRefusedPopulatedMarker = "Cannot import: the flat DB is already populated";
        // Before nethermind#13715 the same cases skipped the import and booted on.
        private const string LegacyImportSkippedExistsMarker = "Flat db already exist";
        private const string LegacyImportSkippedNoRootMarker = "skipping flat DB import";
        // TxPool crash reading head state from an empty flat DB — the failure mode of
        // https://github.com/NethermindEth/nethermind/pull/12383 when the import is wrongly skipped.
        private const string EmptyFlatDbCrashMarker = "no longer exists; concurrently removed";

        [Timeout(86400000)] // 24 hours
        [NonParallelizable]
        [Category("FlatDbMigration")]
        [NethermindTest]
        [Description("Migrate a HalfPath-synced node to FlatDb via FlatDb.ImportFromPruningTrieState and verify the import runs, exits cleanly and the node then syncs on the flat DB. Regression test for the fresh-flat-DB import skip (nethermind#12383).")]
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
            // The import run copies the state and exits; it is not a node start. Left to the compose restart
            // policy it would be rebooted with the import flag still set, which Nethermind rejects ("Cannot
            // import: the flat DB is already populated") in a crash loop. Recreating the container below
            // restores the compose restart policy.
            DockerCommands.PreventDockerContainerRestart(executionContainerName, TestLoggerContext.Logger);

            WaitForImportRunToExit(executionContainerName);

            DisableFlatDbImportInComposeFile(composeFilePath);
            TestLoggerContext.Logger.Info("Recreating execution client as a FlatDb node...");
            DockerCommands.ComposeUp("", composeFilePath, TestLoggerContext.Logger);

            TestLoggerContext.Logger.Info("Migration completed; waiting for node to catch up on FlatDb...");
            WaitForNodeToBeSyncedOnFlatDb(executionContainerName);

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
            lines.Insert(flagIndex + 1, $"{indentation}{ImportFlagLine}");

            File.WriteAllLines(composeFilePath, lines);
            TestLoggerContext.Logger.Info("Enabled FlatDb migration flags in docker-compose.yml");
        }

        private void DisableFlatDbImportInComposeFile(string composeFilePath)
        {
            // The import flag makes every start an import run, so it must go once the import has finished
            // (FlatDb.ImportFromPruningTrieState: "Remove the setting once the import has finished").
            var lines = File.ReadAllLines(composeFilePath).ToList();
            int removed = lines.RemoveAll(l => l.Trim() == ImportFlagLine);

            Assert.That(removed, Is.EqualTo(1), $"Expected exactly one '{ImportFlagLine}' in {composeFilePath}.");

            File.WriteAllLines(composeFilePath, lines);
            TestLoggerContext.Logger.Info("Removed FlatDb import flag from docker-compose.yml");
        }

        /// <summary>
        /// Watches the recreated execution container through the import run: it must log "Copying state",
        /// then "Flat db copy completed", then exit with code 0. Restarts are disabled for this container,
        /// so any exit before the completion marker is the import run failing.
        /// </summary>
        /// <remarks>
        /// The pre-#12383 bug misdetects a fresh flat DB as populated, so the import never starts: Nethermind
        /// builds since #13715 refuse with "Cannot import: the flat DB is already populated" and exit, older
        /// ones log "Flat db already exist" and later crash constructing TxPool ("State ... no longer exists;
        /// concurrently removed"). All of these signatures fail the test.
        /// </remarks>
        private void WaitForImportRunToExit(string executionContainerName)
        {
            TimeSpan pollInterval = TimeSpan.FromSeconds(30);
            TimeSpan importStartTimeout = TimeSpan.FromMinutes(30);
            TimeSpan importCompleteTimeout = TimeSpan.FromHours(8);
            TimeSpan importExitTimeout = TimeSpan.FromMinutes(10);
            TimeSpan prematureSkipGracePeriod = TimeSpan.FromMinutes(3);

            bool importStarted = false;
            bool importCompleted = false;
            DateTime phaseDeadline = DateTime.UtcNow + importStartTimeout;
            DateTime? prematureSkipFirstSeen = null;

            while (true)
            {
                ForceStopWatcher.ThrowIfStopRequested();

                // State before logs: once the container is seen stopped, the snapshot below holds all its output.
                string state = GetContainerState(executionContainerName);
                bool exited = state is "exited" or "dead";

                // One unfiltered snapshot per poll, matched in-process: GetDockerLogs' shell grep
                // mis-tokenizes multi-word filters (nested quotes), and a single snapshot keeps all
                // marker checks consistent with each other.
                List<string> logSnapshot = DockerCommands.GetDockerLogs(
                    executionContainerName, null, false, null, "--tail 100000").ToList();
                string? FindLine(string marker) => logSnapshot.FirstOrDefault(l => l is not null && l.Contains(marker));
                bool LogsContain(string marker) => FindLine(marker) is not null;

                if (LogsContain(EmptyFlatDbCrashMarker))
                {
                    Assert.Fail(
                        "Node crashed reading head state from an empty flat DB " +
                        $"(\"{EmptyFlatDbCrashMarker}\") - the import was skipped on a fresh flat DB. " +
                        "This is the regression fixed by nethermind#12383.");
                }

                string? refusal = FindLine(ImportRefusedMarker);
                if (refusal is not null)
                {
                    Assert.Fail(!importStarted && refusal.Contains(ImportRefusedPopulatedMarker)
                        ? "Nethermind refused to import into a fresh flat DB as already populated - the regression " +
                          $"fixed by nethermind#12383 (fresh flat DB misdetected as populated): {refusal.Trim()}"
                        : $"Nethermind refused the flat DB import: {refusal.Trim()}");
                }

                if (LogsContain(LegacyImportSkippedNoRootMarker))
                {
                    Assert.Fail(
                        "Flat DB import was skipped because the pruning trie state does not contain the head " +
                        "state root. This is a test environment issue (not the #12383 regression) - the node " +
                        "should have its head state persisted after a graceful shutdown.");
                }

                if (!importStarted)
                {
                    if (LogsContain(ImportStartedMarker))
                    {
                        importStarted = true;
                        phaseDeadline = DateTime.UtcNow + importCompleteTimeout;
                        TestLoggerContext.Logger.Info("Flat DB import started.");
                    }
                    else if (LogsContain(LegacyImportSkippedExistsMarker))
                    {
                        prematureSkipFirstSeen ??= DateTime.UtcNow;
                        if (DateTime.UtcNow - prematureSkipFirstSeen > prematureSkipGracePeriod)
                        {
                            Assert.Fail(
                                $"Node logged \"{LegacyImportSkippedExistsMarker}\" on a fresh flat DB without ever " +
                                "starting the import. This is the regression fixed by nethermind#12383 " +
                                "(fresh flat DB misdetected as populated).");
                        }
                    }
                }

                if (importStarted && !importCompleted && LogsContain(ImportCompletedMarker))
                {
                    importCompleted = true;
                    phaseDeadline = DateTime.UtcNow + importExitTimeout;
                    TestLoggerContext.Logger.Info("Flat DB import completed; waiting for the import run to exit.");
                }

                if (exited)
                {
                    string exitCode = DockerCommands.GetDockerDetails(
                        executionContainerName, "{{.State.ExitCode}}", TestLoggerContext.Logger).Trim();

                    Assert.That(importCompleted,
                        $"Import run exited with code {exitCode} before logging \"{ImportCompletedMarker}\" " +
                        $"(import started: {importStarted}).");
                    Assert.That(exitCode, Is.EqualTo("0"),
                        $"Import run logged \"{ImportCompletedMarker}\" but exited with code {exitCode}.");

                    TestLoggerContext.Logger.Info("Flat DB import run exited cleanly.");
                    return;
                }

                if (DateTime.UtcNow > phaseDeadline)
                {
                    string phase = !importStarted ? $"import start (\"{ImportStartedMarker}\")"
                        : !importCompleted ? $"import completion (\"{ImportCompletedMarker}\")"
                        : $"the import run to exit (container state: {state})";
                    Assert.Fail($"Timed out waiting for {phase}.");
                }

                Thread.Sleep(pollInterval);
            }
        }

        /// <summary>
        /// NodeInfo's waits poll forever; this one also stops on a force-stop or a health-verification failure
        /// and on the FlatDb node exiting, so a broken post-migration boot fails with its cause.
        /// </summary>
        private void WaitForNodeToBeSyncedOnFlatDb(string executionContainerName)
        {
            int iteration = 0;
            while (true)
            {
                ForceStopWatcher.ThrowIfStopRequested();

                string state = GetContainerState(executionContainerName);
                Assert.That(state, Is.Not.EqualTo("exited").And.Not.EqualTo("dead"),
                    "FlatDb node stopped after the migration.");

                bool synced;
                try
                {
                    synced = NodeInfo.IsFullySynced(TestLoggerContext.Logger);
                }
                catch (Exception)
                {
                    synced = false; // RPC not up yet
                }

                if (synced)
                {
                    TestLoggerContext.Logger.Info("FlatDb node is synced.");
                    return;
                }

                if (iteration++ % 10 == 0)
                    TestLoggerContext.Logger.Info("Waiting for FlatDb node to be fully synced...");
                Thread.Sleep(TimeSpan.FromSeconds(30));
            }
        }

        // GetDockerContainerStatus single-quotes its template, which reaches docker verbatim (no shell) and
        // comes back in the output; GetDockerDetails double-quotes it, so the state compares cleanly.
        private static string GetContainerState(string containerName) =>
            DockerCommands.GetDockerDetails(containerName, "{{.State.Status}}", TestLoggerContext.Logger).Trim();

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
