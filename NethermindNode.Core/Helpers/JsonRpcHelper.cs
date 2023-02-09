using NethermindNode.Core.RpcResponses;
using Newtonsoft.Json;
using System.Collections;

namespace NethermindNode.Core.Helpers;

public static class JsonRpcHelper
{
    public static bool TryDeserializeReponse<T>(string result, out IRpcResponse deserialized) where T : IRpcResponse
    {
        deserialized = default;
        try
        {
            RpcError error = JsonConvert.DeserializeObject<RpcError>(result);
            bool isError = error.Error != null;
            if (isError)
            {
                deserialized = error;
                return false;
            }

            deserialized = JsonConvert.DeserializeObject<T>(result);

            if (deserialized == null)
                return false;
        }
        catch
        {
            return false;
        }


        return true;
    }

    public static bool TryDeserializeReponses<T>(string result, out IEnumerable<IRpcResponse> deserialized) where T : IEnumerable<IRpcResponse>
    {
        deserialized = default;
        try
        {
            RpcError error = JsonConvert.DeserializeObject<RpcError>(result);
            bool isError = error.Error != null;
            if (isError)
            {
                return false;
            }

            deserialized = JsonConvert.DeserializeObject<T>(result);

            if (deserialized == null)
                return false;

            foreach (var item in deserialized)
            {
                if (item.GetType().GetProperty("error") != null)
                    return false;
            }

        }
        catch
        {
            return false;
        }


        return true;
    }
}