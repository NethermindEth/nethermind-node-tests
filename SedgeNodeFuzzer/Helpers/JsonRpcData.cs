using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SedgeNodeFuzzer.Curl
{
    public class JsonRpcData
    {
        public string Method { get; set; }
        public List<object> Params { get; set; }
        public int Id { get; set; }
        public string JsonRpc { get; set; }
    }
}
