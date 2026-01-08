using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NLog;

namespace NethermindNode.Tests.Tests.SyncedNode
{
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
            // Get the target version from environment variable
            string? targetVersion = Environment.GetEnvironmentVariable(UpgradeTargetVersionEnvVar);

            if (string.IsNullOrEmpty(targetVersion))
            {
                Assert.Fail($"Environment variable {UpgradeTargetVersionEnvVar} is not set. " +
                           "Please provide the target version to upgrade to.");
                return;
            }

            TestLoggerContext.Logger.Info($"Target upgrade version: {targetVersion}");

            // Step 1: Wait for initial sync to complete
            TestLoggerContext.Logger.Info("Step 1: Waiting for node to be ready...");
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

            TestLoggerContext.Logger.Info("Step 1: Waiting for initial sync to complete...");
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);
            TestLoggerContext.Logger.Info("Initial sync completed successfully.");

            // Step 2: Get current version for logging
            string envFilePath = GetEnvFilePath();
            string currentVersion = GetCurrentImageVersion(envFilePath);
            TestLoggerContext.Logger.Info($"Current version before upgrade: {currentVersion}");

            // Step 3: Update to target version
            string newImageName = $"nethermindeth/nethermind:{targetVersion}";
            TestLoggerContext.Logger.Info($"Step 2: Upgrading to {newImageName}...");

            UpdateDockerImageVersionInEnvFile(envFilePath, EcImageVersionVariableName, newImageName);

            // Step 4: Restart container with new version
            RestartDockerContainer(
                ConfigurationHelper.Instance["execution-container-name"],
                Path.Combine(Path.GetDirectoryName(envFilePath)!, "docker-compose.yml"),
                TestLoggerContext.Logger
            );

            // Step 5: Wait for node to be ready again
            TestLoggerContext.Logger.Info("Step 3: Waiting for node to be ready after upgrade...");
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

            // Step 6: Wait for node to sync again (should be fast if just catching up)
            TestLoggerContext.Logger.Info("Step 4: Waiting for node to sync after upgrade...");
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);
            TestLoggerContext.Logger.Info("Post-upgrade sync completed successfully.");

            // Step 7: Verify no errors in logs
            TestLoggerContext.Logger.Info("Step 5: Verifying no errors after upgrade...");
            VerifyNoUndesiredLogs(maxIterations: 10, intervalMs: 60000);

            TestLoggerContext.Logger.Info($"Version upgrade test completed successfully: {currentVersion} -> {newImageName}");
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
