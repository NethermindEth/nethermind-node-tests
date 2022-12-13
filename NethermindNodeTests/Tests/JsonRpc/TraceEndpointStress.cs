using CommandLine;
using NethermindNodeTests.CustomObjects;
using NethermindNodeTests.RpcResponses;
using Newtonsoft.Json;
using SedgeNodeFuzzer.Helpers;
using System.Text;

namespace NethermindNodeTests.Tests.JsonRpc
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class TraceEndpointStress : BaseTest
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        [TestCase(1, 1, Category = "JsonRpc")]
        [TestCase(1000, 1, Category = "JsonRpcBenchmark")]
        [TestCase(1000, 5, Category = "JsonRpcBenchmark")]
        [TestCase(1000, 10, Category = "JsonRpcBenchmark")]
        public async Task TraceBlock(int repeatCount, int parallelizableLevel)
        {
            List<TimeSpan> executionTimes = new List<TimeSpan>();

            Parallel.ForEach(
                Enumerable.Range(0, repeatCount),
                new ParallelOptions { MaxDegreeOfParallelism = parallelizableLevel },
                (task) =>
                {
                    var result = CurlExecutor.ExecuteBenchmarkedNethermindJsonRpcCommand("trace_block", "\"latest\"", "http://50.116.32.22:8545", Logger);
                    //Test result
                    bool isVerifiedPositively = VerifyResponse(result.Result.Item1);

                    if (result.Result.Item3 && isVerifiedPositively)
                    {
                        Logger.Info(result.Result.Item1);
                        executionTimes.Add(result.Result.Item2);
                    }
                    else
                        Logger.Error(result.Result.Item1);
                });

            Assert.IsNotEmpty(executionTimes, "All requests failed - unable to measeure times of execution.");

            var average = executionTimes.Average(x => x.Milliseconds);
            var totalRequestsSucceeded = executionTimes.Count();
            var min = executionTimes.Min(x => x.Milliseconds);
            var max = executionTimes.Max(x => x.Milliseconds);

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

        private bool VerifyResponse(string result)
        {
            try
            {
                TraceBlock parsed = JsonConvert.DeserializeObject<TraceBlock>(result);
                Logger.Info(parsed);
                if (parsed == null || parsed.Result == null)
                    return false;
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }
    }
}