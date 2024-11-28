#if INCLUDE_SUBMODULES

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

[TestFixture]
class ReceiptsVerification
{
  private Web3 w3 = new Web3(NodeInfo.apiBaseUrl);
  

  [NethermindTest]
  [Category("ReceiptsNew")]
  public async Task Verify_New_Receipts()
  {
    var blocks = new Queue<Block>();
    var processedBlocks = new List<Block>();

    var testBlocks = 256;

    var client = new StreamingWebSocketClient(NodeInfo.wsBaseUrl);
    var subscription = new EthNewBlockHeadersSubscription(client);

    // attach our handler for new block header data
    subscription.SubscriptionDataResponse += (object sender, StreamingEventArgs<Block> e) =>
    {
      var utcTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)e.Response.Timestamp.Value);
      Logger.Info($"\n\n\n\nNew Block: Number: {e.Response.Number.Value}, Timestamp: {JsonConvert.SerializeObject(utcTimestamp)}");
      var block = e.Response;
      blocks.Enqueue(block);
    };

    bool subscribed = true;

    // handle unsubscription
    // optional - but may be important depending on your use case
    subscription.UnsubscribeResponse += (object sender, StreamingEventArgs<bool> success) =>
    {
      subscribed = false;
      Logger.Info($"Unsubscribed: {success.Response}");
    };

    // open the web socket connection
    await client.StartAsync();

    // subscribe to new block headers
    // blocks will be received on another thread
    // therefore this doesn't block the current thread
    await subscription.SubscribeAsync();


    Logger.Info("Waiting for new blocks: ");

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
        processedBlocks.Add(block);
      }
      if (processedBlocks.Count() >= testBlocks)
      {
        await subscription.UnsubscribeAsync();
        subscribed = false;
        await client.StopAsync();
        break;
      }
    }

    // Re-verify already processed blocks
    foreach (var block in processedBlocks)
    {
      var receipts = w3.Eth.Blocks.GetBlockReceiptsByNumber.SendRequestAsync(new HexBigInteger(block.Number.Value)).Result;
      var calculatedRoot = ReceiptsHelper.CalculateRoot(receipts);
      Logger.Info($"Processed: {block.BlockHash}");
      Logger.Info($"[{block.Number}] [{block.BlockHash}] Equal: {calculatedRoot == block.ReceiptsRoot}");
      Assert.That(calculatedRoot, Is.EqualTo(block.ReceiptsRoot));
    }
  }

  [NethermindTest]
  [Category("ReceiptsToGenesis")]
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

  [NethermindTest]
  [Category("ReceiptsNearPivot")]
  public async Task Verify_Historical_Receipts_Near_Pivot()
  {
    Logger.Info("Verify_Historical_Receipts_Near_Pivot");

    var network = NodeInfo.GetNetworkType(Logger);
    var pivotBlock = await NodeInfo.GetPivotNumber(Logger);
    var head = w3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value;
    var upperBound = pivotBlock + 500 * 1000; // 500k
    var lowerBound = pivotBlock - 500 * 1000; // 500k

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


  [NethermindTest]
  [Category("ReceiptsNearAncientBarrier")]
  public async Task Verify_Historical_Receipts_Near_Ancient_Barrier()
  {
    Logger.Info("Verify_Historical_Receipts_Near_Ancient_Barrier");

    var head = w3.Eth.Blocks.GetBlockNumber.SendRequestAsync().Result.Value;
    var pivotBlock = await NodeInfo.GetPivotNumber(Logger);
    var ancientBarrier = await NodeInfo.GetAncientReceiptsBarrier(Logger);

    if (ancientBarrier > head)
    {
      ancientBarrier = pivotBlock;
    }

    // The actual value is determined by this formula:
    // max{ 1, min{ PivotNumber, max{ AncientBodiesBarrier, AncientReceiptsBarrier } } }
    var upperBound = ancientBarrier + 500 * 1000; // 500k
    var lowerBound = ancientBarrier - 500 * 1000; // 500k

    if (lowerBound < 0)
    {
      lowerBound = 0;
    }

    if (upperBound > head)
    {
      upperBound = (long)head;
    }

    Logger.Info($"ancientBarrier: {ancientBarrier} Head: {head} Lower: {lowerBound} Upper: {upperBound}");

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

}

#endif