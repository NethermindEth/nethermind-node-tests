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
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.None)]
public class CanonicalChainTests : BaseTest
{
    private const int BatchSize = 500;
    private const string ZeroHash = "0x0000000000000000000000000000000000000000000000000000000000000000";

    [NethermindTestCase(5_000_000, "finalized", Category = "CanonicalChain")]
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
                EthBlockResult? startBlock = await FetchBlockByNumberOrTag(tag);
                if (startBlock is null || HexToLong(startBlock.Number) < requiredDepth)
                {
                    string status = startBlock is null ? "null" : $"#{HexToLong(startBlock.Number)} (need >= {requiredDepth})";
                    TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Waiting for sync: {tag} = {status}");
                }
                else
                {
                    long deepNumber = HexToLong(startBlock.Number) - requiredDepth;
                    EthBlockResult? deepBlock = await FetchBlockByNumberOrTag($"0x{deepNumber:X}");
                    if (deepBlock is not null)
                    {
                        return startBlock;
                    }
                    TestLoggerContext.Logger.Info($"[CANONICAL-CHECK] Waiting for backward header sync: block #{deepNumber} not yet locally available");
                }
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
