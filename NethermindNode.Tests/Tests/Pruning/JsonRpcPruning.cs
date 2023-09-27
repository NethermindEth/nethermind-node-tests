using Nethereum.Merkle.Patricia;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.JsonRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        [Test]
        // Requires adding a "JsonRpc.EnabledModules=[Eth,Subscribe,Trace,TxPool,Web3,Personal,Proof,Net,Parity,Health,Rpc,Debug,Admin]" for Nethermind
        public void ShouldPruneDbUsingAdminRpc()
        {
            Logger.Info($"***Starting test: ShouldPruneDbUsingAdminRpc***");

            NodeInfo.WaitForNodeToBeSynced(Logger);

            string dataPath = DockerCommands.GetExecutionDataPath(Logger);
            string nethermindDbPath = Path.Combine(dataPath, "nethermind_db");
            string network = Directory.GetDirectories(nethermindDbPath).FirstOrDefault();
            Assert.IsNotNull(network, "There is no network directory.");

            string statePath = Path.Combine(network, "state");

            // Check if only one state 
            var stateDirectories = Directory.GetDirectories(statePath);
            Assert.That(stateDirectories.Length, Is.EqualTo(1), "Pruning not yet active so there should be only one state directory.");
            Assert.That(stateDirectories[0].Split('/').Last(), Is.EqualTo("0"), "Invalid name of first state directory.");

            // Execute Prune Command
            var parameters = $"";
            var result = HttpExecutor.ExecuteNethermindJsonRpcCommand("admin_prune", parameters, TestItems.RpcAddress, Logger).Result.Item1;

            Assert.IsTrue(result.Contains("Starting"), $"Result should contains \"Starting\" but it doesn't. Result content: {result}");

            // Wait for maximum 60 seconds for pruning to be properly started
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            while (stateDirectories.Length != 2 && stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                System.Threading.Thread.Sleep(500);
                stateDirectories = Directory.GetDirectories(statePath);
            }

            stopwatch.Stop();

            // Verify if second state dir is created
            stateDirectories = Directory.GetDirectories(statePath);
            Assert.IsTrue(stateDirectories.Length == 2, "Pruning active - backup state directory should be created.");
            Assert.IsTrue(stateDirectories[0].Split('/').Last() == "0", $"Invalid name of first state directory. Should be \"0\" but is {stateDirectories[0].Split('/').Last()}");
            Assert.IsTrue(stateDirectories[1].Split('/').Last() == "1", $"Invalid name of second state directory. Should be \"1\" but is {stateDirectories[1].Split('/').Last()}");

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
                    Console.WriteLine(line);

                    if (line.Contains(expectedLogs[expectedLogIndex]))
                    {
                        expectedLogIndex++;
                    }

                    if (expectedLogIndex >= expectedLogs.Length)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation was canceled."); 
            }

            Assert.That(expectedLogIndex, Is.EqualTo(expectedLogs.Length), "Not all expected log substrings were found in order.");
        }
    }    
}