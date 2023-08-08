using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System.Collections.Concurrent;
using System.Diagnostics;
using NethermindNode.Core.Helpers;
using NUnit.Framework.Internal;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Threading;

namespace NethermindNode.Tests.JsonRpc.Eth;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class EthCallTests : BaseTest
{
    [ThreadStatic]
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    [TestCase(1, 1, Category = "JsonRpc")]
    [TestCase(10000, 5, Category = "JsonRpcBenchmark,JsonRpcEthCallBenchmark")]
    [TestCase(10000, 500, Category = "JsonRpcBenchmark,JsonRpcEthCallBenchmarkStress")]
    public async Task EthCall(int repeatCount, int parallelizableLevel)
    {
        int i = 0;
        double startidx = 300 * Math.Pow(10, 6);   // Randomly chosen starting address, change this for multiple runs
        double increment = 50 * Math.Pow(10, 2);   // Randomly chosen increment between eth_calls

        Parallel.ForEach(
            Enumerable.Range(0, repeatCount),
            new ParallelOptions { MaxDegreeOfParallelism = parallelizableLevel },
            (task) =>
            {
                Console.WriteLine("Call {0} starting at address {1}", task, (startidx + (increment * task)));
                string hexidx = Convert.ToString((long)(startidx + (increment * task)), 16);
                hexidx = hexidx.PadLeft(64, '0');
                string code = "7f";   // PUSH32
                code += hexidx;
                code += "5b";  // JUMPDEST
                code += "46";  // CHAINID
                code += "90";  // SWAP1
                code += "03";  // SUB
                code += "80";  // DUP1
                code += "31";  // BALANCE
                code += "50";  // POP
                code += "60";  // PUSH1
                code += "21";  // JUMPTARGET
                code += "56";  // JUMP
                EthCallScenario(code);
            });
    }

    public enum TestingType
    {
        EthCallOnly = 0,
        TraceCallOnly = 1,
        EthCallAndTraceCall = 2
    }

