using Newtonsoft.Json;
using System.Collections;

namespace NethermindNode.Tests.Helpers
{
    public static class JsonRpcHelper
    {
        public static bool DeserializeReponse<T>(string result)
        {
            try
            {
                dynamic parsed = JsonConvert.DeserializeObject<T>(result);

                if (parsed == null || parsed.GetType().GetProperty("error") != null)
                    return false;

                if (parsed is IEnumerable)
                {
                    foreach(var item in parsed)
                    {
                        if (item.GetType().GetProperty("error") != null)
                            return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
