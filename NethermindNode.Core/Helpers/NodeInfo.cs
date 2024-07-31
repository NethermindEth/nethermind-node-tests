using NethermindNode.Core.RpcResponses;
using NethermindNode.Tests.Enums;
using NLog;

namespace NethermindNode.Core.Helpers;

public static class NodeInfo
{
    private static readonly HttpClient client = new HttpClient();
    public static readonly string apiBaseUrl = "http://localhost:8545";
    public static readonly string wsBaseUrl = "ws://localhost:8545";

    public enum NetworkType
    {
        Mainnet = 1,
        EnergyWeb = 246,
        Gnosis = 100,
        Chiado = 10200,
        Volta = 73799,
        Sepolia = 11155111,
        Holesky = 17000,
    }

    public static bool IsFullySynced(Logger logger)
    {
        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("eth_syncing", "", apiBaseUrl, logger);
        var result = commandResult.Result;
        return result == null ? false : result.Item1.Contains("false");
    }

    public static void WaitForNodeToBeReady(Logger logger)
    {
        var apiIsAvailable = false;

        while (!apiIsAvailable)
        {
            try
            {
                var response = client.GetAsync(apiBaseUrl).Result;

                if (response.IsSuccessStatusCode)
                {
                    apiIsAvailable = true;
                    logger.Info("API is up and running!");
                }
                else
                {
                    logger.Info("API is not yet ready, waiting for 5 seconds...");
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception ex)
            {
                logger.Info($"Error while checking API availability: {ex.Message}");
                logger.Info("Retrying in 5 seconds...");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }

        //Waiting for proper start of node
        //while (DockerCommands.CheckIfDockerContainerIsCreated(ConfigurationHelper.Instance["execution-container-name"], logger) == false)
        //{
        //    logger.Info("Waiting for Execution to be started.");
        //    Thread.Sleep(30000);
        //}
    }

    public static string GetCurrentStage(NLog.Logger logger)
    {
        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("debug_getSyncStage", "", apiBaseUrl, logger);
        string output = "";

        bool isVerifiedPositively = JsonRpcHelper.TryDeserializeReponse<GetSyncStage>(commandResult.Result.Item1, out IRpcResponse deserialized);
        if (!isVerifiedPositively)
        {
            if (deserialized is RpcError)
                throw new Exception(((RpcError)deserialized).Error.Message);
            else
                output = "WaitingForConnection";
        }
        if (output == "")
            output = ((GetSyncStage)deserialized).Result.CurrentStage;

        logger.Debug("Current stage is: " + output);
        return output;
    }

    public static List<Stages> GetCurrentStages(NLog.Logger logger)
    {
        List<Stages> result = new List<Stages>();
        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("debug_getSyncStage", "", apiBaseUrl, logger);
        string output = "";

        bool isVerifiedPositively = JsonRpcHelper.TryDeserializeReponse<GetSyncStage>(commandResult.Result.Item1, out IRpcResponse deserialized);
        if (!isVerifiedPositively)
        {
            if (deserialized is RpcError)
                throw new Exception(((RpcError)deserialized).Error.Message);
            else
                output = "WaitingForConnection";
        }
        if (output == "")
            output = ((GetSyncStage)deserialized).Result.CurrentStage;

        foreach (string stage in output.Split(','))
        {
            bool parsed = Enum.TryParse(stage.Trim(), out Stages parsedStage);
            if (parsed)
            {
                result.Add(parsedStage);
            }
        }

        logger.Debug("Current stage is: " + output);
        return result;
    }

    public static void WaitForNodeToBeSynced(Logger logger)
    {
        while (!NodeInfo.IsFullySynced(logger))
        {
            logger.Debug("Waiting for node to be fully synced...");
            Thread.Sleep(10000);
        }
    }

    public static async Task<NetworkType> GetNetworkType(Logger logger)
    {
        var commandResult = await HttpExecutor.ExecuteAndSerialize<SingleResult>("eth_chainId", "", apiBaseUrl, logger);
        var result = commandResult.Result;
        if (result == null)
        {
            return NetworkType.Mainnet;
        }
        else
        {
            logger.Info($"Network type: {result}");
            return (NetworkType)int.Parse(result, System.Globalization.NumberStyles.HexNumber);
        }
    }

    public static async Task<SingleResult> GetConfigValue(Logger logger, string category, string key)
    {
        var res = await HttpExecutor.ExecuteAndSerialize<SingleResult>("debug_getConfigValue", $"\"{category}\", \"{key}\"", apiBaseUrl, logger);
        return res;
    }


    public static async Task<long> GetPivotNumber(Logger logger)
    {
        var result = await GetConfigValue(logger, "Sync", "PivotNumber");
        if (result.Result == null)
        {
            return 0;
        }

        return long.Parse(result.Result);
    }

    public static async Task<long> GetAncientReceiptsBarrier(Logger logger)
    {
        var result = await GetConfigValue(logger, "Sync", "AncientReceiptsBarrier");
        if (result.Result == null)
        {
            return 0;
        }

        return long.Parse(result.Result);
    }

}
