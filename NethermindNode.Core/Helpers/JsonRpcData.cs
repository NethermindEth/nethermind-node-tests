namespace NethermindNode.Core.Helpers
{
    public class JsonRpcData
    {
        public string? Method { get; set; }
        public List<object>? Params { get; set; }
        public int Id { get; set; }
        public string? JsonRpc { get; set; }
    }
}
