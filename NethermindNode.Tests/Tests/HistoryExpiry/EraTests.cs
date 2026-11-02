// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NLog;

namespace NethermindNode.Tests.HistoryExpiry;

/// <summary>
/// Imports a bounded range of pre-merge EraE archives from a public store into a snap-synced node,
/// exports the same range from the node database, and checks both directions against reference data.
/// </summary>
/// <remarks>
/// Snap sync does not download pre-merge bodies and receipts (AncientBodiesBarrier and AncientReceiptsBarrier
/// are at the merge block on Sepolia), so the range is empty before the import and can only be filled by it.
/// The test changes the Sedge docker-compose file and always restores it at the end.
/// </remarks>
[TestFixture]
[NonParallelizable]
[Category("EraE2E")]
public class EraTests : BaseTest
{
    private const string ContainerEraDirectory = "/era";
    private const string ContainerImportDirectory = ContainerEraDirectory + "/import";
    private const string ContainerExportDirectory = ContainerEraDirectory + "/export";
    private const string ExportExtension = ".ere";
    private const string ChecksumsFileName = "checksums_sha256.txt";
    private const string AccumulatorsFileName = "accumulators.txt";
    private const ushort AccumulatorRootEntryType = 0x07;
    private const long EpochSize = 8192;

    private static readonly TimeSpan JobTimeout = TimeSpan.FromHours(2);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private const int PrunedHistoryUnavailable = 4444;

    private static readonly string[] JobFailureMarkers = ["EraException", "EraImportException"];

    private sealed record ReferenceBlock(long Number, string Hash, int TransactionCount);

    private sealed record EraProfile(string Network, string RemoteBaseUrl, long FirstEpoch, long LastEpoch, ReferenceBlock[] Blocks)
    {
        public long From => FirstEpoch * EpochSize;
        public long To => (LastEpoch + 1) * EpochSize - 1;
    }

    // Two full pre-merge epochs. Hashes and transaction counts come from a public Sepolia RPC. The hashes of the last
    // block in each epoch also match the file names in the remote checksum manifest.
    private static readonly Dictionary<NodeInfo.NetworkType, EraProfile> Profiles = new()
    {
        [NodeInfo.NetworkType.Sepolia] = new(
            "sepolia",
            "https://data.ethpandaops.io/erae/sepolia/",
            FirstEpoch: 175,
            LastEpoch: 176,
            [
                new(1_433_600, "0xcbcad93676594fbf8743513c6a9955ac34c0cd72809795eac2a885588c40d02c", 3),
                new(1_435_150, "0x035260501993f01f5e3df00e8e45a2e586fcb4a139e22cc12854ce771f730d0b", 12),
                new(1_438_150, "0x1189bf5323b8135cae3d598b60d600877a48fbd3794c60d2d4e5c2081a4323ca", 9),
                new(1_441_791, "0xa7bdef90e9fc06c6524fa3d8b4785c8ccd78ded84568f372336cd11f65b28e71", 0),
                new(1_441_792, "0xa909171c7b312d61e426d10dcdb3a6bfe75ecd4cf5f04e3bdb2f9ec135fe11eb", 1),
                new(1_444_150, "0x9d0caff95a66b9b7a6df4927641cf207589d1f468a54f3ee023048d61564ccbd", 2),
                new(1_449_983, "0xfb1459acdac5902a70341ca26b0981b619bcea604791edfa0e9f00d0cceb490d", 0),
            ]),
    };

