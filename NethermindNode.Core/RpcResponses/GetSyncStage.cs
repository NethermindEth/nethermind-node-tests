#nullable disable

namespace NethermindNode.Core.RpcResponses;

public class Result
{
    public string CurrentStage { get; set; }
}

public class GetSyncStage : IRpcResponse
{
    public string Jsonrpc { get; set; }
    public Result Result { get; set; }
    public int Id { get; set; }
}
