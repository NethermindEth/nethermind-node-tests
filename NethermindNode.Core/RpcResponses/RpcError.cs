using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNode.Core.RpcResponses
{
    public class RpcError : IRpcResponse
    {
        public string Jsonrpc { get; set; }
        public Error Error { get; set; }
        public int Id { get; set; }
    }

    public class Error
    {
        public int Code { get; set; }
        public string Message { get; set; }
    }
}
