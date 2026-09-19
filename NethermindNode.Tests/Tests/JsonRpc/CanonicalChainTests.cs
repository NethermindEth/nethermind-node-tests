// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Core.RpcResponses;
using NethermindNode.Tests.CustomAttributes;
using Newtonsoft.Json;

namespace NethermindNode.Tests.JsonRpc;

/// <summary>
/// Regression test for NethermindEth/nethermind#10876.
///
/// 1. Call eth_getBlockByNumber("finalized") — finalized blocks cannot be reorged, so this is a trusted anchor.
/// 2. Walk backward N blocks via parentHash using eth_getBlockByHash to build a ground-truth (number, hash) chain.
/// 3. Batch-fetch those block numbers via eth_getBlockByNumber to get the node's canonical view.
/// 4. If eth_getBlockByNumber(N).hash differs from the ground-truth hash at N, the node has a stale canonical
///    marker (HasBlockOnMainChain=true on a non-canonical block) — that's the bug #10876 surfaces.
///
/// The requested depth is an upper bound: on non-validator nodes ancient headers below the sync pivot are never
/// downloaded, so the walk is capped at the pivot instead of waiting for blocks that will never arrive.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class CanonicalChainTests : BaseTest
{
    private const int BatchSize = 500;
    private const string ZeroHash = "0x0000000000000000000000000000000000000000000000000000000000000000";

    // Below this many walkable blocks the check is meaningless; keep waiting for the head to advance instead.
    private const int MinAdaptiveDepth = 128;
    // Don't walk right up to the pivot block itself — bodies/headers immediately at the pivot edge can lag.
    private const int PivotSafetyMargin = 64;
    // Upper bound on the sync wait — must cover a full initial snap sync (mainnet on g6-standard-16 takes hours).
    // Without it this loop burned the full 20 h job timeout on lanes whose node could never serve the requested
    // depth (JsonRpcGL/ML, 1.39.2 validation 2026-07-16).
    private static readonly TimeSpan MaxSyncWait = TimeSpan.FromMinutes(360);

    [NethermindTestCase(5_000_000, "finalized", Category = "CanonicalChain")]
    public async Task CanonicalChain_WhenWalkingFromTag_ByNumberMatchesByHashChain(int depth, string startTag)
    {
        (EthBlockResult startBlock, int effectiveDepth) = await WaitForBlockWithDepth(startTag, depth);

        TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Start: #{HexToLong(startBlock.Number)}  hash={startBlock.Hash}  walking back {effectiveDepth} blocks");

        List<(long Number, string Hash)> truthChain = await BuildTruthChain(startBlock.Hash, effectiveDepth);
        TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Phase 1 complete: {truthChain.Count} block(s) walked by parentHash");

        Dictionary<long, string?> byNumberMap = await FetchBlocksByNumber(
            truthChain.Select(t => t.Number).ToList());
        TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Phase 2 complete: {byNumberMap.Count} block(s) fetched by number");

        List<(long Height, string Expected, string? Actual)> mismatches = FindMismatches(truthChain, byNumberMap);

        foreach ((long height, string expected, string? actual) in mismatches)
        {
            TestLoggerContext.Logger.Error(
                $"[CANONICAL-CHECK] MISMATCH at height {height}: by-hash={expected}  by-number={actual}");
        }

        (long Height, string Expected, string? Actual) first = mismatches.FirstOrDefault();
        Assert.That(mismatches, Is.Empty,
            $"{mismatches.Count} canonical mismatch(es) — eth_getBlockByNumber returns wrong block after reorg. " +
            $"First: height={first.Height}, expected={first.Expected}, got={first.Actual}");
    }

    private static Task<EthBlockResult?> FetchBlockByNumberOrTag(string numberOrTag) =>
        FetchBlock("eth_getBlockByNumber", $"\"{numberOrTag}\", false");

    private static async Task<(EthBlockResult StartBlock, int Depth)> WaitForBlockWithDepth(string tag, int requiredDepth)
    {
        TimeSpan pollInterval = TimeSpan.FromSeconds(30);
        DateTime deadline = DateTime.UtcNow + MaxSyncWait;
        string lastStatus = "no status yet";

        // Headers below the sync pivot are never downloaded, so a walk deeper than
        // head-minus-pivot can never be served — cap the depth there instead of waiting forever.
        //
        // This is a property of the PIVOT, not of Sync.NonValidatorNode. Gating it on that flag
        // meant the cap never engaged on an ordinary snap-synced node: JsonRpcGL and JsonRpcML
        // set no NonValidatorNode flag, so pivot stayed 0, lowestReachable stayed 0, and both
        // lanes attempted the full 5,000,000-block walk their node could never serve. Each then
        // burned the entire 360-minute MaxSyncWait and failed — run 35280402155, 6h03m and 6h23m,
        // with the node healthy and at tip throughout (mainnet processed 26001918 in 30 ms while
        // the walk was still waiting).
        //
        // A node that genuinely holds every header — archive, or full sync from genesis — reports
        // Sync.PivotNumber = 0 and is therefore still walked uncapped, which is the intent.
        //
        // The pivot is read lazily, from inside the retry loop, because the node's JSON-RPC is not
        // guaranteed to be listening when this method is entered. GetPivotNumber has no internal
        // guard (unlike IsNonValidatorNode, which swallows and returns false), so reading it here
        // threw straight out of the test before the loop could absorb it: run 35357693250 killed
        // JsonRpcGL and JsonRpcML ~16 s after node start while the node was healthily syncing
        // headers. Deferring the read puts it under the loop's existing catch-and-retry.
        long pivot = 0;
        bool pivotKnown = false;
        bool cappedByPivot = false;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                // Canonical markers are only trustworthy on a synced node; during snap/fast sync the
                // by-number index is still being (re)written, so don't judge canonicality mid-sync.
                if (!NodeInfo.IsFullySynced(TestLoggerContext.Logger))
                {
                    lastStatus = "node not fully synced yet";
                }
                else if (await FetchBlockByNumberOrTag(tag) is not EthBlockResult startBlock)
                {
                    lastStatus = $"{tag} = null";
                }
                else
                {
                    long startNumber = HexToLong(startBlock.Number);

                    if (!pivotKnown)
                    {
                        pivot = await NodeInfo.GetPivotNumber(TestLoggerContext.Logger);
                        pivotKnown = true;
                        cappedByPivot = pivot > 0;
                        if (cappedByPivot)
                            TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Sync pivot {pivot} detected (NonValidatorNode={await IsNonValidatorNode()}): depth will be capped at the pivot");
                        else
                            TestLoggerContext.Logger.Info("[CANONICAL-CHECK] Sync.PivotNumber=0 (archive or full-sync node): walking uncapped");
                    }

                    long lowestReachable = cappedByPivot ? pivot + PivotSafetyMargin : 0;
                    int effectiveDepth = (int)Math.Min(requiredDepth, startNumber - lowestReachable);

                    if (effectiveDepth < MinAdaptiveDepth)
                    {
                        lastStatus = $"only {effectiveDepth} walkable block(s) above pivot {pivot}; waiting for head to advance (need >= {MinAdaptiveDepth})";
                    }
                    else
                    {
                        long deepNumber = startNumber - effectiveDepth;
                        EthBlockResult? deepBlock = await FetchBlockByNumberOrTag($"0x{deepNumber:X}");
                        if (deepBlock is not null)
                        {
                            if (effectiveDepth < requiredDepth)
                                TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Depth capped to {effectiveDepth} (requested {requiredDepth}): headers below pivot {pivot} are not downloaded on this node");
                            return (startBlock, effectiveDepth);
                        }
                        lastStatus = $"block #{deepNumber} not yet locally available (backward header sync in progress)";
                    }
                }
                TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Waiting: {lastStatus}");
            }
            catch (Exception ex)
            {
                lastStatus = ex.Message;
                TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Waiting for node: {ex.Message}");
            }
            await Task.Delay(pollInterval);
        }

        Assert.Fail($"Node could not serve a {requiredDepth}-deep block walk from '{tag}' within {MaxSyncWait.TotalMinutes:F0} min. " +
                    $"Last status: {lastStatus}. If this node is expected to backfill that deep, raise MaxSyncWait; " +
                    "otherwise lower the test-case depth or run against a node that keeps ancient headers.");
        throw new Exception("unreachable — Assert.Fail throws");
    }

    private static async Task<bool> IsNonValidatorNode()
    {
        try
        {
            var configValue = await NodeInfo.GetConfigValue(TestLoggerContext.Logger, "Sync", "NonValidatorNode");
            return bool.TryParse(configValue?.Result, out bool nonValidator) && nonValidator;
        }
        catch (Exception ex)
        {
            TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Could not read Sync.NonValidatorNode ({ex.Message}); assuming validator node");
            return false;
        }
    }

    private static async Task<List<(long Number, string Hash)>> BuildTruthChain(string startHash, int depth)
    {
        List<(long Number, string Hash)> truthChain = new(depth);
        string currentHash = startHash;

        for (int i = 0; i < depth; i++)
        {
            EthBlockResult? block = await FetchBlockByHash(currentHash);
            if (block is null)
            {
                throw new Exception(
                    $"eth_getBlockByHash returned null at iteration {i} for hash {currentHash}. " +
                    $"Walked {truthChain.Count}/{depth} blocks. The node likely hasn't backward-synced headers " +
                    "to that depth yet. Wait longer or run in archive mode.");
            }

            truthChain.Add((HexToLong(block.Number), block.Hash));

            if (IsGenesis(block)) break;
            currentHash = block.ParentHash;
        }

        return truthChain;
    }

    private static Task<EthBlockResult?> FetchBlockByHash(string blockHash) =>
        FetchBlock("eth_getBlockByHash", $"\"{blockHash}\", false");

    private static async Task<Dictionary<long, string?>> FetchBlocksByNumber(List<long> numbers)
    {
        Dictionary<long, string?> result = new(numbers.Count);

        for (int offset = 0; offset < numbers.Count; offset += BatchSize)
        {
            List<long> chunk = numbers.GetRange(offset, Math.Min(BatchSize, numbers.Count - offset));
            List<string> paramsList = chunk.Select(n => $"\"0x{n:X}\", false").ToList();

            Tuple<string, TimeSpan, bool> batchResponse = await HttpExecutor.ExecuteBatchedNethermindJsonRpcCommand(
                "eth_getBlockByNumber", paramsList, TestItems.RpcAddress, TestLoggerContext.Logger);

            string batchResponseBody = batchResponse.Item1;
            List<EthBlockResponse>? responses = JsonConvert.DeserializeObject<List<EthBlockResponse>>(batchResponseBody);
            if (responses is null) continue;

            // Batch IDs are 1-based sequential per HttpExecutor; Id-1 = index into chunk
            foreach (EthBlockResponse item in responses)
            {
                int chunkIndex = item.Id - 1;
                if (chunkIndex >= 0 && chunkIndex < chunk.Count)
                    result[chunk[chunkIndex]] = item.Result?.Hash;
            }
        }

        return result;
    }

    private static List<(long Height, string Expected, string? Actual)> FindMismatches(
        List<(long Number, string Hash)> truthChain,
        Dictionary<long, string?> byNumberMap)
    {
        List<(long Height, string Expected, string? Actual)> mismatches = new();

        foreach ((long number, string hash) in truthChain)
        {
            string? byNumberHash = byNumberMap.GetValueOrDefault(number);
            if (byNumberHash != hash)
                mismatches.Add((number, hash, byNumberHash));
        }

        return mismatches;
    }

    private static async Task<EthBlockResult?> FetchBlock(string method, string parameters)
    {
        Tuple<string, TimeSpan, bool> response = await HttpExecutor.ExecuteNethermindJsonRpcCommand(
            method, parameters, TestItems.RpcAddress, TestLoggerContext.Logger);

        bool isSuccess = response.Item3;
        if (!isSuccess)
            throw new Exception($"{method} failed — no node reachable at {TestItems.RpcAddress}");

        string responseBody = response.Item1;
        JsonRpcHelper.TryDeserializeReponse<EthBlockResponse>(responseBody, out IRpcResponse? deserialized);
        return (deserialized as EthBlockResponse)?.Result;
    }

    private static long HexToLong(string hex) => Convert.ToInt64(hex, 16);

    private static bool IsGenesis(EthBlockResult block) => block.ParentHash == ZeroHash;
}