    [TestCase(100000, 30, 0, 0, 0, TestingType.EthCallOnly, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 50, 0, 0, 0, TestingType.EthCallOnly, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 80, 0, 0, 0, TestingType.EthCallOnly, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 10, 0, 0, 0, TestingType.TraceCallOnly, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 30, 0, 0, 0, TestingType.TraceCallOnly, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 50, 0, 0, 0, TestingType.TraceCallOnly, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 80, 0, 0, 0, TestingType.TraceCallOnly, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 30, 0, 0, 0, TestingType.EthCallAndTraceCall, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 50, 0, 0, 0, TestingType.EthCallAndTraceCall, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 80, 0, 0, 0, TestingType.EthCallAndTraceCall, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    public async Task EthCallGatewayScenario(int repeatCount, int initialRequestsPerSecond, int rpsStep, int stepInterval, int maxTimeout = 0, TestingType testingType = TestingType.EthCallOnly)
    {
        Console.WriteLine($"Test Details:");
        Console.WriteLine($"Repeat Count: {repeatCount}");
        Console.WriteLine($"Initial Requests Per Second: {initialRequestsPerSecond}");
        Console.WriteLine($"RPS Step: {rpsStep}");
        Console.WriteLine($"Step Interval: {stepInterval}");
        Console.WriteLine($"Max Timeout: {maxTimeout}");
        Console.WriteLine($"Test Type: {testingType.ToString()}");
        int counter = 0;
        int success = 0;
        int fail = 0;
        int elapsedSeconds = 0;

        // Define the cancellation token source
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        BlockingCollection<Task<string>> responseTasks = new BlockingCollection<Task<string>>();
        Dictionary<string, int> uniqueErrorMessages = new Dictionary<string, int>();

        //Start only if API is ready
        NodeInfo.WaitForNodeToBeReady(Logger);


        // Create a separate task to handle responses
        var responseHandlingTask = Task.Run(async () =>
        {
            bool TryParseJson(string jsonString, out JObject jsonObject)
            {
                try
                {
                    jsonObject = JObject.Parse(jsonString);
                    return true;
                }
                catch (JsonReaderException)
                {
                    jsonObject = null;
                    return false;
                }
            }

            foreach (var responseTask in responseTasks.GetConsumingEnumerable())
            {
                var response = await responseTask;

                if ((response.Contains("error") && response.ToString() != String.Empty) || response == null)
                {
                    fail++;

                    if (response != null)
                    {
                        var parsed = TryParseJson(response, out var jsonResponse);
                        if (parsed)
                        {
                            string errorMessage, errorData;
                            errorMessage = jsonResponse["error"]["message"].ToString();
                            errorData = jsonResponse["error"]["data"].ToString();

                            string parsedResponse = $"{errorMessage} : {errorData}";

                            if (uniqueErrorMessages.ContainsKey(parsedResponse))
                            {
                                uniqueErrorMessages[parsedResponse]++;
                            }
                            else
                            {
                                uniqueErrorMessages[parsedResponse] = 1;
                            }
                        }
                        else
                        {
                            string errorMessage = $"API ERROR = {response}";

                            if (uniqueErrorMessages.ContainsKey(errorMessage))
                            {
                                uniqueErrorMessages[errorMessage]++;
                            }
                            else
                            {
                                uniqueErrorMessages[errorMessage] = 1;
                            }
                        }
                    }
                }
                else
                {
                    success++;
                }
            }
        });

        //var apiMonitoringTask = Task.Run(async () =>
        //{
        //    while (!cancellationTokenSource.Token.IsCancellationRequested)
        //    {
        //        bool isAlive = NodeInfo.IsApiAlive(TestItems.RpcAddress);
        //
        //        if (!isAlive)
        //        {
        //            // Handle the situation when the API is down.
        //            // This can be logging an error, stopping the current test, etc.
        //            Console.WriteLine("API is down!");
        //
        //            // Optionally, stop the main test by setting some shared flag or directly using a CancellationToken.
        //            cancellationTokenSource.Cancel();
        //            break;
        //        }
        //
        //        await Task.Delay(TimeSpan.FromSeconds(10));  // Check every 10 seconds.
        //    }
        //});


        // This task will periodically report the achieved RPS.
        var periodicReportTask = Task.Run(async () =>
        {
            Stopwatch reportStopwatch = new Stopwatch();
            reportStopwatch.Start();

            int previousCounter = 0;

            while (!cancellationTokenSource.Token.IsCancellationRequested)
            {
                await Task.Delay(10000); // Wait for 10 seconds.

                int newRequests = counter - previousCounter;
                double achievedRps = newRequests / reportStopwatch.Elapsed.TotalSeconds;

                Console.WriteLine($"Achieved RPS over the last 10 seconds: {achievedRps}");

                // Reset for the next period.
                reportStopwatch.Restart();
                previousCounter = counter;
            }
        });

        DateTime startTime = DateTime.UtcNow;

        if (repeatCount != 0)
        {
            for (var i = 0; i < repeatCount && !cancellationTokenSource.Token.IsCancellationRequested; i++)
            {
                if (stepInterval > 0 && (DateTime.UtcNow - startTime).TotalSeconds > stepInterval)
                {
                    Console.WriteLine($"Increasing RPS to: {initialRequestsPerSecond + rpsStep}");
                    initialRequestsPerSecond += rpsStep;
                    startTime = DateTime.UtcNow;
                }

                string code = TestItems.HugeGatewayCall;
                var requestTask = ExecuteGatewayScenario(testingType, code, i);
                responseTasks.Add(requestTask);

                counter++;

                var delay = 1000 / initialRequestsPerSecond;
                await Task.Delay(delay);
            }
        }
        else if (maxTimeout != 0)
        {
            var sw = new Stopwatch();
            sw.Start();
            int iterator = 0;

            while (sw.Elapsed.TotalSeconds < maxTimeout && !cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (stepInterval > 0 && (DateTime.UtcNow - startTime).TotalSeconds > stepInterval)
                {
                    initialRequestsPerSecond += rpsStep;
                    startTime = DateTime.UtcNow;
                }

                string code = TestItems.HugeGatewayCall;
                var requestTask = ExecuteGatewayScenario(testingType, code, iterator);
                responseTasks.Add(requestTask);

                counter++;
                iterator++;

                var delay = 1000 / initialRequestsPerSecond;
                await Task.Delay(delay);
            }

            sw.Stop();
        }

        cancellationTokenSource.Cancel();
        await periodicReportTask;
        //await apiMonitoringTask;

        // Indicate that no more items will be added
        responseTasks.CompleteAdding();

        // Wait for the response handling task to finish processing all items
        await responseHandlingTask;

        Console.WriteLine($"Requests per second: {initialRequestsPerSecond}");
        Console.WriteLine($"Requests sent in total: {counter}");
        Console.WriteLine($"Succeded requests: {success}");
        Console.WriteLine($"Failed requests: {fail}");
        Console.WriteLine($"Unique error messages: {uniqueErrorMessages.Count}");
        foreach (var kvp in uniqueErrorMessages)
        {
            Console.WriteLine($"Message: {kvp.Key}, Count: {kvp.Value}");
        }
    }

    private Task<string> ExecuteGatewayScenario(TestingType testingType, string code, int id)
    {
        if (testingType == TestingType.EthCallOnly)
            return EthCallGatewayScenario(code, id);
        if (testingType == TestingType.TraceCallOnly)
            return TraceCallGatewayScenario(code, id);

        if (id % 2 == 0)
            return EthCallGatewayScenario(code, id);

        return TraceCallGatewayScenario(code, id);
    }
    async Task<string> TraceCallGatewayScenario(string code, int id)
    {
        try
        {
            var callParams = new
            {
                to = TestItems.TestingAddress,
                data = code
            };

            var traceArray = new[] { "trace" };

            var payload = new object[] { callParams, traceArray, "latest" };

            var serializedPayload = JsonConvert.SerializeObject(payload);

            var result = await HttpExecutor.ExecuteNethermindJsonRpcCommandAsync("trace_call", serializedPayload, id.ToString(), TestItems.RpcAddress, Logger);

            return result;
        }
        catch (Exception e)
        {
            return await Task.FromResult("An error occurred: " + e.Message);
        }
    }

    async Task<string> EthCallGatewayScenario(string code, int id)
    {
        try
        {
            var callObject = new
            {
                to = TestItems.TestingAddress,
                data = code
            };

            var payload = new object[] { callObject, "latest" };

            string serializedPayload = JsonConvert.SerializeObject(payload);

            var result = await HttpExecutor.ExecuteNethermindJsonRpcCommandAsync("eth_call", serializedPayload, id.ToString(), TestItems.RpcAddress, Logger);

            return result;
        }
        catch (Exception e)
        {
            return await Task.FromResult("An error occurred: " + e.Message);
        }
    }
    void EthCallScenario(string code)
    {
        try
        {
            var w3 = new Web3(TestItems.RpcAddress);

            var callInput = new CallInput
            {
                Value = new HexBigInteger(0),
                From = "0x0000000000000000000000000000000000000000",
                Gas = new HexBigInteger(Convert.ToString((long)(100 * Math.Pow(10, 6)), 16)),
                MaxFeePerGas = new HexBigInteger(Convert.ToString((long)(250 * Math.Pow(10, 9)), 16)),
                MaxPriorityFeePerGas = new HexBigInteger(Convert.ToString((long)Math.Pow(10, 9), 16)),
                Data = code
            };
            var result = w3.Eth.Transactions.Call.SendRequestAsync(callInput);
            //var parsed = result.Result.ToString();

        }
        catch (AggregateException e)
        {
            if (e.InnerException is RpcResponseException)
            {
                var innner = (RpcResponseException)e.InnerException;
                if (innner.RpcError.Data.ToString() != "OutOfGas")
                {
                    throw innner;
                }
            }
        }
        catch (Exception e)
        {
            Logger.Error(e.Message);
            Logger.Error(e.StackTrace);
        }
    }
}