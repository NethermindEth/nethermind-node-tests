using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NethermindNode.Tests.JsonRpc.Eth;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class EthCallTests : BaseTest
{
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

 //   [TestCase(100000, 50, 0, 0, 0, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 100, 0, 0, 0, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(100000, 150, 0, 0, 0, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    [TestCase(0, 100, 0, 0, 600, Category = "JsonRpcBenchmark,JsonRpcGatewayEthCallBenchmarkStress")]
    public async Task EthCallGatewayScenario(int repeatCount, int initialRequestsPerSecond, int rpsStep, int stepInterval, int maxTimeout = 0)
    {
        int counter = 0;
        int success = 0;
        int fail = 0;
        int elapsedSeconds = 0;

        // Create a separate task to output the counter every second.
        //var monitoringTask = Task.Run(async () =>
        //{
        //  while (true)
        //{
        //  await Task.Delay(1000);
        //  Console.WriteLine($"Requests sent in the last second: {counter}");
        //  counter = 0; // reset the counter every second
        //  elapsedSeconds++; // increase elapsed time

        //  if (elapsedSeconds % stepInterval == 0)
        //{
        //  initialRequestsPerSecond += rpsStep; // Increase rps every stepInterval seconds
        //  Console.WriteLine($"RPS updated to: {initialRequestsPerSecond}");
        //}
        //}
        //});

        BlockingCollection<Task<string>> responseTasks = new BlockingCollection<Task<string>>();

        // Create a separate task to handle responses
        var responseHandlingTask = Task.Run(async () =>
        {
            foreach (var responseTask in responseTasks.GetConsumingEnumerable())
            {
                var response = await responseTask;

                if (response.Contains("error") && response.ToString() != String.Empty)
                {
                    fail++;
                }
                else
                {
                    success++;
                }
            }
        });

        if (repeatCount != 0)
        {
            for (var i = 0; i < repeatCount; i++)
            {
                string code = TestItems.HugeGatewayCall;
                var requestTask = EthCallGatewayScenario(code, i);
                responseTasks.Add(requestTask); // Add the task to the collection

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

            while (sw.Elapsed.TotalSeconds < maxTimeout)
            {
                string code = TestItems.HugeGatewayCall;
                var requestTask = EthCallGatewayScenario(code, iterator);
                responseTasks.Add(requestTask); // Add the task to the collection

                counter++;
                iterator++;

                var delay = 1000 / initialRequestsPerSecond;
                await Task.Delay(delay);
            }

            sw.Stop();
        }

        // Indicate that no more items will be added
        responseTasks.CompleteAdding();

        // Wait for the response handling task to finish processing all items
        await responseHandlingTask;

        Console.WriteLine($"Requests per second: {initialRequestsPerSecond}");
        Console.WriteLine($"Requests sent in total: {counter}");
        Console.WriteLine($"Succeded requests: {success}");
        Console.WriteLine($"Failed requests: {fail}");
    }

    void EthCallScenario(string code)
    {
        try
        {
            var w3 = new Web3("http://localhost:8545");

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

    async Task<string> EthCallGatewayScenario(string code, int id)
    {
        try
        {
            var w3 = new Web3("http://localhost:8545");

            var callInput = new CallInput
            {
                To = "0xc3da629c518404860c8893a66ce3bb2e16bea6ec",
                Data = code
            };
            var result = await w3.Eth.Transactions.Call.SendRequestAsync(callInput, id);
            //var parsed = result.Result.ToString();
            return result;

        }
        catch (Exception e)
        {
            return await Task.FromResult("An error occurred: " + e.Message);
        }
    }
}