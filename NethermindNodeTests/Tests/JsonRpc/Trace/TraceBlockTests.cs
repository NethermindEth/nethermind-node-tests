using CommandLine;
using NethermindNodeTests.CustomObjects;
using NethermindNodeTests.Helpers;
using NethermindNodeTests.RpcResponses;
using Newtonsoft.Json;
using SedgeNodeFuzzer.Helpers;
using System.Text;

namespace NethermindNodeTests.Tests.JsonRpc.Trace
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class TraceBlockTests : BaseTest
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        [TestCase(1, 1, Category = "JsonRpc")]
        [TestCase(1000, 1, Category = "JsonRpcTraceSimple")]
        [TestCase(1000, 1, Category = "JsonRpcBenchmark,JsonRpcBenchmarkSimple")]
        [TestCase(1000, 5, Category = "JsonRpcBenchmark")]
        [TestCase(1000, 10, Category = "JsonRpcBenchmark")]
        [TestCase(100000, 100, Category = "JsonRpcBenchmarkStress")]
        public void TraceBlock(int repeatCount, int parallelizableLevel)
        {
            List<TimeSpan> executionTimes = new List<TimeSpan>();
            Random rnd = new Random();

            Parallel.ForEach(
                Enumerable.Range(0, repeatCount),
                new ParallelOptions { MaxDegreeOfParallelism = parallelizableLevel },
                (task, loopState) =>
                {
                    var tempNum = 16219920;
                    //temp fixed numbers
                    int num = tempNum + task;
                    if (num == 16220067)
                        loopState.Stop();
                    var result = CurlExecutor.ExecuteBenchmarkedNethermindJsonRpcCommand("trace_block", $"\"{num}\"", "http://localhost:8545", Logger);
                    //Test result
                    bool isVerifiedPositively = JsonRpcHelper.DeserializeReponse<TraceBlock>(result.Result.Item1);

                    if (result.Result.Item3 && isVerifiedPositively)
                    {
                        executionTimes.Add(result.Result.Item2);
                    }
                    else
                        Logger.Error(result.Result.Item1);
                });

            Assert.IsNotEmpty(executionTimes, "All requests failed - unable to measeure times of execution.");

            var average = executionTimes.Average(x => x.TotalMilliseconds);
            var totalRequestsSucceeded = executionTimes.Count();
            var min = executionTimes.Min(x => x.TotalMilliseconds);
            var max = executionTimes.Max(x => x.TotalMilliseconds);

            string fileName = $"TraceBlockPerformance_{repeatCount}_{parallelizableLevel}.json";

            BenchmarkedJsonRpcEndpoint result = new BenchmarkedJsonRpcEndpoint()
            {
                EndpointName = "trace_block",
                LevelOfParralelizm = parallelizableLevel,
                AverageTimeInMs = average,
                TotalRequestsExecuted = repeatCount,
                TotalRequestsSucceeded = totalRequestsSucceeded,
                MinimumTimeOfExecution = min,
                MaximumTimeOfExecution = max
            };

            var serializedJson = JsonConvert.SerializeObject(result);
            File.WriteAllText(fileName, serializedJson, Encoding.UTF8);
        }
    }
}