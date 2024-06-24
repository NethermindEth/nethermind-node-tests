

using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Specs;
using Nethermind.Evm.Tracing.GethStyle.Custom.JavaScript;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs;
using Nethermind.State.Proofs;
using Newtonsoft.Json;

class ReceiptsHelper
{

  private static readonly NLog.Logger Logger = NLog.LogManager.GetLogger(TestContext.CurrentContext.Test.Name);

  private static TxReceipt[] ConvertReceipts(TransactionReceipt[] receipts)
  {

    TxReceipt[] txReceipts = new TxReceipt[receipts.Length];
    for (int i = 0; i < receipts.Length; i++)
    {
      // Logger.Info($"\n\nConverting {JsonConvert.SerializeObject(receipts[i])}");
      var rcp = new TxReceipt();
      rcp.Bloom = new Bloom(receipts[i].LogsBloom.ToBytes());

      // ??? not sure it's the right field
      rcp.PostTransactionState = receipts[i].Root == null ? null : new Hash256(receipts[i].Root);
      rcp.ReturnValue = receipts[i].Status.HexValue.ToBytes();

      rcp.Recipient = receipts[i].To == null ? null : new Address(receipts[i].To);
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

      rcp.Logs = new LogEntry[receipts[i].Logs.Count];
      for (int j = 0; j < receipts[i].Logs.Count; j++)
      {
        var log = receipts[i].Logs[j];
        var addr = new Address(log["address"].ToString());
        byte[] data = log.Value<string>("data").ToBytes();
        Hash256[] topics = log["topics"].Select(t => new Hash256(t.ToString())).ToArray();

        rcp.Logs[j] = new LogEntry(addr, data, topics);
      }

      txReceipts[i] = rcp;
      // Logger.Info($"Converted: {i}");
    }

    return txReceipts;
  }

  public static string CalculateRoot(TransactionReceipt[] receipts)
  {
    // Calculate the root hash of the receipts
    // Logger.Info($"CalculateRoot: {receipts.Length}");

    var txReceipts = ConvertReceipts(receipts);
    // Logger.Info($"txReceipts: {txReceipts.Length}");

    // return ReceiptsRootCalculator.Instance.GetReceiptsRoot(txReceipts, new ReleaseSpec() { ValidateReceipts = false }, new Hash256("0x0")).ToString();

    // var spec = new ReleaseSpec() { ValidateReceipts = false };
    var spec = HoleskySpecProvider.Instance.GetSpec(HoleskySpecProvider.Instance.TransitionActivations[1]);

    var _decoder = Rlp.GetStreamDecoder<TxReceipt>(RlpDecoderKey.Trie);
    return ReceiptTrie<TxReceipt>.CalculateRoot(spec, txReceipts, _decoder).ToString();
  }
}