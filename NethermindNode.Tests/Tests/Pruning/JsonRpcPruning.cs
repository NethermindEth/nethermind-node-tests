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

            HashSet<string> missingLogs = new HashSet<string>(expectedLogs);

            try
            {
                foreach (var line in DockerCommands.GetDockerLogs("sedge-execution-client", "Full Pruning", true, cts.Token, "--tail 0"))
                {
                    Console.WriteLine(line); // For visibility during testing

                    foreach (var expectedLog in missingLogs)
                    {
                        if (line.Contains(expectedLog))
                        {
                            Logger.Info($"Log found: \"{line}\" - Expected log: {expectedLog}");
                            missingLogs.Remove(expectedLog);
                        }
                    }

                    if (missingLogs.Count == 0)
                    {
                        cts.Cancel();
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Operation was canceled.");
            }

            if (missingLogs.Count > 0)
            {
                Logger.Warn($"Missing logs: {string.Join(", ", missingLogs)}");
            }

            Assert.That(missingLogs.Count, Is.EqualTo(0), "Not all expected log substrings were found in order.");

        }
    }    
}