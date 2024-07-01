namespace NethermindNode.Core.RpcResponses
{
  // Generic single string result response
  public class SingleResult : IRpcResponse
  {
    public string Jsonrpc { get; set; }
    public string Result { get; set; }
    public int Id { get; set; }
  }

}
