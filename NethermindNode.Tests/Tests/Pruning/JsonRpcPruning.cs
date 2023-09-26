using Nethereum.Merkle.Patricia;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.JsonRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNode.Tests.Tests.Pruning
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class JsonRpcPruning
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        [Timeout(172800000)] //48 hours
        [Category("RpcPruning")]
        public void ShoudlPruneDbUsingAdminRpc()
        {
            Logger.Info($"***Starting test: ShoudlPruneDbUsingAdminRpc***");

            NodeInfo.WaitForNodeToBeReady(Logger);

            string dataPath = DockerCommands.GetExecutionDataPath(Logger);
            string nethermindDbPath = Path.Combine(dataPath, "nethermind_db");
            string network = Directory.GetDirectories(nethermindDbPath).FirstOrDefault();
            Assert.IsNotNull(network, "There is no network directory.");

            string statePath = Path.Combine(network, "state");

            // Check if only one state 
            var stateDirectories = Directory.GetDirectories(statePath);
            Assert.IsTrue(stateDirectories.Length == 1, "Pruning not yet active so there should be only one state directory.");
            Assert.IsTrue(Path.GetFileName(stateDirectories[0]) == "0", "Invalid name of first state directory.");

            // Execute Prune Command
            var parameters = $"";
            var result = HttpExecutor.ExecuteNethermindJsonRpcCommand("admin_prune", parameters, TestItems.RpcAddress, Logger).Result.Item1;

            Assert.IsTrue(result.Contains("Starting"));

            // Wait for 10 seconds for pruning to be properly started
            Thread.Sleep(10000);

            // Verify if second state dir is created
            stateDirectories = Directory.GetDirectories(statePath);
            Assert.IsTrue(stateDirectories.Length == 2, "Pruning active - backup state directory should be created.");
            Assert.IsTrue(Path.GetFileName(stateDirectories[0]) == "0", "Invalid name of first state directory.");
            Assert.IsTrue(Path.GetFileName(stateDirectories[1]) == "1", "Invalid name of second state directory.");

            // Verify Logs
            CancellationTokenSource cts = new CancellationTokenSource();

            string[] expectedLogs =
            {
                "Full Pruning Persist Cache started",
                "Full Pruning Ready to start: waiting for state",
                "Full Pruning Persist Cache in progress",
                "Full Pruning Persist Cache finished",
                "Full Pruning Waiting for state",
                "Full Pruning Waiting for block",
                "Full Pruning Ready to start",
                "Full Pruning Started on root hash",
                "Full Pruning In Progress",
                "Full Pruning Finished"
            };

            int expectedLogIndex = 0;

            try
            {
                foreach (var line in DockerCommands.GetDockerLogs("sedge-execution-client", "Full Pruning", true, cts.Token))
                {
                    Console.WriteLine(line);  // For visibility during testing

                    // If current line contains the expected log substring
                    if (line.Contains(expectedLogs[expectedLogIndex]))
                    {
                        expectedLogIndex++;
                    }

                    // If we've found all expected log substrings
                    if (expectedLogIndex >= expectedLogs.Length)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation was canceled.");  // This can be omitted or replaced with some assertion based on your test needs
            }

            // Assert that we found all expected log substrings in order
            Assert.That(expectedLogIndex, Is.EqualTo(expectedLogs.Length), "Not all expected log substrings were found in order.");
        }
    }    
}