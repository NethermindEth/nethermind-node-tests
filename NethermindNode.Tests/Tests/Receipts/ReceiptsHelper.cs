

using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing.GethStyle.Custom.JavaScript;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs;
using Nethermind.State.Proofs;
using Newtonsoft.Json;

class ReceiptsHelper
{

  private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

  ReleaseSpec spec = new ReleaseSpec() { ValidateReceipts = false };

  private static TxReceipt[] ConvertReceipts(TransactionReceipt[] receipts)
  {

    TxReceipt[] txReceipts = new TxReceipt[receipts.Length];
    for (int i = 0; i < receipts.Length; i++)
    {
      Logger.Info($"Converting {JsonConvert.SerializeObject(receipts[i])}");
      // var entry = JsonConvert.DeserializeObject<LogEntry>(JsonConvert.SerializeObject(receipts[i].Logs[0]));
      var rcp = new TxReceipt();
      // rcp.Logs = receipts[i].Logs.Select(l => new LogEntry(new Address(l.Address), l.Data.ToBytes(), l.Topics.Select(t => new Hash256(t)).ToArray())).ToArray(),
      rcp.Bloom = new Bloom(receipts[i].LogsBloom.ToBytes());

      // ??? not sure it's the right field
      rcp.PostTransactionState = receipts[i].Root == null ? null : new Hash256(receipts[i].Root);
      rcp.ReturnValue = receipts[i].Status.HexValue.ToBytes();

      rcp.Recipient = new Address(receipts[i].To);
      rcp.ContractAddress = receipts[i].ContractAddress == null ? null : new Address(receipts[i].ContractAddress);
      rcp.Sender = new Address(receipts[i].From);
      rcp.GasUsedTotal = (long)receipts[i].CumulativeGasUsed.Value;
      rcp.GasUsed = (long)receipts[i].GasUsed.Value;
      rcp.Index = (int)receipts[i].TransactionIndex.Value;
      rcp.TxHash = new Hash256(receipts[i].TransactionHash);
      rcp.BlockHash = new Hash256(receipts[i].BlockHash);
      rcp.BlockNumber = (long)receipts[i].BlockNumber.Value;
      rcp.StatusCode = receipts[i].Status.HexValue.ToBytes()[0];
      rcp.TxType = (TxType)(byte)receipts[i].Type.Value;

      for (int j = 0; j < receipts[i].Logs.Count; j++)
      {
        var entry = JsonConvert.DeserializeObject<LogEntry>(JsonConvert.SerializeObject(receipts[i].Logs[j]));
        Logger.Info($"Log: {JsonConvert.SerializeObject(entry)}");

        rcp.Logs[j] = entry;
        // rcp.Logs[j] = new LogEntry(new Address(receipts[i].Logs[j].Address), receipts[i].Logs[j].Data.ToBytes(), receipts[i].Logs[j].Topics.Select(t => new Hash256(t)).ToArray());
      }

      txReceipts[i] = rcp;
      Logger.Info($"Converted: {i}");
    }

    return txReceipts;
  }

  public static string CalculateRoot(TransactionReceipt[] receipts)
  {
    // Calculate the root hash of the receipts
    Logger.Info($"CalculateRoot: {receipts.Length}");

    var txReceipts = ConvertReceipts(receipts);
    Logger.Info($"txReceipts: {txReceipts.Length}");

    return ReceiptsRootCalculator.Instance.GetReceiptsRoot(txReceipts, new ReleaseSpec() { ValidateReceipts = false }, new Hash256("0x0")).ToString();

    // var spec = new ReleaseSpec() { ValidateReceipts = false };
    // var _decoder = Rlp.GetStreamDecoder<TxReceipt>(RlpDecoderKey.Trie);
    // Hash256 receiptsRoot = ReceiptTrie<TxReceipt>.CalculateRoot(spec, receipts, _decoder);


  }
}