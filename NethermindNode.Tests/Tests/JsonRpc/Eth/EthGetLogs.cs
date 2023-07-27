using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using NethermindNode.Tests.JsonRpc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNode.Tests.Tests.JsonRpc.Eth
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class EthGetLogs : BaseTest
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

        [TestCase(0, 1, 0, 0, 10, Category = "JsonRpcBenchmark,JsonRpcGatewayEthGetLogsBenchmarkStress")]
        public async Task EthGetLogsGatewayScenario(int repeatCount, int initialRequestsPerSecond, int rpsStep, int stepInterval, int maxTimeout = 0)
        {
            int counter = 0;
            int success = 0;
            int fail = 0;

            BlockingCollection<Task<FilterLog[]>> responseTasks = new BlockingCollection<Task<FilterLog[]>>();

            // Create a separate task to handle responses
            var responseHandlingTask = Task.Run(async () =>
            {
                foreach (var responseTask in responseTasks.GetConsumingEnumerable())
                {
                    var response = await responseTask;
                    if (response != null)
                        foreach (var filterLog in response.ToList())
                        {
                            Console.WriteLine($"Removed: {filterLog.Removed}");
                            Console.WriteLine($"Type: {filterLog.Type}");
                            Console.WriteLine($"Log Index: {filterLog.LogIndex?.Value}"); // ?.Value is used to get value from HexBigInteger
                            Console.WriteLine($"Transaction Hash: {filterLog.TransactionHash}");
                            Console.WriteLine($"Transaction Index: {filterLog.TransactionIndex?.Value}");
                            Console.WriteLine($"Block Hash: {filterLog.BlockHash}");
                            Console.WriteLine($"Block Number: {filterLog.BlockNumber?.Value}");
                            Console.WriteLine($"Address: {filterLog.Address}");
                            Console.WriteLine($"Data: {filterLog.Data}");

                            // Print topics
                            if (filterLog.Topics != null && filterLog.Topics.Length > 0)
                            {
                                Console.WriteLine("Topics: ");
                                foreach (var topic in filterLog.Topics)
                                {
                                    Console.WriteLine($"- {topic}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("No topics");
                            }

                            Console.WriteLine("-----------------------------------------------------");

                        }

                    //if (response.Contains("error") && response.ToString() != String.Empty)
                    //{
                    //    fail++;
                    //}
                    //else
                    //{
                    //    success++;
                    //}
                }
            });

            if (repeatCount != 0)
            {
                for (var i = 0; i < repeatCount; i++)
                {
                    string code = TestItems.HugeGatewayCall;
                    var requestTask = EthGetLogsGatewayScenario(i);
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
                    var requestTask = EthGetLogsGatewayScenario(iterator);
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

        async Task<FilterLog[]> EthGetLogsGatewayScenario(int id)
        {
            try
            {
                var w3 = new Web3(TestItems.RpcAddress);

                var filter = new NewFilterInput
                {
                    FromBlock = new BlockParameter(new Nethereum.Hex.HexTypes.HexBigInteger("0x0")),
                    ToBlock = BlockParameter.CreateLatest(),
                    Address = new[] { "0xebcede67d7699293ee3228a511c14d6a531307b8" },
                    Topics = new[]
                    {
                        "0x4641df4a962071e12719d8c8c8e5ac7fc4d97b927346a3d7a335b1f7517e133c",
                        "0xaceaac9344b11101e7119e739232774d88d3c6c471a76a950e71c4567abf2af6"
                    }
                };
                var result = await w3.Eth.Filters.GetLogs.SendRequestAsync(filter, id);
                return result;

            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}
