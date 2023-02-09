using NethermindNode.Core.RpcResponses;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace NethermindNode.Tests.RpcResponses;


public class Action
{
    public string CallType { get; set; }
    public string From { get; set; }
    public string Gas { get; set; }
    public string Input { get; set; }
    public string To { get; set; }
    public string Value { get; set; }
}

public class Results
{
    public Action Action { get; set; }
    public string BlockHash { get; set; }
    public int BlockNumber { get; set; }
    public Results Result { get; set; }
    public int Subtraces { get; set; }
    public List<int> TraceAddress { get; set; }
    public string TransactionHash { get; set; }
    public int TransactionPosition { get; set; }
    public string Type { get; set; }
    public string GasUsed { get; set; }
    public string Output { get; set; }
}

public class TraceBlock : IRpcResponse
{
    public string Jsonrpc { get; set; }
    public List<Results> Result { get; set; }
    public int Id { get; set; }
}
