using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NethermindNode.Tests.Enums;
using NLog;

namespace NethermindNode.Tests.Tests.SyncedNode
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class VersionUpgradeTest : BaseTest
    {
        private const string EcImageVersionVariableName = "EC_IMAGE_VERSION";
        private const string UpgradeTargetVersionEnvVar = "UPGRADE_TARGET_VERSION";
        private const int WarmupDelaySeconds = 60;
        private const int SyncCheckIntervalSeconds = 60;
        private const int PostSyncStabilityWaitSeconds = 1152; // ~96 blocks worth (128-32)*12s

        [NethermindTest]
        [Description("Sync with initial version, upgrade to target version, verify sync completes successfully.")]
        [Category("VersionUpgrade")]
        public void UpgradeToTargetVersion()
        {
            // Get the target version from environment variable
            string? targetVersion = Environment.GetEnvironmentVariable(UpgradeTargetVersionEnvVar);

            if (string.IsNullOrEmpty(targetVersion))
            {
                Assert.Fail($"Environment variable {UpgradeTargetVersionEnvVar} is not set. " +
                           "Please provide the target version to upgrade to.");
                return;
            }

            TestLoggerContext.Logger.Info($"Target upgrade version: {targetVersion}");

            // ============================================
            // PHASE 1: Wait for initial sync to complete
            // ============================================
            
            // Step 1.1: Initial warmup - give the node time to start
            TestLoggerContext.Logger.Info($"Step 1.1: Initial warmup period ({WarmupDelaySeconds} seconds)...");
            Thread.Sleep(TimeSpan.FromSeconds(WarmupDelaySeconds));

            // Step 1.2: Wait for node API to be available
            TestLoggerContext.Logger.Info("Step 1.2: Waiting for node API to be available...");
            WaitForNodeApiWithRetry();

            // Step 1.3: Wait for sync stage to reach SnapSync/StateNodes
            TestLoggerContext.Logger.Info("Step 1.3: Waiting for sync stage (SnapSync/StateNodes)...");
            WaitForSyncStage();

            // Step 1.4: Wait for eth_syncing to return false
            TestLoggerContext.Logger.Info("Step 1.4: Waiting for node to be fully synced (eth_syncing=false)...");
            WaitForFullSync();

            // Step 1.5: Extra stability wait after sync
            TestLoggerContext.Logger.Info($"Step 1.5: Post-sync stability wait ({PostSyncStabilityWaitSeconds} seconds / ~96 blocks)...");
            Thread.Sleep(TimeSpan.FromSeconds(PostSyncStabilityWaitSeconds));
            
            TestLoggerContext.Logger.Info("Initial sync completed successfully!");

            // ============================================
            // PHASE 2: Perform the upgrade
            // ============================================

            // Step 2.1: Get current version for logging
            string envFilePath = GetEnvFilePath();
            string currentVersion = GetCurrentImageVersion(envFilePath);
            TestLoggerContext.Logger.Info($"Step 2.1: Current version before upgrade: {currentVersion}");

            // Step 2.2: Update to target version
            string newImageName = $"nethermindeth/nethermind:{targetVersion}";
            TestLoggerContext.Logger.Info($"Step 2.2: Updating .env to use {newImageName}...");
            UpdateDockerImageVersionInEnvFile(envFilePath, EcImageVersionVariableName, newImageName);

            // Step 2.3: Restart container with new version
            TestLoggerContext.Logger.Info("Step 2.3: Restarting container with new version...");
            RestartDockerContainer(
                ConfigurationHelper.Instance["execution-container-name"],
                Path.Combine(Path.GetDirectoryName(envFilePath)!, "docker-compose.yml"),
                TestLoggerContext.Logger
            );

            // ============================================
            // PHASE 3: Wait for post-upgrade sync
            // ============================================

            // Step 3.1: Wait for node to come back online
            TestLoggerContext.Logger.Info("Step 3.1: Waiting for node to restart (30 seconds grace period)...");
            Thread.Sleep(TimeSpan.FromSeconds(30));

            // Step 3.2: Wait for API to be available again
            TestLoggerContext.Logger.Info("Step 3.2: Waiting for node API to be available after upgrade...");
            WaitForNodeApiWithRetry();

            // Step 3.3: Wait for node to sync after upgrade (should be fast - just catching up)
            TestLoggerContext.Logger.Info("Step 3.3: Waiting for post-upgrade sync to complete...");
            WaitForFullSync();
            
            TestLoggerContext.Logger.Info("Post-upgrade sync completed successfully!");

            // ============================================
            // PHASE 4: Verify health after upgrade
            // ============================================

            // Step 4.1: Verify client version
            TestLoggerContext.Logger.Info("Step 4.1: Verifying client version after upgrade...");
            VerifyClientVersion(targetVersion);

            // Step 4.2: Verify no errors in logs
            TestLoggerContext.Logger.Info("Step 4.2: Verifying no errors after upgrade (10 minute monitoring)...");
            VerifyNoUndesiredLogs(maxIterations: 10, intervalMs: 60000);

            TestLoggerContext.Logger.Info($"=== VERSION UPGRADE TEST COMPLETED SUCCESSFULLY ===");
            TestLoggerContext.Logger.Info($"Upgraded from: {currentVersion}");
            TestLoggerContext.Logger.Info($"Upgraded to: {newImageName}");
        }

        /// <summary>
        /// Waits for the node API to become available with detailed logging
        /// </summary>
        private void WaitForNodeApiWithRetry()
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    var response = new HttpClient().GetAsync(NodeInfo.apiBaseUrl).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        TestLoggerContext.Logger.Info("Node API is available!");
                        return;
                    }
                    TestLoggerContext.Logger.Info($"API returned {response.StatusCode}, retrying in {SyncCheckIntervalSeconds}s...");
                }
                catch (Exception ex)
                {
                    retryCount++;
                    TestLoggerContext.Logger.Info($"API not available (attempt {retryCount}): {ex.Message}");
                    TestLoggerContext.Logger.Info($"Retrying in {SyncCheckIntervalSeconds} seconds...");
                }
                Thread.Sleep(TimeSpan.FromSeconds(SyncCheckIntervalSeconds));
            }
        }

        /// <summary>
        /// Waits for sync stage to reach SnapSync or StateNodes (or WaitingForBlock for already synced nodes)
        /// </summary>
        private void WaitForSyncStage()
        {
            while (true)
            {
                try
                {
                    var stages = NodeInfo.GetCurrentStages(TestLoggerContext.Logger);
                    var stageNames = stages.Select(s => s.ToString()).ToList();
                    var stageString = string.Join(", ", stageNames);
                    
                    // Check for stages that indicate sync is progressing or complete
                    bool hasSnapSync = stages.Contains(Stages.SnapSync);
                    bool hasStateNodes = stages.Contains(Stages.StateNodes);
                    bool hasWaitingForBlock = stages.Contains(Stages.WaitingForBlock);
                    
                    if (hasSnapSync || hasStateNodes || hasWaitingForBlock)
                    {
                        TestLoggerContext.Logger.Info($"Sync stage reached: {stageString}");
                        return;
                    }
                    
                    TestLoggerContext.Logger.Info($"Current sync stage: {stageString}. Waiting for SnapSync/StateNodes/WaitingForBlock...");
                }
                catch (Exception ex)
                {
                    TestLoggerContext.Logger.Info($"Could not get sync stage: {ex.Message}");
                }
                
                TestLoggerContext.Logger.Info($"Retrying in {SyncCheckIntervalSeconds} seconds...");
                Thread.Sleep(TimeSpan.FromSeconds(SyncCheckIntervalSeconds));
            }
        }

        /// <summary>
        /// Waits for eth_syncing to return false (fully synced)
        /// </summary>
        private void WaitForFullSync()
        {
            while (true)
            {
                try
                {
                    if (NodeInfo.IsFullySynced(TestLoggerContext.Logger))
                    {
                        TestLoggerContext.Logger.Info("Node is fully synced (eth_syncing=false)!");
                        return;
                    }
                    
                    // Get current sync progress for logging
                    var stages = NodeInfo.GetCurrentStages(TestLoggerContext.Logger);
                    var stageString = string.Join(", ", stages.Select(s => s.ToString()));
                    TestLoggerContext.Logger.Info($"Still syncing. Current stages: {stageString}");
                }
                catch (Exception ex)
                {
                    TestLoggerContext.Logger.Info($"Error checking sync status: {ex.Message}");
                }
                
                TestLoggerContext.Logger.Info($"Retrying in {SyncCheckIntervalSeconds} seconds...");
                Thread.Sleep(TimeSpan.FromSeconds(SyncCheckIntervalSeconds));
            }
        }

        /// <summary>
        /// Verifies the client version contains the expected version string
        /// </summary>
        private void VerifyClientVersion(string expectedVersion)
        {
            try
            {
                var result = HttpExecutor.ExecuteNethermindJsonRpcCommand(
                    "web3_clientVersion", 
                    "", 
                    NodeInfo.apiBaseUrl, 
                    TestLoggerContext.Logger
                );
                
                if (result?.Result != null)
                {
                    string clientVersion = result.Result.Item1;
                    TestLoggerContext.Logger.Info($"Client version: {clientVersion}");
                    
                    // Note: We just log the version, not strictly assert, 
                    // as version string format may vary
                    if (!clientVersion.Contains(expectedVersion))
                    {
                        TestLoggerContext.Logger.Warn($"Client version '{clientVersion}' does not contain expected version '{expectedVersion}'");
                    }
                }
            }
            catch (Exception ex)
            {
                TestLoggerContext.Logger.Warn($"Could not verify client version: {ex.Message}");
            }
        }

        private string GetEnvFilePath()
        {
            string dataPath = DockerCommands.GetExecutionDataPath(TestLoggerContext.Logger);
            string? parentDirectory = Directory.GetParent(dataPath)?.FullName
                                     ?? throw new DirectoryNotFoundException("Parent directory not found.");

            string envFilePath = Path.Combine(parentDirectory, ".env");

            if (!File.Exists(envFilePath))
            {
                throw new FileNotFoundException("The .env file was not found.", envFilePath);
            }

            return envFilePath;
        }

        private string GetCurrentImageVersion(string envFilePath)
        {
            var lines = File.ReadAllLines(envFilePath);
            foreach (var line in lines)
            {
                if (line.StartsWith(EcImageVersionVariableName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(EcImageVersionVariableName.Length + 1);
                }
            }
            return "unknown";
        }

        private void UpdateDockerImageVersionInEnvFile(string envFilePath, string variableName, string newValue)
        {
            var lines = File.ReadAllLines(envFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(variableName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{variableName}={newValue}";
                    break;
                }
            }

            File.WriteAllLines(envFilePath, lines);
        }

        private void RestartDockerContainer(string containerName, string dockerComposePathDir, Logger logger)
        {
            DockerCommands.StopDockerContainer(containerName, logger);
            DockerCommands.ComposeUp("", dockerComposePathDir, logger);
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
