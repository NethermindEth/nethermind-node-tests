using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NLog;

namespace NethermindNode.Tests.Tests.SyncedNode
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class UpgradeDowngrade : BaseTest
    {
        private const string EcImageVersionVariableName = "EC_IMAGE_VERSION";

        [NethermindTest]
        [Description("Upgrade from actually installed version to current docker image version.")]
        [Category("UpgradeTest")]
        public void UpgradeFromOldToCurrent()
        {
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

            string envFilePath = GetEnvFilePath();

            UpdateDockerImageVersionInEnvFile(
                envFilePath,
                EcImageVersionVariableName,
                ConfigurationHelper.Instance["current-version-docker-image"]
            );

            RestartDockerContainer(
                ConfigurationHelper.Instance["execution-container-name"],
                TestLoggerContext.Logger
            );

            
            VerifyNoUndesiredLogs(maxIterations: 10, intervalMs: 60000);
        }

        [NethermindTest]
        [Description("Downgrade from release candidate or master to latest stable version.")]
        [Category("DowngradeTest")]
        public void DowngradeToLatest()
        {
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

            string envFilePath = GetEnvFilePath();

            UpdateDockerImageVersionInEnvFile(
                envFilePath,
                EcImageVersionVariableName,
                "nethermind/nethermind:latest"
            );

            RestartDockerContainer(
                ConfigurationHelper.Instance["execution-container-name"],
                TestLoggerContext.Logger
            );

            // Optionally check for specific exceptions in logs after restart
            var version = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], ConfigurationHelper.Instance["latest-nethermind-version"]);
            Assert.That(version.Count() > 0, "Unable to find a version after upgrade");
            
            VerifyNoUndesiredLogs(maxIterations: 10, intervalMs: 60000);
        }

        private string GetEnvFilePath()
        {
            string dataPath = DockerCommands.GetExecutionDataPath(TestLoggerContext.Logger);
            string parentDirectory = Directory.GetParent(dataPath)?.FullName
                                     ?? throw new DirectoryNotFoundException("Parent directory not found.");

            string envFilePath = Path.Combine(parentDirectory, ".env");

            if (!File.Exists(envFilePath))
            {
                throw new FileNotFoundException("The .env file was not found.", envFilePath);
            }

            return envFilePath;
        }

        private void UpdateDockerImageVersionInEnvFile(
            string envFilePath,
            string variableName,
            string newValue
        )
        {
            var lines = File.ReadAllLines(envFilePath);

            for (int i = 0; i < lines.Length; i++)
            {
                // Find the line that starts with "variableName="
                if (lines[i].StartsWith(variableName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{variableName}={newValue}";
                    break;
                }
            }

            File.WriteAllLines(envFilePath, lines);
        }

        private void RestartDockerContainer(string containerName, Logger logger)
        {
            DockerCommands.StopDockerContainer(containerName, logger);
            DockerCommands.StartDockerContainer(containerName, logger);
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
