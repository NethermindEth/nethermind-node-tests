#if INCLUDE_SUBMODULES

using Nethereum.RPC.Eth.DTOs;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing.GethStyle.Custom.JavaScript;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs;
using Nethermind.State.Proofs;

class ReceiptsHelper
{
  private static TxReceipt[] ConvertReceipts(TransactionReceipt[] receipts)
  {

    TxReceipt[] txReceipts = new TxReceipt[receipts.Length];
    for (int i = 0; i < receipts.Length; i++)
    {
      var rcp = new TxReceipt
      {
        Bloom = new Bloom(receipts[i].LogsBloom.ToBytes()),
        PostTransactionState = receipts[i].Root is null ? null : new Hash256(receipts[i].Root),
        ReturnValue = receipts[i].Status.HexValue.ToBytes(),
        Recipient = receipts[i].To is null ? null : new Address(receipts[i].To),
        ContractAddress = receipts[i].ContractAddress is null ? null : new Address(receipts[i].ContractAddress),
        Sender = new Address(receipts[i].From),
        GasUsedTotal = (long)receipts[i].CumulativeGasUsed.Value,
        GasUsed = (long)receipts[i].GasUsed.Value,
        Index = (int)receipts[i].TransactionIndex.Value,
        TxHash = new Hash256(receipts[i].TransactionHash),
        BlockHash = new Hash256(receipts[i].BlockHash),
        BlockNumber = (long)receipts[i].BlockNumber.Value,
        StatusCode = receipts[i].Status.HexValue.ToBytes()[0],
        TxType = (TxType)(byte)receipts[i].Type.Value,
        Logs = new LogEntry[receipts[i].Logs.Count]
      };
      for (int j = 0; j < receipts[i].Logs.Count; j++)
      {
        var log = receipts[i].Logs[j];
        var addr = new Address(log["address"].ToString());
        byte[] data = log.Value<string>("data").ToBytes();
        Hash256[] topics = log["topics"].Select(t => new Hash256(t.ToString())).ToArray();

        rcp.Logs[j] = new LogEntry(addr, data, topics);
      }

      txReceipts[i] = rcp;
    }

    return txReceipts;
  }

  public static string CalculateRoot(TransactionReceipt[] receipts)
  {
    // Calculate the root hash of the receipts
    var txReceipts = ConvertReceipts(receipts);
    var spec = HoleskySpecProvider.Instance.GetSpec(HoleskySpecProvider.Instance.TransitionActivations[1]);
    var _decoder = Rlp.GetStreamDecoder<TxReceipt>(RlpDecoderKey.Trie);
    return ReceiptTrie<TxReceipt>.CalculateRoot(spec, txReceipts, _decoder).ToString();
  }
}
#endif