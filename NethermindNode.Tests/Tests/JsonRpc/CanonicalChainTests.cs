// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Core.RpcResponses;
using NethermindNode.Tests.CustomAttributes;
using Newtonsoft.Json;

namespace NethermindNode.Tests.JsonRpc;

/// <summary>
/// Verifies canonical chain integrity: eth_getBlockByNumber(N) must return the same block
/// as walking backward via parentHash from the chain head.
///
/// Reproduces the Nethermind canonical-mismatch bug where engine_forkchoiceUpdatedV3 to a
/// canonical ancestor leaves beacon-synced descendants with stale HasBlockOnMainChain=true
/// markers, causing eth_getBlockByNumber to return the wrong block after a reorg. The walk
/// goes deep (3M blocks) to surface stale markers left behind by historical reorgs.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class CanonicalChainTests : BaseTest
{
    private const int BatchSize = 500;
    private const string ZeroHash = "0x0000000000000000000000000000000000000000000000000000000000000000";

    [NethermindTestCase(10_000_000, "finalized", Category = "CanonicalChain")]
    public async Task CanonicalChain_WhenWalkingFromTag_ByNumberMatchesByHashChain(int depth, string startTag)
    {
        EthBlockResult startBlock = await WaitForBlockWithDepth(startTag, depth);

        TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Start: #{HexToLong(startBlock.Number)}  hash={startBlock.Hash}  walking back {depth} blocks");

        List<(long Number, string Hash)> truthChain = await BuildTruthChain(startBlock.Hash, depth);
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

    private static async Task<EthBlockResult> WaitForBlockWithDepth(string tag, int requiredDepth)
    {
        TimeSpan pollInterval = TimeSpan.FromSeconds(30);
        while (true)
        {
            try
            {
                EthBlockResult? block = await FetchBlockByNumberOrTag(tag);
                if (block is not null && HexToLong(block.Number) >= requiredDepth)
                {
                    return block;
                }
                string status = block is null ? "null" : $"#{HexToLong(block.Number)} (need >= {requiredDepth})";
                TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Waiting for sync: {tag} = {status}");
            }
            catch (Exception ex)
            {
                TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Waiting for node: {ex.Message}");
            }
            await Task.Delay(pollInterval);
        }
    }

    private static async Task<List<(long Number, string Hash)>> BuildTruthChain(string startHash, int depth)
    {
        List<(long Number, string Hash)> truthChain = new(depth);
        string currentHash = startHash;

        for (int i = 0; i < depth; i++)
        {
            EthBlockResult? block = await FetchBlockByHash(currentHash);
            if (block is null) break;

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
