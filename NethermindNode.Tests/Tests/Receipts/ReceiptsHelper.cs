

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
      var rcp = new TxReceipt
      {
        // rcp.Logs = receipts[i].Logs.Select(l => new LogEntry()
        // {
        //   address = new Address(l.Address),
        //   data = l.Data.ToBytes(),
        //   topics = l.Topics.Select(t => new Hash256(t)).ToArray()
        // }).ToArray();
        Bloom = new Bloom(receipts[i].LogsBloom.ToBytes()),

        // ??? not sure it's the right field
        PostTransactionState = new Hash256(receipts[i].Root),
        ReturnValue = receipts[i].Status.HexValue.ToBytes(),

        Recipient = new Address(receipts[i].To),
        ContractAddress = new Address(receipts[i].ContractAddress),
        Sender = new Address(receipts[i].From),
        GasUsedTotal = (long)receipts[i].CumulativeGasUsed.Value,
        GasUsed = (long)receipts[i].GasUsed.Value,
        Index = (int)receipts[i].TransactionIndex.Value,
        TxHash = new Hash256(receipts[i].TransactionHash),
        BlockHash = new Hash256(receipts[i].BlockHash),
        BlockNumber = (long)receipts[i].BlockNumber.Value,
        StatusCode = receipts[i].Status.HexValue.ToBytes()[0],
        TxType = (TxType)(byte)receipts[i].Type.Value
      };

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