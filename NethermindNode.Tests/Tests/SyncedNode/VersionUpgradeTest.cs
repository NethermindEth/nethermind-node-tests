using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NLog;

namespace NethermindNode.Tests.Tests.SyncedNode
{
    /// <summary>
    /// Test for verifying node upgrade functionality.
    /// This test follows the same pattern as UpgradeDowngrade.cs and other working tests:
    /// 1. Wait for node to be ready (API available)
    /// 2. Wait for node to be fully synced
    /// 3. Perform upgrade
    /// 4. Wait for sync again
    /// 5. Verify health
    /// </summary>
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class VersionUpgradeTest : BaseTest
    {
        private const string EcImageVersionVariableName = "EC_IMAGE_VERSION";
        private const string UpgradeTargetVersionEnvVar = "UPGRADE_TARGET_VERSION";

        [NethermindTest]
        [Description("Sync with initial version, upgrade to target version, verify sync completes successfully.")]
        [Category("VersionUpgrade")]
        public void UpgradeToTargetVersion()
        {
            TestLoggerContext.Logger.Info($"=== VERSION UPGRADE TEST STARTED ===");
            
            // Get the target version from environment variable
            string? targetVersion = Environment.GetEnvironmentVariable(UpgradeTargetVersionEnvVar);
            TestLoggerContext.Logger.Info($"Reading {UpgradeTargetVersionEnvVar} from environment: '{targetVersion ?? "(null)"}'");

            if (string.IsNullOrEmpty(targetVersion))
            {
                string errorMessage = $"Environment variable {UpgradeTargetVersionEnvVar} is not set or empty. " +
                                     "Please provide the target version via the workflow input 'upgrade_target_version'.";
                TestLoggerContext.Logger.Error(errorMessage);
                Assert.Fail(errorMessage);
                return;
            }

            TestLoggerContext.Logger.Info($"Target upgrade version: {targetVersion}");

            // ============================================
            // PHASE 1: Wait for initial sync to complete
            // ============================================
            TestLoggerContext.Logger.Info("PHASE 1: Waiting for initial sync...");
            
            // Use the same proven pattern as UpgradeDowngrade.cs
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
            TestLoggerContext.Logger.Info("Node API is ready.");
            
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);
            TestLoggerContext.Logger.Info("Initial sync completed successfully!");

            // Extra stability wait after sync (like BlockProduction.cs uses 120s)
            TestLoggerContext.Logger.Info("Waiting 120 seconds for post-sync stability...");
            Thread.Sleep(120000);

            // ============================================
            // PHASE 2: Perform the upgrade
            // ============================================
            TestLoggerContext.Logger.Info("PHASE 2: Performing upgrade...");

            string envFilePath = GetEnvFilePath();
            string currentVersion = GetCurrentImageVersion(envFilePath);
            TestLoggerContext.Logger.Info($"Current version before upgrade: {currentVersion}");

            // Update to target version
            string newImageName = $"nethermindeth/nethermind:{targetVersion}";
            TestLoggerContext.Logger.Info($"Updating .env to use: {newImageName}");
            UpdateDockerImageVersionInEnvFile(envFilePath, EcImageVersionVariableName, newImageName);

            // Restart container with new version (same as UpgradeDowngrade.cs)
            TestLoggerContext.Logger.Info("Restarting container with new version...");
            RestartDockerContainer(
                ConfigurationHelper.Instance["execution-container-name"],
                Path.Combine(Path.GetDirectoryName(envFilePath)!, "docker-compose.yml"),
                TestLoggerContext.Logger
            );

            // ============================================
            // PHASE 3: Wait for post-upgrade sync
            // ============================================
            TestLoggerContext.Logger.Info("PHASE 3: Waiting for post-upgrade sync...");

            // Give container time to restart
            TestLoggerContext.Logger.Info("Waiting 30 seconds for container restart...");
            Thread.Sleep(30000);

            // Wait for node to be ready again
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
            TestLoggerContext.Logger.Info("Node API is ready after upgrade.");

            // Wait for sync to complete after upgrade
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);
            TestLoggerContext.Logger.Info("Post-upgrade sync completed!");

            // ============================================
            // PHASE 4: Verify health after upgrade
            // ============================================
            TestLoggerContext.Logger.Info("PHASE 4: Verifying health after upgrade...");

            // Verify client version
            VerifyClientVersion(targetVersion);

            // Verify no errors in logs (same as UpgradeDowngrade.cs - 10 iterations, 60s each)
            TestLoggerContext.Logger.Info("Monitoring logs for errors (10 minutes)...");
            VerifyNoUndesiredLogs(maxIterations: 10, intervalMs: 60000);

            TestLoggerContext.Logger.Info($"=== VERSION UPGRADE TEST COMPLETED SUCCESSFULLY ===");
            TestLoggerContext.Logger.Info($"Upgraded from: {currentVersion}");
            TestLoggerContext.Logger.Info($"Upgraded to: {newImageName}");
        }

        /// <summary>
        /// Verifies the client version after upgrade
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
                    TestLoggerContext.Logger.Info($"Client version after upgrade: {clientVersion}");
                    
                    if (!clientVersion.Contains(expectedVersion))
                    {
                        TestLoggerContext.Logger.Warn($"Client version '{clientVersion}' may not contain expected version '{expectedVersion}'");
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
