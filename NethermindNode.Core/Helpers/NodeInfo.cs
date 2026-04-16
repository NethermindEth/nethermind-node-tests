using NethermindNode.Core.RpcResponses;
using NethermindNode.Tests.Enums;
using NLog;

namespace NethermindNode.Core.Helpers;

public static class NodeInfo
{
    private static readonly HttpClient client = new HttpClient();
    public static readonly string apiBaseUrl = "http://localhost:" + ConfigurationHelper.Instance["default-rpc-port"];
    public static readonly string wsBaseUrl = "ws://localhost:" + ConfigurationHelper.Instance["default-rpc-port"];

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
        var currentStages = GetCurrentStages(logger);
        if (currentStages.Count == 0 || currentStages.ToJoinedString() == Stages.Disconnected.ToString() || currentStages.ToJoinedString() == Stages.None.ToString() || currentStages.Contains(Stages.UpdatingPivot) && !currentStages.Contains(Stages.WaitingForBlock))
        {
            return false;
        }

        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("eth_syncing", "", apiBaseUrl, logger);
        var result = commandResult.Result;
        return result == null ? false : result.Item1.Contains("false");
    }

    public static void WaitForNodeToBeReady(Logger logger)
    {
        var apiIsAvailable = false;
        int attempt = 0;

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
                    if (attempt == 0 || attempt % 12 == 0)
                        logger.Info("API is not yet ready, waiting...");
                    attempt++;
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            }
            catch (Exception)
            {
                if (attempt == 0 || attempt % 12 == 0)
                    logger.Info("API is not yet ready, waiting...");
                attempt++;
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }
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

        logger.Trace("Current stage is: " + output);
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

        logger.Trace("Current stage is: " + output);
        return result;
    }

    public static int GetCurrentBlock(NLog.Logger logger)
    {
        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("eth_blockNumber", "", apiBaseUrl, logger);
        string output = commandResult.Result.Item1;

        logger.Trace("Current Block is: " + output);
        return Convert.ToInt32(output);
    }

    public static int GetPeerCount(Logger logger)
    {
        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("net_peerCount", "", apiBaseUrl, logger);
        string output = commandResult.Result?.Item1;
        if (string.IsNullOrEmpty(output))
        {
            logger.Trace("Peer count: N/A (no response)");
            return -1;
        }

        try
        {
            var cleaned = output.Replace("\"", "").Trim();
            int count = Convert.ToInt32(cleaned, 16);
            logger.Trace("Peer count: " + count);
            return count;
        }
        catch (Exception ex)
        {
            logger.Trace("Failed to parse peer count: " + ex.Message);
            return -1;
        }
    }

    public static void WaitForNodeToBeSynced(Logger logger)
    {
        int iteration = 0;
        while (!IsFullySynced(logger))
        {
            if (iteration == 0 || iteration % 6 == 0)
                logger.Info("Waiting for node to be fully synced...");
            iteration++;
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

    public static bool VerifyLogsForUndesiredEntries(ref List<string> errors)
    {
        var exceptions = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Exception");
        var corruption = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Corruption");
        var freeDiskSpace = DockerCommands.GetDockerLogs(ConfigurationHelper.Instance["execution-container-name"], "Free disk space");
        bool status = true;
        var undesiredEntries = new List<string>();

        if (exceptions.Any())
        {
            foreach (var item in exceptions)
            {
                if (!string.IsNullOrEmpty(item))
                {
                    if (!item.Contains("ObjectDisposedException")) //HACK: Until Timer disposals will be fixed
                    {
                        undesiredEntries.Add("Exception: " + item.Trim());
                        errors.Add(item);
                        status = false;
                    }
                }
            }
        }

        if (corruption.Any())
        {
            foreach (var item in corruption)
            {
                if (!string.IsNullOrEmpty(item))
                {
                    undesiredEntries.Add("Corruption: " + item.Trim());
                }
            }
            errors.AddRange(corruption);
            status = false;
        }

        if (freeDiskSpace.Any())
        {
            foreach (var item in freeDiskSpace)
            {
                if (!string.IsNullOrEmpty(item))
                {
                    undesiredEntries.Add("FreeDiskSpace: " + item.Trim());
                }
            }
            errors.AddRange(freeDiskSpace);
            status = false;
        }

        if (undesiredEntries.Count > 0)
        {
            int total = undesiredEntries.Count;
            int displayCount = Math.Min(total, 5);
            TestLoggerContext.Logger.Error($"[VERIFY] Found {total} undesired log entries:");
            for (int i = 0; i < displayCount; i++)
            {
                TestLoggerContext.Logger.Error($"  - {undesiredEntries[i]}");
            }
            if (total > 5)
            {
                TestLoggerContext.Logger.Error($"  ... and {total - 5} more");
            }
        }

        return status;
    }
}
