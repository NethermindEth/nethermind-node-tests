using NethermindNode.Core.Helpers;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using System.Collections.Concurrent;
using System.Diagnostics;
using NethermindNode.Core.Helpers;
using NUnit.Framework.Internal;

using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.Web3;

using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.RPC.Eth.Subscriptions;
using Newtonsoft.Json;
using System;
using System.Numerics;
using System.Threading.Tasks;
namespace NethermindNode.Tests.Receipts;

public static class DBG
{
  public static Task WriteLineA(string message)
  {
    return Task.Run(() => Console.WriteLine(message));
  }

  public static Task WriteA(string message)
  {
    return Task.Run(() => Console.Write(message));
  }
  public static void WriteLine(string message)
  {
    Console.WriteLine(message);
  }

  public static void Write(string message)
  {
    Console.Write(message);
  }
}

class ReceiptsVerification
{
  private const string RpcAddress = "http://localhost:8545";
  private const string WsAddress = "ws://localhost:8545";

  // private Web3 w3 = new Web3(RpcAddress);

  private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

  public Queue<Block> blocks = new Queue<Block>();


  // [Test]
  // [Category("Receipts")]
  public void ShouldVerifyHeadReceipts()
  {
    TestContext.WriteLine("ShouldVerifyHeadReceipts");
    Logger.Info("ShouldVerifyHeadReceipts");

    Logger.Info($"***Starting test: ShouldVerifyHeadReceipts ***");
    var w3 = new Web3(RpcAddress);
    var blockNumber = w3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value;
    TestContext.WriteLine($"Current block number: {blockNumber}");
  }


  // 1. Head receipts verification
  private string CompareHeadReceipts(Block block)
  {
    var number = block.Number.Value;
    var w3 = new Web3(RpcAddress);

    var receipts = w3.Eth.Blocks.GetBlockReceiptsByNumber.SendRequestAsync(new HexBigInteger(number)).Result;
    Logger.Info($"Receipts count: {receipts.Length}");


    return ReceiptsHelper.CalculateRoot(receipts);

    /*
    1. On synced node subscribe to new blocks using eth_subscribe with newHeads topic
    2. Whenever notification from subscription is received we should:
        1. Get ReceiptsRoot from new block
        2. Get new block hash
        3. Use block hash and call **eth_getBlockReceipts** and calculate hash of all receipts (use Nethermind method for that or craft a new one to have a separate implementation).
        4. Compare Hashes from point i and iii.
    3. Thing to consider: we are “losing receipts” from time to time in unknown way so maybe such tool should not only check once after subscription notification but “recheck” also after some time (maybe after pruning interval?) to make sure receipts are properly flushed to DB
    */

  }

  public void SubscriptionHandler(object? sender, StreamingEventArgs<Block> e)
  {
    var utcTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)e.Response.Timestamp.Value);
    TestContext.WriteLine($"New Block: Number: {e.Response.Number.Value}, Timestamp: {JsonConvert.SerializeObject(utcTimestamp)}");
    Logger.Info($"\n\n\n\nNew Block: Number: {e.Response.Number.Value}, Timestamp: {JsonConvert.SerializeObject(utcTimestamp)}");
    var block = e.Response;
    blocks.Enqueue(block);
  }

  [Test]
  [Category("Receipts")]
  public async Task Verify_New_Receipts()
  {
    var testRunTime = 2 * 60 * 1000; // 10 minutes


    var client = new StreamingWebSocketClient(WsAddress);
    // create a subscription 
    // it won't do anything just yet though
    var subscription = new EthNewBlockHeadersSubscription(client);

    // attach our handler for new block header data
    subscription.SubscriptionDataResponse += SubscriptionHandler;

    bool subscribed = true;

    // handle unsubscription
    // optional - but may be important depending on your use case
    subscription.UnsubscribeResponse += (object sender, StreamingEventArgs<bool> success) =>
    {
      subscribed = false;
      TestContext.WriteLine($"Unsubscribed: {success.Response}");
      Logger.Info($"Unsubscribed: {success.Response}");
    };

    // open the web socket connection
    await client.StartAsync();

    // subscribe to new block headers
    // blocks will be received on another thread
    // therefore this doesn't block the current thread
    await subscription.SubscribeAsync();


    TestContext.Write("Waiting for new blocks: ");
    Logger.Info("Waiting for new blocks: ");

    var sw = new Stopwatch();
    sw.Start();
    //allow time to unsubscribe
    while (subscribed)
    {
      if (blocks.Count > 0)
      {
        var block = blocks.Dequeue();
        Logger.Info($"Processing: {block.BlockHash}");

        var calculatedRoot = CompareHeadReceipts(block);
        var receiptsRoot = block.ReceiptsRoot;
        Logger.Info($"ReceiptsRoot: {receiptsRoot} CalculatedRoot: {calculatedRoot} {calculatedRoot == receiptsRoot}");
        Assert.That(calculatedRoot, Is.EqualTo(receiptsRoot));
      }
      if (sw.ElapsedMilliseconds > testRunTime)
      {
        subscription.UnsubscribeAsync().Wait();
        break;
      }
    }
  }

  [Test]
  [Category("Receipts")]
  public async Task Verify_Historical_Receipts()
  {
    Logger.Info("Verify_Historical_Receipts");

    /*

2. Historical receipts verification
    1. On a synced node get a latest block hash (call it block X)
    2. Do a steps from **1.b.i** to **1.b.iv** for this block
    3. Get block X-1 and do the same as for point b
    4. Continue till genesis.

    */



    var sw = new Stopwatch();
    sw.Start();
    var testRunTime = 3 * 60 * 1000; // 10 minutes

    var w3 = new Web3(RpcAddress);
    var blockNumber = w3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value;
    var block = w3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(blockNumber)).Result;
    var count = 0;
    while (true)
    {
      if (sw.ElapsedMilliseconds > testRunTime)
      {
        Logger.Info($"Terminating. Processed: {count} blocks");
        break;
      }

      Logger.Info($"Processing: {block.BlockHash}");

      var calculatedRoot = CompareHeadReceipts(block);
      var receiptsRoot = block.ReceiptsRoot;
      Logger.Info($"[{block.Number}] ReceiptsRoot: {receiptsRoot} CalculatedRoot: {calculatedRoot} {calculatedRoot == receiptsRoot}");
      Assert.That(calculatedRoot, Is.EqualTo(receiptsRoot));
      count++;

      block = w3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(block.Number.Value - 1)).Result;
    }
  }
}