    private static string HostEraDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "era-e2e-test");

    [NethermindTest]
    public async Task ShouldImportFromRemoteAndExportSameHistory()
    {
        Logger logger = TestLoggerContext.Logger;
        string containerName = ConfigurationHelper.Instance["execution-container-name"];

        NodeInfo.WaitForNodeToBeReady(logger);
        NodeInfo.WaitForNodeToBeSynced(logger);

        NodeInfo.NetworkType network = await NodeInfo.GetNetworkType(logger);
        if (!Profiles.TryGetValue(network, out EraProfile? profile))
        {
            Assert.Ignore($"No EraE reference data for {network}.");
            return;
        }

        foreach (ReferenceBlock block in profile.Blocks)
        {
            Assert.That(await IsBlockBodyMissing(block.Number, logger), Is.True,
                $"Block {block.Number} already has a body before the import, so the import cannot be verified.");
        }

        string hostImportDirectory = Path.Combine(HostEraDirectory, "import");
        string hostExportDirectory = Path.Combine(HostEraDirectory, "export");
        ResetDirectory(HostEraDirectory);
        Directory.CreateDirectory(hostImportDirectory);
        Directory.CreateDirectory(hostExportDirectory);
        NodeConfig.Backup();

        try
        {
            NodeConfig.AddVolume($"{HostEraDirectory}:{ContainerEraDirectory}");
            NodeConfig.SetElFlag("EraE", "ImportDirectory", ContainerImportDirectory);
            NodeConfig.SetElFlag("EraE", "RemoteBaseUrl", profile.RemoteBaseUrl);
            NodeConfig.SetElFlag("EraE", "From", profile.From.ToString());
            NodeConfig.SetElFlag("EraE", "To", profile.To.ToString());
            RecreateExecutionContainer(containerName, logger);
            WaitForEraJob(containerName, "Finished EraE import", logger);
            NodeInfo.WaitForNodeToBeReady(logger);

            await AssertReferenceBlocks(profile, "after import", logger);

            NodeConfig.RemoveElFlag("EraE", "ImportDirectory");
            NodeConfig.RemoveElFlag("EraE", "RemoteBaseUrl");
            NodeConfig.SetElFlag("EraE", "ExportDirectory", ContainerExportDirectory);
            RecreateExecutionContainer(containerName, logger);
            WaitForEraJob(containerName, "Finished EraE export", logger);
            NodeInfo.WaitForNodeToBeReady(logger);

            AssertExport(profile, hostImportDirectory, hostExportDirectory, logger);
            await AssertReferenceBlocks(profile, "after export", logger);
        }
        finally
        {
            NodeConfig.Restore();
            RecreateExecutionContainer(containerName, logger);
            NodeInfo.WaitForNodeToBeReady(logger);
            Directory.Delete(HostEraDirectory, recursive: true);
        }
    }

    private static async Task AssertReferenceBlocks(EraProfile profile, string stage, Logger logger)
    {
        foreach (ReferenceBlock expected in profile.Blocks)
        {
            JsonElement block = (await ExecuteRpc("eth_getBlockByNumber", $"\"0x{expected.Number:x}\", false", logger)).Result;
            Assert.That(block.ValueKind, Is.EqualTo(JsonValueKind.Object), $"Block {expected.Number} is missing {stage}.");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(block.GetProperty("hash").GetString(), Is.EqualTo(expected.Hash),
                    $"Block {expected.Number} hash {stage}.");
                Assert.That(block.GetProperty("transactions").GetArrayLength(), Is.EqualTo(expected.TransactionCount),
                    $"Block {expected.Number} transaction count {stage}.");
            }

            JsonElement receipts = (await ExecuteRpc("eth_getBlockReceipts", $"\"0x{expected.Number:x}\"", logger)).Result;
            Assert.That(receipts.ValueKind, Is.EqualTo(JsonValueKind.Array), $"Block {expected.Number} receipts are missing {stage}.");
            Assert.That(receipts.GetArrayLength(), Is.EqualTo(expected.TransactionCount), $"Block {expected.Number} receipt count {stage}.");
            foreach (JsonElement receipt in receipts.EnumerateArray())
            {
                Assert.That(receipt.GetProperty("blockHash").GetString(), Is.EqualTo(expected.Hash),
                    $"Block {expected.Number} receipt block hash {stage}.");
            }
        }

        logger.Info($"All {profile.Blocks.Length} reference blocks match {stage}.");
    }

    private static void AssertExport(EraProfile profile, string hostImportDirectory, string hostExportDirectory, Logger logger)
    {
        Dictionary<string, string> checksums = ReadManifest(Path.Combine(hostExportDirectory, ChecksumsFileName));
        Dictionary<string, string> accumulators = ReadManifest(Path.Combine(hostExportDirectory, AccumulatorsFileName));

        for (long epoch = profile.FirstEpoch; epoch <= profile.LastEpoch; epoch++)
        {
            ReferenceBlock lastBlock = profile.Blocks.Single(b => b.Number == (epoch + 1) * EpochSize - 1);
            string hashPrefix = lastBlock.Hash[2..10];
            string exportedName = $"{profile.Network}-{epoch:D5}-{hashPrefix}-noproofs{ExportExtension}";
            string exportedPath = Path.Combine(hostExportDirectory, exportedName);
            Assert.That(File.Exists(exportedPath), Is.True, $"Expected export file {exportedName} is missing.");

            string remotePath = Directory.GetFiles(hostImportDirectory, $"{profile.Network}-{epoch:D5}-*").Single();
            Assert.That(Path.GetFileName(remotePath), Does.StartWith($"{profile.Network}-{epoch:D5}-{hashPrefix}"),
                $"Downloaded file for epoch {epoch} names a different last block.");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(checksums.GetValueOrDefault(exportedName), Is.EqualTo(Sha256Hex(exportedPath)),
                    $"{ChecksumsFileName} does not match the content of {exportedName}.");
                Assert.That(accumulators.GetValueOrDefault(exportedName), Is.EqualTo(ReadAccumulatorRoot(remotePath)),
                    $"Accumulator root of {exportedName} differs from the downloaded archive.");
                Assert.That(ReadAccumulatorRoot(exportedPath), Is.EqualTo(ReadAccumulatorRoot(remotePath)),
                    $"Accumulator root inside {exportedName} differs from the downloaded archive.");
            }
        }

        int exportedFiles = Directory.GetFiles(hostExportDirectory, "*" + ExportExtension).Length;
        Assert.That(exportedFiles, Is.EqualTo(profile.LastEpoch - profile.FirstEpoch + 1), "Unexpected number of exported files.");
        logger.Info($"Export of {exportedFiles} file(s) matches the downloaded archives.");
    }

    private static void RecreateExecutionContainer(string containerName, Logger logger)
    {
        DockerCommands.StopDockerContainer(containerName, logger);
        DockerCommands.ComposeUp("execution", NodeConfig.ComposeFilePath, logger);
    }

    /// <summary>
    /// The import and export run in a startup step. A failed step stops the node, and the restart policy
    /// starts it again, so a restart counts as a failure.
    /// </summary>
    private static void WaitForEraJob(string containerName, string successMarker, Logger logger)
    {
        logger.Info($"Waiting for '{successMarker}'.");
        DateTime deadline = DateTime.UtcNow + JobTimeout;
        while (DateTime.UtcNow < deadline)
        {
            List<string> lines = DockerCommands.GetDockerLogs(containerName).ToList();
            string? success = lines.FirstOrDefault(l => l.Contains(successMarker, StringComparison.Ordinal));
            if (success is not null)
            {
                logger.Info($"Found: {success}");
                return;
            }

            string? failure = lines.FirstOrDefault(l => JobFailureMarkers.Any(m => l.Contains(m, StringComparison.Ordinal)));
            string restarts = DockerCommands.GetDockerDetails(containerName, "{{.RestartCount}}", logger).Trim();
            string status = DockerCommands.GetDockerContainerStatus(containerName, logger).Trim();
            if (failure is not null || restarts != "0" || !status.Contains("running", StringComparison.Ordinal))
            {
                Assert.Fail($"EraE job failed (status {status}, restarts {restarts}). Last log lines:{Environment.NewLine}" +
                    string.Join(Environment.NewLine, lines.TakeLast(40)));
            }

            Thread.Sleep(PollInterval);
        }

        Assert.Fail($"'{successMarker}' not found within {JobTimeout}.");
    }

    /// <summary>A missing body gives either a null block or the pruned-history error, depending on the node version.</summary>
    private static async Task<bool> IsBlockBodyMissing(long number, Logger logger)
    {
        (JsonElement result, int? errorCode) = await ExecuteRpc("eth_getBlockByNumber", $"\"0x{number:x}\", false", logger, allowError: true);
        return errorCode == PrunedHistoryUnavailable || (errorCode is null && result.ValueKind == JsonValueKind.Null);
    }

    private static async Task<(JsonElement Result, int? ErrorCode)> ExecuteRpc(string method, string parameters, Logger logger, bool allowError = false)
    {
        Tuple<string, TimeSpan, bool> response =
            await HttpExecutor.ExecuteNethermindJsonRpcCommand(method, parameters, NodeInfo.apiBaseUrl, logger);
        using JsonDocument document = JsonDocument.Parse(response.Item1);
        if (document.RootElement.TryGetProperty("error", out JsonElement error))
        {
            if (!allowError)
            {
                Assert.Fail($"{method}({parameters}) returned an error: {error}");
            }

            return (default, error.GetProperty("code").GetInt32());
        }

        return (document.RootElement.GetProperty("result").Clone(), null);
    }

    private static Dictionary<string, string> ReadManifest(string path)
    {
        Assert.That(File.Exists(path), Is.True, $"{Path.GetFileName(path)} is missing from the export.");
        Dictionary<string, string> entries = new();
        foreach (string line in File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            entries[parts[^1]] = NormalizeHex(parts[0]);
        }

        return entries;
    }

    private static string Sha256Hex(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    /// <summary>Reads the AccumulatorRoot entry of an e2store file: 2-byte type, 4-byte length, 2 reserved bytes, data.</summary>
    private static string ReadAccumulatorRoot(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[8];
        while (stream.Position < stream.Length)
        {
            stream.ReadExactly(header);
            ushort type = BinaryPrimitives.ReadUInt16LittleEndian(header);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(header[2..]);
            if (type == AccumulatorRootEntryType)
            {
                byte[] root = new byte[length];
                stream.ReadExactly(root);
                return Convert.ToHexStringLower(root);
            }

            stream.Seek(length, SeekOrigin.Current);
        }

        Assert.Fail($"{Path.GetFileName(path)} has no AccumulatorRoot entry.");
        return string.Empty;
    }

    private static string NormalizeHex(string value) =>
        (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value).ToLowerInvariant();

    private static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }
}
