using NethermindNode.Core.RpcResponses;
using NethermindNode.Tests.Enums;

namespace NethermindNode.Core.Helpers;

public static class NodeInfo
{
    private static readonly HttpClient client = new HttpClient();
    private static readonly string apiBaseUrl = "http://localhost:8545";

    public static bool IsFullySynced(NLog.Logger logger)
    {
        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommandWithTimingInfo("eth_syncing", "", "1", "http://localhost:8545", logger);
        var result = commandResult.Result;
        return result == null ? false : result.Item1.Contains("false");
    }

    public static bool IsApiAlive(string apiUrl)
    {
        try
        {
            using (var client = new HttpClient())
            {
                var response = client.GetAsync(apiUrl).Result;
                return response.IsSuccessStatusCode;
            }
        }
        catch
        {
            return false;
        }
    }


    public static void WaitForNodeToBeReady(NLog.Logger logger)
    {
        var apiIsAvailable = false;
        Console.WriteLine($"Is API available: {apiIsAvailable}");

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
        //while (DockerCommands.CheckIfDockerContainerIsCreated("sedge-execution-client", logger) == false)
        //{
        //    logger.Info("Waiting for Execution to be started.");
        //    Thread.Sleep(30000);
        //}
    }

    public static string GetCurrentStage(NLog.Logger logger)
    {
        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommandWithTimingInfo("debug_getSyncStage", "", "1", "http://localhost:8545", logger);
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
        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommandWithTimingInfo("debug_getSyncStage", "", "1", "http://localhost:8545", logger);
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
}
