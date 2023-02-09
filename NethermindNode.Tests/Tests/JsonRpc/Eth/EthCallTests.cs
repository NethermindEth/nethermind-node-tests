﻿using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;

namespace NethermindNode.Tests.JsonRpc.Eth;

[TestFixture]
[Parallelizable(ParallelScope.None)]
public class EthCallTests : BaseTest
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

    [TestCase(1, 1, Category = "JsonRpc")]
    [TestCase(10000, 5, Category = "JsonRpcBenchmark,JsonRpcEthCallBenchmark")]
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
                JustExecuteCode(code);
            });
    }

    void JustExecuteCode(string code)
    {
        try
        {
            var w3 = new Web3("http://139.144.31.115:8545");

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
            var parsed = result.Result.ToString();

        }
        catch (AggregateException e)
        {
            if (e.InnerException is RpcResponseException)
            {
                var innner = (RpcResponseException) e.InnerException;
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