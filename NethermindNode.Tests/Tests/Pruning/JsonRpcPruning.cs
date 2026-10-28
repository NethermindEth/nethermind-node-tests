using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NethermindNode.Tests.Helpers;
using NethermindNode.Tests.JsonRpc;
using System.Diagnostics;

namespace NethermindNode.Tests.Tests.Pruning
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class JsonRpcPruning
    {
        // Unix time (seconds) at which the lane's test budget runs out; set by the harness through tests.service.
        private const string LaneDeadlineEnvVar = "TESTS_DEADLINE_EPOCH";
        // Used when no lane deadline is passed (local runs, older harness).
        private static readonly TimeSpan DefaultFullPruningTimeout = TimeSpan.FromHours(24);
        // Removing the old state directory is the behaviour under test; how fast a large DB unlinks is not.
        private static readonly TimeSpan StateDirectoryCleanupTimeout = TimeSpan.FromMinutes(15);
        // Left to the lane after this test for collecting logs and reporting.
        private static readonly TimeSpan LaneReportingMargin = TimeSpan.FromMinutes(15);

        [Timeout(172800000)] //48 hours
        [Category("RpcPruning")]
        [NethermindTest]
        public async Task ShouldPruneDbUsingAdminRpc()
        {
            NodeInfo.WaitForNodeToBeSynced(TestLoggerContext.Logger);

            string dataPath = DockerCommands.GetExecutionDataPath(TestLoggerContext.Logger);
            string nethermindDbPath = Path.Combine(dataPath, "nethermind_db");
            string network = Directory.GetDirectories(nethermindDbPath).FirstOrDefault();
            Assert.That(network != null, "There is no network directory.");

            string statePath = Path.Combine(network, "state");

            // Check if only one state 
            var stateDirectories = Directory.GetDirectories(statePath);
            Assert.That(stateDirectories.Length, Is.EqualTo(1), "Pruning not yet active so there should be only one state directory.");

            // Execute Prune Command
            var parameters = $"";
            var result = HttpExecutor.ExecuteNethermindJsonRpcCommand("admin_prune", parameters, TestItems.RpcAddress, TestLoggerContext.Logger).Result.Item1;

            Assert.That(result.ToLowerInvariant().Contains("starting"), $"Result should contains \"starting\" but it doesn't. Result content: {result}");

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
            Assert.That(stateDirectories.Length == 2, "Pruning active - backup state directory should be created.");

            // Verify Logs
            DateTime pruningDeadline = GetFullPruningDeadline();
            TimeSpan pruningBudget = pruningDeadline - DateTime.UtcNow;
            Assert.That(pruningBudget, Is.GreaterThan(TimeSpan.Zero), $"No time left in the lane budget to wait for full pruning (deadline {pruningDeadline:u}).");

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ForceStopWatcher.Token);
            cts.CancelAfter(pruningBudget);

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
            string? lastPruningLog = null;

            try
            {
                await foreach (var line in DockerCommands.GetDockerLogsAsync(ConfigurationHelper.Instance["execution-container-name"], "", true, cts.Token))
                {
                    if (!line.Contains("Full Pruning") && !line.Contains("Disposing"))
                        continue;

                    if (line.Contains("Full Pruning"))
                        lastPruningLog = line;

                    if (line.Contains("Full Pruning Failed") || line.Contains("Full Pruning Cancelled"))
                        Assert.Fail($"Full pruning aborted: {line}");

                    foreach (var expectedLog in missingLogs.ToList())
                    {
                        if (!line.Contains(expectedLog))
                        {
                            continue;
                        }

                        TestLoggerContext.Logger.Info($"Log found: \"{line}\" - Expected log: {expectedLog}");
                        missingLogs.Remove(expectedLog);
                    }

                    if (missingLogs.Count == 0)
                    {
                        break;
                    }
                }
            }
            catch (Exception e) when (cts.IsCancellationRequested && e is not AssertionException)
            {
                ForceStopWatcher.ThrowIfStopRequested();
                Assert.Fail($"Full pruning did not finish within {pruningBudget.TotalMinutes:F0} min (deadline {pruningDeadline:u}). Missing logs: {string.Join(", ", missingLogs)}. Last progress: {lastPruningLog ?? "none"}");
            }

            Assert.That(missingLogs, Is.Empty, $"Execution client log stream ended before full pruning finished. Last progress: {lastPruningLog ?? "none"}");

            DateTime cleanupDeadline = DateTime.UtcNow + StateDirectoryCleanupTimeout;
            stateDirectories = Directory.GetDirectories(statePath);
            while (stateDirectories.Length != 1 && DateTime.UtcNow < cleanupDeadline)
            {
                ForceStopWatcher.ThrowIfStopRequested();
                await Task.Delay(TimeSpan.FromSeconds(10));
                stateDirectories = Directory.GetDirectories(statePath);
            }

            Assert.That(stateDirectories.Length, Is.EqualTo(1), $"After pruning only one state directory should remain, but {StateDirectoryCleanupTimeout.TotalMinutes:F0} min after \"Disposing DB State\" found: [{string.Join(", ", stateDirectories)}]");
        }

        private static DateTime GetFullPruningDeadline()
        {
            if (long.TryParse(Environment.GetEnvironmentVariable(LaneDeadlineEnvVar), out long laneDeadlineEpoch) && laneDeadlineEpoch > 0)
            {
                return DateTimeOffset.FromUnixTimeSeconds(laneDeadlineEpoch).UtcDateTime - StateDirectoryCleanupTimeout - LaneReportingMargin;
            }

            return DateTime.UtcNow + DefaultFullPruningTimeout;
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
