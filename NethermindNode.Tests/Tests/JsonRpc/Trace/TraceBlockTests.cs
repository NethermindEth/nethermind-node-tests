using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Core.RpcResponses;
using NethermindNode.Tests.CustomAttributes;
using NethermindNode.Tests.CustomObjects;
using NethermindNode.Tests.RpcResponses;
using Newtonsoft.Json;
using System.Text;

namespace NethermindNode.Tests.JsonRpc.Trace;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class TraceBlockTests : BaseTest
{
    [NethermindTestCase(5, 1, Category = "JsonRpc")]
    [NethermindTestCase(50, 1, Category = "JsonRpc")]
    [Description("This test should be used only on Archive node OR on node with Pruning.Mode=None")]
    public void TraceBlockHttp(int repeatCount, int parallelizableLevel)
    {
        List<TimeSpan> executionTimes = new List<TimeSpan>();

        Parallel.ForEach(
            Enumerable.Range(16419108, repeatCount),
            new ParallelOptions { MaxDegreeOfParallelism = parallelizableLevel },
            (task, loopState) =>
            {
                var result = HttpExecutor.ExecuteNethermindJsonRpcCommand("trace_block", $"\"{task}\"", TestItems.RpcAddress, TestLoggerContext.Logger);
                //Test result
                bool isVerifiedPositively = JsonRpcHelper.TryDeserializeReponse<TraceBlock>(result.Result.Item1, out IRpcResponse deserialized);

                if (result.Result.Item3 && isVerifiedPositively)
                {
                    executionTimes.Add(result.Result.Item2);
                }
                else
                {
                    TestLoggerContext.Logger.Error(result.Result.Item1);
                    Console.WriteLine("Curl result: " + result.Result.Item3);
                    Console.WriteLine("Parsing result: " + isVerifiedPositively);
                    Console.WriteLine("Output: " + result.Result.Item1);
                }
            });

        Assert.That(executionTimes.Count > 0, "All requests failed - unable to measure times of execution.");

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

    [NethermindTestCase(5, 5, 1, Category = "JsonRpc")]
    [NethermindTestCase(50, 5, 1, Category = "JsonRpc")]
    [Description("This test should be used only on Archive node OR on node with Pruning.Mode=None")]
    public void TraceBlockBatched(int repeatCount, int batchSize, int parallelizableLevel)
    {
        List<TimeSpan> executionTimes = new List<TimeSpan>();

        int start = 16419108;
        int end = start + repeatCount;

        Parallel.ForEach(
            Enumerable.Range(start, repeatCount).Where(x => (x - start) % batchSize == 0).ToList(),
            new ParallelOptions { MaxDegreeOfParallelism = parallelizableLevel },
            (task, loopState) =>
            {
                var batchedIds = Enumerable.Range(task, batchSize).Select(x => $"\"{x}\"").ToList();
                var result = HttpExecutor.ExecuteBatchedNethermindJsonRpcCommand("trace_block", batchedIds, TestItems.RpcAddress, TestLoggerContext.Logger);
                //Test result
                bool isVerifiedPositively = JsonRpcHelper.TryDeserializeReponses<IEnumerable<TraceBlock>>(result.Result.Item1, out IEnumerable<IRpcResponse> deserialized);

                if (result.Result.Item3 && isVerifiedPositively)
                {
                    executionTimes.Add(result.Result.Item2);
                }
                else
                {
                    TestLoggerContext.Logger.Error(result.Result.Item1);
                    Console.WriteLine("Curl result: " + result.Result.Item3);
                    Console.WriteLine("Parsing result: " + isVerifiedPositively);
                    Console.WriteLine("Output: " + result.Result.Item1);
                }
            });

        Assert.That(executionTimes.Count > 0, "All requests failed - unable to measeure times of execution.");

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

    [NethermindTestCase("170.187.152.20", "51.159.102.95", 1, 1, Category = "JsonRpcComapare")]
    [NethermindTestCase("170.187.152.20", "51.159.102.95", 40, 10, Category = "JsonRpcBenchmarkComapare")]
    [NethermindTestCase("18.222.197.12", "18.216.213.143", 500, 10, Category = "JsonRpcBenchmarkComapare")]
    public void TraceBlockCompare(string sourceNode, string targetNode, int repeatCount, int parallelizableLevel)
    {
        List<TimeSpan> executionTimes = new List<TimeSpan>();
        Random rnd = new Random();

        Parallel.ForEach(
            Enumerable.Range(16398035, repeatCount),
            new ParallelOptions { MaxDegreeOfParallelism = parallelizableLevel },
            (task, loopState) =>
            {
                var resultSource = HttpExecutor.ExecuteNethermindJsonRpcCommand("trace_block", $"\"{task}\"", $"http://{sourceNode}:8545", TestLoggerContext.Logger);
                var resultTarget = HttpExecutor.ExecuteNethermindJsonRpcCommand("trace_block", $"\"{task}\"", $"http://{targetNode}:8545", TestLoggerContext.Logger);
                //Test result
                bool isVerifiedPositivelySource = JsonRpcHelper.TryDeserializeReponse<TraceBlock>(resultSource.Result.Item1, out IRpcResponse deserializedSource);
                bool isVerifiedPositivelyTarget = JsonRpcHelper.TryDeserializeReponse<TraceBlock>(resultTarget.Result.Item1, out IRpcResponse deserializedTarget);

                Assert.That(isVerifiedPositivelyTarget, Is.EqualTo(isVerifiedPositivelySource), "Parsing result of both responses to TraceBlock schema is not the same.");
                Assert.That(resultTarget.Result.Item3, Is.EqualTo(resultSource.Result.Item3), "Response code is not the same for both requests.");
                Assert.That(resultTarget.Result.Item1, Is.EqualTo(resultSource.Result.Item1), "Response body is not equal for both requests.");

            });
    }

    [NethermindTestCase("18.222.197.12", "18.216.213.143", 500, 5, 10, Category = "JsonRpcBenchmarkComapare")]
    public void TraceBlockBatchedCompare(string sourceNode, string targetNode, int requestsCount, int step, int parallelizableLevel)
    {
        List<TimeSpan> executionTimes = new List<TimeSpan>();
        Random rnd = new Random();

        int start = 16398000;
        int end = start + requestsCount;

        Parallel.ForEach(
            Enumerable.Range(start, requestsCount).Where(x => (x - start) % step == 0).ToList(),
            new ParallelOptions { MaxDegreeOfParallelism = parallelizableLevel },
            (task, loopState) =>
            {
                var batchedIds = Enumerable.Range(task, step).Select(x => $"\"{x}\"").ToList();
                var resultSource = HttpExecutor.ExecuteBatchedNethermindJsonRpcCommand("trace_block", batchedIds, $"http://{sourceNode}:8545", TestLoggerContext.Logger);
                var resultTarget = HttpExecutor.ExecuteBatchedNethermindJsonRpcCommand("trace_block", batchedIds, $"http://{targetNode}:8545", TestLoggerContext.Logger);
                //Test result
                bool isVerifiedPositivelySource = JsonRpcHelper.TryDeserializeReponses<IEnumerable<TraceBlock>>(resultSource.Result.Item1, out IEnumerable<IRpcResponse> deserializedSource);
                bool isVerifiedPositivelyTarget = JsonRpcHelper.TryDeserializeReponses<IEnumerable<TraceBlock>>(resultTarget.Result.Item1, out IEnumerable<IRpcResponse> deserializedTarget);

                Assert.That(isVerifiedPositivelyTarget, Is.EqualTo(isVerifiedPositivelySource), "Parsing result of both responses to TraceBlock schema is not the same.");
                Assert.That(resultTarget.Result.Item3, Is.EqualTo(resultSource.Result.Item3), "Response code is not the same for both requests.");
                Assert.That(resultTarget.Result.Item1, Is.EqualTo(resultSource.Result.Item1), "Response body is not equal for both requests.");
            });
    }
}