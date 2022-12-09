using NethermindNodeTests.CustomObjects;
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
                    var result = CurlExecutor.ExecuteBenchmarkedNethermindJsonRpcCommand("trace_block", "\"latest\"", "http://localhost:8545", Logger);
                    //Test result
                    var isVerifiedPositively = VerifyResponse(result.Result.Item1);

                    if (result.Result.Item3 /* && testing succeeded */)
                        executionTimes.Add(result.Result.Item2);
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

        private object VerifyResponse(HttpResponseMessage item1)
        {
            return null;
            //throw new NotImplementedException();
        }
    }
}