using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NethermindNode.Tests.JsonRpc;
using System.Diagnostics;

namespace NethermindNode.Tests.Tests.Pruning
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class JsonRpcPruning
    {
        [Timeout(172800000)] //48 hours
        [Category("RpcPruning")]
        [NethermindTest]
        public async Task ShouldPruneDbUsingAdminRpc()
        {
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

            string dataPath = DockerCommands.GetExecutionDataPath(TestLoggerContext.Logger);
            string nethermindDbPath = Path.Combine(dataPath, "nethermind_db");
            string network = Directory.GetDirectories(nethermindDbPath).FirstOrDefault();
            Assert.IsNotNull(network, "There is no network directory.");

            string statePath = Path.Combine(network, "state");

            // Check if only one state 
            var stateDirectories = Directory.GetDirectories(statePath);
            Assert.That(stateDirectories.Length, Is.EqualTo(1), "Pruning not yet active so there should be only one state directory.");

            // Execute Prune Command
            var parameters = $"";
            var result = HttpExecutor.ExecuteNethermindJsonRpcCommand("admin_prune", parameters, TestItems.RpcAddress, TestLoggerContext.Logger).Result.Item1;

            Assert.IsTrue(result.ToLowerInvariant().Contains("starting"), $"Result should contains \"starting\" but it doesn't. Result content: {result}");

            // Wait for maximum 60 seconds for pruning to be properly started
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            while (stateDirectories.Length != 2 && stopwatch.Elapsed < TimeSpan.FromSeconds(300))
            {
                System.Threading.Thread.Sleep(500);
                stateDirectories = Directory.GetDirectories(statePath);
            }

            stopwatch.Stop();

            // Verify if second state dir is created
            stateDirectories = Directory.GetDirectories(statePath);
            Assert.IsTrue(stateDirectories.Length == 2, "Pruning active - backup state directory should be created.");

            // Verify Logs
            CancellationTokenSource cts = new CancellationTokenSource();

            string[] expectedLogs =
            {
                "Full Pruning Ready to start: waiting for state",
                "Full Pruning Waiting for state",
                "Full Pruning Ready to start",
                "Full Pruning Started on root hash",
                "Full Pruning Finished",
                "Disposing DB State"
            };

            HashSet<string> missingLogs = new HashSet<string>(expectedLogs);
            List<string> foundLogs = new List<string>();

            try
            {
                await foreach (var line in DockerCommands.GetDockerLogsAsync(ConfigurationHelper.Instance["execution-container-name"], "", true, cts.Token))
                {
                    if (!line.Contains("Full Pruning") && !line.Contains("Disposing"))
                        continue;

                    foreach (var expectedLog in missingLogs)
                    {
                        if (!line.Contains(expectedLog))
                        {
                            continue;
                        }

                        TestLoggerContext.Logger.Info($"Log found: \"{line}\" - Expected log: {expectedLog}");
                        foundLogs.Add(expectedLog);
                        missingLogs.Remove(expectedLog);
                    }

                    if (foundLogs.Count == expectedLogs.Length)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                TestLoggerContext.Logger.Info("Operation was canceled.");
            }

            //Wait for 30 seconds for data to be properly removed.
            Thread.Sleep(30000);
            stateDirectories = Directory.GetDirectories(statePath);
            Assert.That(stateDirectories.Length, Is.EqualTo(1), "After Pruning directories length should be back on 1.");
        }

        [Category("RpcPruningInfinity")]
        [NethermindTest]
        public async Task ShouldPruneDbUsingAdminRpcInfinity()
        {
            while (true)
            {
                await ShouldPruneDbUsingAdminRpc();
            }
        }
    }    
}
