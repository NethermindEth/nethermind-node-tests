using NethermindNode.Core.Helpers;

namespace NethermindNode.Tests.SyncingNode
{
    [TestFixture]
    public class BodiesAndReceiptsTests : BaseTest
    {

        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        [TestCase(10)]
        [Category("BodiesAndReceipts")]
        [Description(
            """
                !!!!! Test to be used when NonValidator mode is true. !!!!!

                1. Wait for node to be synced (eth_syncing returns false)
                2. Stop a execution node
                3. Create a backup of database
                4. Restart node but without NonValidator node flags
                5. Wait until end of sync (eth_syncing returns false)
                6. Check if logs "Fast blocks bodies task completed." and "Fast blocks receipts task completed." are there - those two should always been present meaning that tasks are properly finished.
                7. Stop a node, restore backup, redo steps 5 and 6

                WHY?
                If there is no such logs, there is high chance we "lost" something and we should investigate what is missing 
            """
            )]
        public void ShouldResyncBodiesAndReceiptsAfterNonValidator(int repeatCount)
        {
            Logger.Info("***Starting test: ShouldResyncBodiesAndReceiptsAfterNonValidator***");

            var execPath = GetExecutionDataPath();

            // 1
            NodeInfo.WaitForNodeToBeReady(Logger);
            NodeInfo.WaitForNodeToBeSynced(Logger);

            // 2
            DockerCommands.StopDockerContainer(ConfigurationHelper.Instance["execution-container-name"], Logger);

            // 3
            CommandExecutor.BackupDirectory(execPath + "/nethermind_db", execPath + "/nethermind_db_backup" , Logger);

            // 4
            string[] flagsToRemove =
            {
                "--Sync.NonValidatorNode=true",
                "--Sync.DownloadBodiesInFastSync=false",
                "--Sync.DownloadReceiptsInFastSync=false"
            };

            Logger.Info("Reading docker compose file at path: " + execPath + "/../docker-compose.yml");
            var dockerCompose = DockerComposeHelper.ReadDockerCompose(execPath + "/../docker-compose.yml");
            foreach (var flag in flagsToRemove)
            {
                Console.WriteLine("Removing: " + flag);
                DockerComposeHelper.RemoveCommandFlag(dockerCompose, "execution", flag);
            }
            DockerComposeHelper.WriteDockerCompose(dockerCompose, execPath + "/../docker-compose.yml");
            DockerCommands.RecreateDockerCompose("execution", execPath + "/../docker-compose.yml", Logger);
            DockerCommands.StartDockerContainer(ConfigurationHelper.Instance["execution-container-name"], Logger);

            // 5-6-7
            for (int i = 0; i < repeatCount; i++)
            {
                NodeInfo.WaitForNodeToBeReady(Logger);
                NodeInfo.WaitForNodeToBeSynced(Logger);
                var bodiesLine = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Fast blocks bodies task completed.");
                var receiptsLine = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Fast blocks receipts task completed.");

                Assert.IsTrue(bodiesLine.Count() > 0, "Bodies log line missing - verify with getBlockByNumber.");
                Assert.IsTrue(receiptsLine.Count() > 0, "Receipts log line missing - verify with getReceipt");

                DockerCommands.StopDockerContainer(ConfigurationHelper.Instance["execution-container-name"], Logger);

                CommandExecutor.BackupDirectory(execPath + "/nethermind_db_backup", execPath + "/nethermind_db", Logger);

                DockerCommands.StartDockerContainer(ConfigurationHelper.Instance["execution-container-name"], Logger);
            }

            // Restore to previous state
            foreach (var flag in flagsToRemove)
            {
                Console.WriteLine("Adding: " + flag);
                DockerComposeHelper.AddCommandFlag(dockerCompose, "execution", flag);
            }
            DockerComposeHelper.WriteDockerCompose(dockerCompose, execPath + "/../docker-compose.yml");
            DockerCommands.RecreateDockerCompose("execution", execPath + "/../docker-compose.yml", Logger);
            DockerCommands.StopDockerContainer(ConfigurationHelper.Instance["execution-container-name"], Logger);
            DockerCommands.StartDockerContainer(ConfigurationHelper.Instance["execution-container-name"], Logger);
        }

        private string GetExecutionDataPath()
        {
            return DockerCommands.GetDockerDetails(ConfigurationHelper.Instance["execution-container-name"], "{{ range .Mounts }}{{ if eq .Destination \\\"/nethermind/data\\\" }}{{ .Source }}{{ end }}{{ end }}", Logger).Trim(); ;
        }
    }
}
