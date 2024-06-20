

using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethermind.Blockchain.Receipts;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm.Tracing.GethStyle.Custom.JavaScript;
using Nethermind.Serialization.Rlp;
using Nethermind.Specs;
using Nethermind.State.Proofs;

class ReceiptsHelper
{

  ReleaseSpec spec = new ReleaseSpec() { ValidateReceipts = false };

  private static TxReceipt[] ConvertReceipts(TransactionReceipt[] receipts)
  {

    TxReceipt[] txReceipts = new TxReceipt[receipts.Length];
    for (int i = 0; i < receipts.Length; i++)
    {
      var rcp = new TxReceipt();
      rcp.BlockHash = new Hash256(receipts[i].BlockHash);
      rcp.BlockNumber = (long)receipts[i].BlockNumber.Value;
      rcp.ContractAddress = new Address(receipts[i].ContractAddress);

      // rcp.Logs = receipts[i].Logs.Select(l => new LogEntry()
      // {
      //   address = new Address(l.Address),
      //   data = l.Data.ToBytes(),
      //   topics = l.Topics.Select(t => new Hash256(t)).ToArray()
      // }).ToArray();
      rcp.Bloom = new Bloom(receipts[i].LogsBloom.ToBytes());

      // ??? not sure it's the right field
      rcp.PostTransactionState = new Hash256(receipts[i].Root);
      rcp.ReturnValue = receipts[i].Status.HexValue.ToBytes();

      rcp.Recipient = new Address(receipts[i].To);
      rcp.ContractAddress = new Address(receipts[i].ContractAddress);
      rcp.Sender = new Address(receipts[i].From);
      rcp.GasUsedTotal = (long)receipts[i].CumulativeGasUsed.Value;
      rcp.GasUsed = (long)receipts[i].GasUsed.Value;
      rcp.Index = (int)receipts[i].TransactionIndex.Value;
      rcp.TxHash = new Hash256(receipts[i].TransactionHash);
      rcp.BlockHash = new Hash256(receipts[i].BlockHash);
      rcp.BlockNumber = (long)receipts[i].BlockNumber.Value;
      rcp.StatusCode = receipts[i].Status.HexValue.ToBytes()[0];
      rcp.TxType = (TxType)(byte)receipts[i].Type.Value;

      txReceipts[i] = rcp;
    }

    return txReceipts;
  }

  public static string CalculateRoot(TransactionReceipt[] receipts)
  {
    // Calculate the root hash of the receipts

    var txReceipts = ConvertReceipts(receipts);
    return ReceiptsRootCalculator.Instance.GetReceiptsRoot(txReceipts, new ReleaseSpec() { ValidateReceipts = false }, new Hash256("0x0")).ToString();

    // var spec = new ReleaseSpec() { ValidateReceipts = false };
    // var _decoder = Rlp.GetStreamDecoder<TxReceipt>(RlpDecoderKey.Trie);
    // Hash256 receiptsRoot = ReceiptTrie<TxReceipt>.CalculateRoot(spec, receipts, _decoder);


  }
}