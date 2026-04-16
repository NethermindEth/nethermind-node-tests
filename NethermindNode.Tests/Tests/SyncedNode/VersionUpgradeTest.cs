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
        private const string UpgradeTargetImageEnvVar = "UPGRADE_TARGET_IMAGE";
        // Legacy fallback
        private const string UpgradeTargetVersionEnvVar = "UPGRADE_TARGET_VERSION";

        [NethermindTest]
        [Description("Sync with initial version, upgrade to target version, verify sync completes successfully.")]
        [Category("VersionUpgrade")]
        public void UpgradeToTargetVersion()
        {
            // Get the target image from environment (set by run-test.sh from the tested branch)
            string? targetImage = Environment.GetEnvironmentVariable(UpgradeTargetImageEnvVar);
            // Legacy fallback: manual version input
            string? targetVersion = Environment.GetEnvironmentVariable(UpgradeTargetVersionEnvVar);

            if (!string.IsNullOrEmpty(targetImage))
            {
                // already set
            }
            else if (!string.IsNullOrEmpty(targetVersion))
            {
                targetImage = $"nethermindeth/nethermind:{targetVersion}";
            }
            else
            {
                string errorMessage = $"Neither {UpgradeTargetImageEnvVar} nor {UpgradeTargetVersionEnvVar} is set. " +
                                     "The upgrade scope must be triggered with a branch that maps to a Docker image.";
                TestLoggerContext.Logger.Error(errorMessage);
                Assert.Fail(errorMessage);
                return;
            }

            string envFilePath = GetEnvFilePath();
            string currentVersion = GetCurrentImageVersion(envFilePath);
            TestLoggerContext.Logger.Info($"[UPGRADE] Starting \u2014 from {currentVersion} to {targetImage}");

            // Phase 1: Wait for initial sync
            TestLoggerContext.Logger.Info("[UPGRADE] Phase 1: Waiting for initial sync...");
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);
            TestLoggerContext.Logger.Info("[UPGRADE] \u2713 Initial sync complete");

            // Extra stability wait after sync
            Thread.Sleep(120000);

            // Phase 2: Perform the upgrade
            TestLoggerContext.Logger.Info($"[UPGRADE] Phase 2: Upgrading {currentVersion} \u2192 {targetImage}");
            UpdateDockerImageVersionInEnvFile(envFilePath, EcImageVersionVariableName, targetImage);
            RestartDockerContainer(
                ConfigurationHelper.Instance["execution-container-name"],
                Path.Combine(Path.GetDirectoryName(envFilePath)!, "docker-compose.yml"),
                TestLoggerContext.Logger
            );
            TestLoggerContext.Logger.Info("[UPGRADE] \u2713 Container restarted with new version");

            // Phase 3: Wait for post-upgrade sync
            TestLoggerContext.Logger.Info("[UPGRADE] Phase 3: Waiting for post-upgrade sync...");
            Thread.Sleep(30000);
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);
            TestLoggerContext.Logger.Info("[UPGRADE] \u2713 Post-upgrade sync complete");

            // Phase 4: Verify health
            TestLoggerContext.Logger.Info("[UPGRADE] Phase 4: Verifying health (10 min)");
            VerifyNoUndesiredLogs(maxIterations: 10, intervalMs: 60000);

            TestLoggerContext.Logger.Info($"[UPGRADE] \u2713 ALL PHASES PASSED \u2014 upgraded from {currentVersion} to {targetImage}");
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
