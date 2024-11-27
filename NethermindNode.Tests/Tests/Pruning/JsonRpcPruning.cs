﻿using Nethereum.Merkle.Patricia;
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
        public async Task ShouldPruneDbUsingAdminRpc()
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

            Assert.IsTrue(result.ToLowerInvariant().Contains("starting"), $"Result should contains \"starting\" but it doesn't. Result content: {result}");

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
                "Full Pruning Ready to start: waiting for state",
                "Full Pruning Waiting for state",
                "Full Pruning Ready to start",
                "Full Pruning Started on root hash",
                "Full Pruning In Progress",
                "Full Pruning Finished"
            };

            HashSet<string> missingLogs = new HashSet<string>(expectedLogs);
            List<string> foundLogs = new List<string>();

            try
            {
                await foreach (var line in DockerCommands.GetDockerLogsAsync(ConfigurationHelper.Instance["execution-container-name"], "Full Pruning", true, cts.Token))
                {
                    foreach (var expectedLog in missingLogs)
                    {
                        if (!line.Contains(expectedLog))
                        {
                            continue;
                        }

                        Logger.Info($"Log found: \"{line}\" - Expected log: {expectedLog}");
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
                Logger.Info("Operation was canceled.");
            }

            if (missingLogs.Count > 0)
            {
                // In case some of the logs were not displayed log Warning
                Logger.Warn($"Missing logs: {string.Join(", ", missingLogs)}");
            }

            Assert.That(missingLogs.Count, Is.EqualTo(0), $"Not all expected log substrings were found. Missing logs: {string.Join(", ", missingLogs)}");
            
            stateDirectories = Directory.GetDirectories(statePath);
            Assert.That(stateDirectories.Length, Is.EqualTo(1), "After Pruning directories length should be back on 1.");
        }
    }    
}
