using NethermindNode.Core.RpcResponses;
using NethermindNode.Tests.Enums;
using NLog;
using System.Text.RegularExpressions;

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

    public static long GetCurrentBlock(NLog.Logger logger)
    {
        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("eth_blockNumber", "", apiBaseUrl, logger);
        string output = commandResult.Result?.Item1 ?? "";

        logger.Trace("Current Block raw: " + output);

        try
        {
            // Response is JSON-RPC: {"jsonrpc":"2.0","id":1,"result":"0xffc"}
            // Parse the result field and convert hex to long
            var json = System.Text.Json.JsonDocument.Parse(output);
            if (json.RootElement.TryGetProperty("result", out var result))
            {
                string hex = result.GetString()?.Replace("\"", "").Trim() ?? "0x0";
                if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt64(hex.Substring(2), 16);
                return long.Parse(hex);
            }
        }
        catch { }

        // Fallback: try parsing raw output directly (legacy format)
        try
        {
            if (output.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Convert.ToInt64(output.Substring(2), 16);
            return long.Parse(output);
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets the currently synced block for progress tracking.
    /// First tries eth_syncing.currentBlock (locally processed block).
    /// If eth_syncing returns false (fully synced) or is unparseable,
    /// falls back to eth_blockNumber.
    /// Returns -1 only if both methods fail.
    /// </summary>
    public static long GetSyncingCurrentBlock(Logger logger)
    {
        var commandResult = HttpExecutor.ExecuteNethermindJsonRpcCommand("eth_syncing", "", apiBaseUrl, logger);
        string output = commandResult.Result?.Item1 ?? "";

        // If eth_syncing returns false → fully synced, use eth_blockNumber for actual count
        if (string.IsNullOrEmpty(output) || output.Contains("false"))
        {
            try
            {
                return GetCurrentBlock(logger);
            }
            catch
            {
                return -1;
            }
        }

        // Try parsing currentBlock from eth_syncing response
        try
        {
            var json = System.Text.Json.JsonDocument.Parse(output);
            if (json.RootElement.TryGetProperty("currentBlock", out var currentBlock))
            {
                string hex = currentBlock.GetString()?.Replace("\"", "").Trim() ?? "0x0";
                long block = Convert.ToInt64(hex, 16);
                logger.Trace($"Syncing currentBlock: {block}");
                return block;
            }
        }
        catch (Exception ex)
        {
            logger.Trace($"Failed to parse eth_syncing currentBlock: {ex.Message}");
        }

        // Fallback: try eth_blockNumber (may return chain head for snap sync,
        // but better than -1 for progress display)
        try
        {
            return GetCurrentBlock(logger);
        }
        catch
        {
            return -1;
        }
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

    // Exception patterns to ignore — these are expected during normal operation
    private static readonly string[] IgnoredExceptionPatterns = new[]
    {
        "ObjectDisposedException",         // Timer disposal race condition
        "Cannot access a disposed object", // PLINQ-wrapped ObjectDisposedException from SnapProvider.AddAccountRange's AsParallel _codeDb.KeyExists racing RocksDB disposal on shutdown; logged as AggregateException whose header lacks the literal "ObjectDisposedException" token so the pattern above doesn't match. Node recovers fine on restart.
        "DISCONNECT",                      // NetworkDiag peer disconnect traces (RlpException, etc.)
        "Error in communication with",     // NetworkDiag peer communication errors
        "over limit 8 or",                 // RlpLimitException at HelloMessageSerializer when a peer advertises a capability protocol code >8 bytes — strict spec rejection is intended behavior (NethermindEth/nethermind#11751 closed without merge)
        "Failed to deserialize message",   // ProtocolHandlerBase: a peer that negotiated eth/69+ sent a malformed/old-format Status (or Disconnect) message — Nethermind's eth/69 decoder hits a scalar where a 32-byte hash is expected and throws DecodeKeccakRlpException/RlpException. These are non-conformant or foreign-network peers (e.g. energi3, bor, besu-dev); the node correctly disconnects them and syncs fine. Capability negotiation only agrees versions the peer advertised, so this is benign peer noise, not a node defect.
        "partial receipts response below minimum size", // SubprotocolException in the eth/6x-70 receipts sync dispatcher when a PEER serves an undersized receipts page during Old-Receipts backfill. The node rejects the response and retries another peer; it never affects local state. Confirmed benign: the only "failure" it caused was this detector flagging it on an otherwise-healthy, head-following node (gnosis SyncGLFV). Peer-quality noise, not a node defect. Subsumed by the SubprotocolException entry below; kept for documentation of a known face.
        "SubprotocolException: Receipt count",          // Documented face of the eth/70 receipts-paging peer-noise class (Eth70ProtocolHandler ValidateReceiptCount/ValidateReceiptSizeAgainstTransactionGasLimit: "Receipt count exceeds/mismatch with block transactions count"). Subsumed by the SubprotocolException entry below; kept for documentation.
        "Failure when executing request Nethermind.Network.P2P.Subprotocols.SubprotocolException", // Whole eth-sync peer-response-validation noise class. A SubprotocolException surfaced through the SyncDispatcher's "Failure when executing request" wrapper always means a PEER served a response that violates the subprotocol on a sync request (bodies/receipts/etc.); Nethermind logs at WARN, discards the response, deprioritizes the peer and retries the batch elsewhere — local state is never touched. This subsumes the receipts-validation family whose 13 distinct messages (Eth70ProtocolHandler.cs: "Cumulative gas decreased within block receipts", "Intrinsic gas lower bound exceeds block gas used", "Receipt count exceeds/mismatch", "Received more receipts than requested", "Invalid firstBlockReceiptIndex", "Received ... above hard limit", "Receipt/Block receipts size exceeds ... gas ... allowance", "Peer returned no progress for partial receipts", the two "partial receipts"/"Receipt count" faces above, etc.) all reduce to the same benign class — string-by-string allowlisting kept missing new faces (FuzzHT hit "Cumulative gas decreased" on run 31318571038 after "Receipt count" was allowlisted). Scoped to the SyncDispatcher wrapper + the SubprotocolException type so a real node-side fault (which surfaces as a different exception type and/or ERROR/FATAL, not as a peer sync-request failure) is NOT masked.
        "Network is unreachable",          // OS-level SocketException from the Discv5 discovery handler when a peer endpoint is unroutable. Discovery-layer only; nodes logging it synced fine (SyncHL/SyncHLF/BPSMN passed). Transient networking, not a node defect.
        "WebSocket not open",              // IOException writing to a WebSocket (JSON-RPC/subscription) whose socket was already closed — teardown artifact during a fuzz kill / restart. The close reason varies by timing ("(Aborted)", "(CloseReceived)", …); all are the same benign socket-lifecycle event, so match the reason-independent prefix. Irrelevant to node/state health. (run 31318571038: FuzzHN flagged the "(CloseReceived)" variant that the earlier "(Aborted)"-only entry missed.)
        "Unhandled exception in BlockDownloader: System.OperationCanceledException", // BlockDownloader task cancelled on shutdown / sync-mode transition. Scoped to the cancellation type so a real BlockDownloader fault is NOT masked. Nodes logging it passed (archive lanes). Benign lifecycle cancellation.
    };

    // Nethermind's console logs are ANSI-colored, so a captured log line can be
    // wrapped in SGR escape sequences (e.g. "\e[91m   at Foo.Bar()\e[0m"). Both the
    // continuation check and the allowlist Contains-match must run against the plain
    // text, or a leading escape defeats StartsWith("at ") and a mid-phrase escape can
    // break a pattern match. Strip all CSI escape sequences before comparing.
    private static readonly Regex AnsiEscape = new(@"\u001b\[[0-9;]*[A-Za-z]", RegexOptions.Compiled);

    private static string StripAnsi(string logLine) => AnsiEscape.Replace(logLine, string.Empty);

    private static bool IsIgnoredException(string logLine)
    {
        string clean = StripAnsi(logLine);
        foreach (var pattern in IgnoredExceptionPatterns)
        {
            if (clean.Contains(pattern))
                return true;
        }
        return false;
    }

    // Stack-trace continuation lines must follow their header's fate, never be judged
    // alone: the docker-log grep for "Exception" also matches frames whose method
    // SIGNATURE contains the word (e.g. "at CountingStreamPipeWriter.CompleteAsync(
    // Exception exception)"), so an exception whose header is allowlisted still fails
    // the scan on its frames (SyncSNWS, run 30865692535: "WebSocket not open (Aborted)"
    // header ignored per allowlist, its two CompleteAsync frames flagged). The header
    // line itself always names the exception type, so no signal is lost by skipping.
    // ANSI must be stripped first: on run 31318571038 the frames arrived color-wrapped
    // ("\e[91m   at ...") so the pre-ANSI StartsWith("at ") check let them leak through.
    private static bool IsStackTraceContinuation(string logLine)
    {
        string trimmed = StripAnsi(logLine).TrimStart();
        return trimmed.StartsWith("at ") || trimmed.StartsWith("--- End of");
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
                if (!string.IsNullOrEmpty(item) && !IsStackTraceContinuation(item) && !IsIgnoredException(item))
                {
                    undesiredEntries.Add("Exception: " + item.Trim());
                    errors.Add(item);
                    status = false;
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
