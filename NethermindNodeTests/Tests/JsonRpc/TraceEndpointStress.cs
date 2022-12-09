using NethermindNodeTests.CustomObjects;
using Newtonsoft.Json;
using SedgeNodeFuzzer.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Tests.JsonRpc
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class TraceEndpointStress : BaseTest
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        [TestCase(1000, 1)]
        [TestCase(1000, 5)]
        [TestCase(1000, 10)]
        [Category("JsonRpc")]
        public async Task TraceBlock(int repeatCount, int parallelizableLevel)
        {
            List<TimeSpan> executionTimes = new List<TimeSpan>();
            //for (int i = 0; i < repeatCount; i++)
            //{
            //    var result = await CurlExecutor.ExecuteBenchmarkedNethermindJsonRpcCommand("trace_block", "\"latest\"", "http://localhost:8545", Logger);
            //    if (result.Item3)
            //        executionTimes.Add(result.Item2);
            //}

            Parallel.For(
                0,
                repeatCount,
                new ParallelOptions { MaxDegreeOfParallelism = parallelizableLevel },
                async (task) =>
                {
                    var result = await CurlExecutor.ExecuteBenchmarkedNethermindJsonRpcCommand("trace_block", "\"latest\"", "http://localhost:8545", Logger);
                    if (result.Item3)
                        executionTimes.Add(result.Item2);
                });

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
    }
}