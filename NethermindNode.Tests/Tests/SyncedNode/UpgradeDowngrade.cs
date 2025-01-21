using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;

namespace NethermindNode.Tests.Tests.SyncedNode
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class UpgradeDowngrade : BaseTest
    {
        [NethermindTest]
        public void UpgradeToLatest()
        {
            NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

            string dataPath = DockerCommands.GetExecutionDataPath(TestLoggerContext.Logger);
            string parentDirectory = Directory.GetParent(dataPath).FullName;
            string envFilePath = Path.Combine(parentDirectory, ".env");

            if (!File.Exists(envFilePath))
            {
                throw new FileNotFoundException("The .env file was not found.", envFilePath);
            }

            var lines = File.ReadAllLines(envFilePath);

            string variableName = "EC_IMAGE_VERSION";
            string newValue = "nethermind/nethermind:latest";

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(variableName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"{variableName}={newValue}";
                    break; 
                }
            }

            File.WriteAllLines(envFilePath, lines);

            DockerCommands.StopDockerContainer(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger);
            DockerCommands.StartDockerContainer(ConfigurationHelper.Instance["execution-container-name"], TestLoggerContext.Logger);

            int j = 0;
            List<string> errors = new List<string>();
            while (j < 10)
            {
                var verificationSuceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
                Assert.That(verificationSuceeded == true, "Undesired log occurred: " + string.Join(", ", errors));

                j++;
                Thread.Sleep(30000);
            }
        }
    }
}
