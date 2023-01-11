﻿using CommandLine;
using Nethereum.Contracts.QueryHandlers.MultiCall;
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
        [TestCase(100, 1, Category = "JsonRpcBenchmark")]
        [TestCase(1000, 1, Category = "JsonRpcBenchmark")]
        [TestCase(100, 5, Category = "JsonRpcBenchmark")]
        [TestCase(1000, 5, Category = "JsonRpcBenchmark")]
        [TestCase(100, 10, Category = "JsonRpcBenchmark")]
        [TestCase(1000, 10, Category = "JsonRpcBenchmark")]
        [TestCase(1000, 100, Category = "JsonRpcBenchmarkStress")]
        [Description("This test should be used only on Archive node OR on node with Pruning.Mode=None")]
        public void TraceBlock(int repeatCount, int parallelizableLevel)
        {
            List<TimeSpan> executionTimes = new List<TimeSpan>();
            Random rnd = new Random();

            Parallel.ForEach(
                Enumerable.Range(16375600, repeatCount),
                new ParallelOptions { MaxDegreeOfParallelism = parallelizableLevel },
                (task, loopState) =>
                {
                    var result = CurlExecutor.ExecuteBenchmarkedNethermindJsonRpcCommand("trace_block", $"\"{task}\"", "http://170.187.152.20:8545", Logger);
                    //Test result
                    bool isVerifiedPositively = JsonRpcHelper.DeserializeReponse<TraceBlock>(result.Result.Item1);

                    if (result.Result.Item3 && isVerifiedPositively)
                    {
                        executionTimes.Add(result.Result.Item2);
                    }
                    else
                    {
                        Logger.Error(result.Result.Item1);
                        Console.WriteLine("Curl reslt: " + result.Result.Item3);
                        Console.WriteLine("Parsing result: " + isVerifiedPositively);
                        Console.WriteLine("Output: " + result.Result.Item1);
                    }
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
            Console.WriteLine(serializedJson);
        }

        [TestCase("170.187.152.20", "51.159.102.95", 1, 1, Category = "JsonRpcComapare")]
        [TestCase("170.187.152.20", "51.159.102.95", 40, 10, Category = "JsonRpcBenchmarkComapare")]
        [TestCase("170.187.152.20", "51.159.102.95", 500, 10, Category = "JsonRpcBenchmarkComapare")]
        public void TraceBlockCompare(string sourceNode, string targetNode, int repeatCount, int parallelizableLevel)
        {
            List<TimeSpan> executionTimes = new List<TimeSpan>();
            Random rnd = new Random();

            Parallel.ForEach(
                Enumerable.Range(16375600, repeatCount),
                new ParallelOptions { MaxDegreeOfParallelism = parallelizableLevel },
                (task, loopState) =>
                {
                    var resultSource = CurlExecutor.ExecuteBenchmarkedNethermindJsonRpcCommand("trace_block", $"\"{task}\"", $"http://{sourceNode}:8545", Logger);
                    var resultTarget = CurlExecutor.ExecuteBenchmarkedNethermindJsonRpcCommand("trace_block", $"\"{task}\"", $"http://{targetNode}:8545", Logger);
                    //Test result
                    bool isVerifiedPositivelySource = JsonRpcHelper.DeserializeReponse<TraceBlock>(resultSource.Result.Item1);
                    bool isVerifiedPositivelyTarget = JsonRpcHelper.DeserializeReponse<TraceBlock>(resultTarget.Result.Item1);

                    Assert.That(isVerifiedPositivelyTarget, Is.EqualTo(isVerifiedPositivelySource), "Parsing result of both responses to TraceBlock schema is not the same.");
                    Assert.That(resultTarget.Result.Item3, Is.EqualTo(resultSource.Result.Item3), "Response code is not the same for both requests.");
                    Assert.That(resultTarget.Result.Item1, Is.EqualTo(resultSource.Result.Item1), "Response body is not equal for both requests.");

                });
        }
    }
}