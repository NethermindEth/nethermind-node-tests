using NethermindNode.Core.Helpers;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Hex.HexTypes;
using System.Diagnostics;
using NUnit.Framework.Internal;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Eth.Subscriptions;
using Newtonsoft.Json;
using System.Numerics;
namespace NethermindNode.Tests.Receipts;

class ReceiptsVerification
{
  private const string RpcAddress = "http://localhost:8545";
  private const string WsAddress = "ws://localhost:8545";

  private Web3 w3 = new Web3(RpcAddress);

  private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

  public Queue<Block> blocks = new Queue<Block>();


  // [Test]
  // [Category("Receipts")]
  public void ShouldVerifyHeadReceipts()
  {
    TestContext.WriteLine("ShouldVerifyHeadReceipts");
    Logger.Info("ShouldVerifyHeadReceipts");

    Logger.Info($"***Starting test: ShouldVerifyHeadReceipts ***");
    var blockNumber = w3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value;
    TestContext.WriteLine($"Current block number: {blockNumber}");
  }

  public void SubscriptionHandler(object? sender, StreamingEventArgs<Block> e)
  {
    var utcTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)e.Response.Timestamp.Value);
    TestContext.WriteLine($"New Block: Number: {e.Response.Number.Value}, Timestamp: {JsonConvert.SerializeObject(utcTimestamp)}");
    Logger.Info($"\n\n\n\nNew Block: Number: {e.Response.Number.Value}, Timestamp: {JsonConvert.SerializeObject(utcTimestamp)}");
    var block = e.Response;
    blocks.Enqueue(block);
  }

  // [Test]
  // [Category("Receipts")]
  public async Task Verify_New_Receipts()
  {
    var testRunTime = 2 * 60 * 1000; // 10 minutes

    var client = new StreamingWebSocketClient(WsAddress);
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
        /*
        1. On synced node subscribe to new blocks using eth_subscribe with newHeads topic
        2. Whenever notification from subscription is received we should:
            1. Get ReceiptsRoot from new block
            2. Get new block hash
            3. Use block hash and call **eth_getBlockReceipts** and calculate hash of all receipts (use Nethermind method for that or craft a new one to have a separate implementation).
            4. Compare Hashes from point i and iii.
        3. Thing to consider: we are “losing receipts” from time to time in unknown way so maybe such tool should not only check once after subscription notification but “recheck” also after some time (maybe after pruning interval?) to make sure receipts are properly flushed to DB
        */

        var receipts = w3.Eth.Blocks.GetBlockReceiptsByNumber.SendRequestAsync(new HexBigInteger(block.Number.Value)).Result;
        var calculatedRoot = ReceiptsHelper.CalculateRoot(receipts);
        var receiptsRoot = block.ReceiptsRoot;
        Logger.Info($"ReceiptsRoot: {receiptsRoot} CalculatedRoot: {calculatedRoot} {calculatedRoot == receiptsRoot}");
        Assert.That(calculatedRoot, Is.EqualTo(receiptsRoot));
      }
      if (sw.ElapsedMilliseconds > testRunTime)
      {
        await subscription.UnsubscribeAsync();
        subscribed = false;
        await client.StopAsync();
        break;
      }
    }
  }

  private void ProcessBlock(BigInteger blockNumber)
  {
    if (blockNumber < 0)
    {
      Logger.Info($"Negative block number: {blockNumber}");
      return;
    }
    var block = w3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(new HexBigInteger(blockNumber)).Result;
    var receipts = w3.Eth.Blocks.GetBlockReceiptsByNumber.SendRequestAsync(new HexBigInteger(block.Number.Value)).Result;
    var calculatedRoot = ReceiptsHelper.CalculateRoot(receipts);
    if (blockNumber % 100 == 0)
    {
      Logger.Info($"Processing: {block.BlockHash}");
      Logger.Info($"[{block.Number}] [{block.BlockHash}] Equal: {calculatedRoot == block.ReceiptsRoot}");
    }
    Assert.That(calculatedRoot, Is.EqualTo(block.ReceiptsRoot));
  }

  // [Test]
  // [Category("Receipts")]
  public void Verify_Historical_Receipts_To_Genesis()
  {
    Logger.Info("Verify_Historical_Receipts_To_Genesis");

    var head = w3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value;
    var upperBound = (int)head + 1;
    var lowerBound = 0; // genesis

    ParallelOptions parallelOptions = new ParallelOptions
    {
      // Set the maximum number of concurrent operations
      MaxDegreeOfParallelism = 4
    };


    Parallel.For(lowerBound, upperBound, parallelOptions, i =>
    {
      var blockNumber = head - i;
      ProcessBlock(blockNumber);
    });
  }

  [Test]
  [Category("Receipts")]
  public void Verify_Historical_Receipts_Near_Pivot()
  {
    Logger.Info("Verify_Historical_Receipts_Near_Pivot");

    var network = NodeInfo.GetNetworkType(Logger);
    var pivotBlock = NodeInfo.GetPivotNumber(Logger);
    var head = w3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value;
    var upperBound = pivotBlock + 1 * 1000 * 1000; // 1mil
    var lowerBound = pivotBlock - 1 * 1000 * 1000; // 1mil

    if (lowerBound < 0)
    {
      lowerBound = 0;
    }

    if (upperBound > head)
    {
      upperBound = (long)head;
    }

    Logger.Info($"Pivot: {pivotBlock} Head: {head} Lower: {lowerBound} Upper: {upperBound}");

    ParallelOptions parallelOptions = new ParallelOptions
    {
      // Set the maximum number of concurrent operations
      MaxDegreeOfParallelism = 4
    };


    Parallel.For(lowerBound, upperBound, parallelOptions, i =>
    {
      var blockNumber = head - i;
      ProcessBlock(blockNumber);
    });
  }
}
