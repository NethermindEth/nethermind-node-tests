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

            string fileName = "TraceBlockPerformance.txt";

            try
            {
                // Check if file already exists. If yes, delete it.     
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                // Create a new file     
                using (FileStream fs = File.Create(fileName))
                {
                    // Add some text to file    
                    Byte[] averageByte = new UTF8Encoding(true).GetBytes("Average: " + average + "\n");
                    Byte[] totalRequestsExecuted = new UTF8Encoding(true).GetBytes("Requests executed: " + repeatCount + "\n");
                    Byte[] totalRequestsSucceededByte = new UTF8Encoding(true).GetBytes("Requests Succeeded: " + totalRequestsSucceeded + "\n");
                    Byte[] minByte = new UTF8Encoding(true).GetBytes("Minimum: " + min + "\n");
                    Byte[] maxByte = new UTF8Encoding(true).GetBytes("Maximum: " + max + "\n");
                    fs.Write(averageByte, 0, averageByte.Length);
                    fs.Write(totalRequestsExecuted, 0, totalRequestsExecuted.Length);
                    fs.Write(totalRequestsSucceededByte, 0, totalRequestsSucceededByte.Length);
                    fs.Write(minByte, 0, minByte.Length);
                    fs.Write(maxByte, 0, maxByte.Length);
                }
            }
            catch (Exception Ex)
            {
                Console.WriteLine(Ex.ToString());
            }
        }
    }
}