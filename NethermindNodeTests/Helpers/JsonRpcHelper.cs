using NethermindNodeTests.RpcResponses;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Helpers
{
    public static class JsonRpcHelper
    {
        public static bool DeserializeReponse<T>(string result)
        {
            try
            {
                dynamic parsed = JsonConvert.DeserializeObject<T>(result);
                if (parsed == null || parsed.Result == null)
                    return false;
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }
    }
}
