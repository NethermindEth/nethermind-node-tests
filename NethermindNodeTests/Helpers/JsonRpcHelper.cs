using Newtonsoft.Json;

namespace NethermindNode.Tests.Helpers
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
