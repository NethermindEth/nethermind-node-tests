// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NLog;

namespace NethermindNode.Tests.HistoryExpiry;

[TestFixture]
[NonParallelizable]
[Category("EraExportImport")]
public class EraTests : BaseTest
{
    private const string EraDirectory = "/era";
    private const string ImportDirectory = EraDirectory + "/import";
    private const string ExportDirectory = EraDirectory + "/export";
    private const string VolumeMapping = "/mnt/era-stavros:" + EraDirectory;
    private const string ComposeFile = "/root/docker-compose.yml";
    private const string RemoteBaseUrl = "https://data.ethpandaops.io/erae/mainnet/";

    // Pre-merge blocks: snap sync does not download bodies for these.
    // After EraE import they must be accessible with correct hashes.
    private static readonly (long Number, string Hash)[] PreMergeBlocks =
    [
        (1L,          "0x88e96d4537bea4d9c05d12549907b32561d3bf31f45aae734cdc119f13406cb6"),
        (1_920_000L,  "0x4985f5ca3d2afbec36529aa96f74de3cc10a2a4a6c44f2157a57d2c6059a11bb"), // DAO fork
        (15_537_393L, "0x55b11b918355b1ef9c5db810302ebad0bf2544255b530cdce90674d5887bb286"), // last PoW block
    ];

    // Post-merge blocks: accessible from snap sync regardless of EraE import.
    // Verified here to confirm import/export did not corrupt existing DB state.
    private static readonly (long Number, string Hash)[] PostMergeBlocks =
    [
        (15_537_394L, "0x56a9bb0302da44b8c0b3df540781424684c3af04d0b7a38d72842b762076a664"), // first PoS block
        (16_000_000L, "0x3dc4ef568ae2635db1419c5fec55c4a9322c05302ae527cd40bff380c1d465dd"),
    ];

    [NethermindTest]
    [Category("EraImport")]
    public async Task ShouldImportFromRemote()
    {
        Logger logger = TestLoggerContext.Logger;
        string containerName = ConfigurationHelper.Instance["execution-container-name"];

        NodeInfo.WaitForNodeToBeReady(logger);
        NodeInfo.WaitForNodeToBeSynced(logger);
        logger.Info("Node snap sync complete.");

        // Verify pre-merge blocks are NOT accessible before import
        foreach ((long number, string _) in PreMergeBlocks)
        {
            string response = await QueryBlock(number, logger);
            Assert.That(response, Does.Contain("\"result\":null"),
                $"Block {number} should not be accessible before EraE import.");
        }
        logger.Info("Confirmed pre-merge blocks absent before import.");

        long lastImportedBlock = await ImportFromRemote(containerName, logger);

        // Verify pre-merge blocks ARE accessible with correct hashes after import
        foreach ((long number, string expectedHash) in PreMergeBlocks)
        {
            string response = await QueryBlock(number, logger);
            Assert.That(response, Does.Contain(expectedHash),
                $"Block {number} hash mismatch after import.");
        }
        logger.Info("All pre-merge blocks verified after import.");

        // Verify post-merge blocks are unaffected
        foreach ((long number, string expectedHash) in PostMergeBlocks)
        {
            string response = await QueryBlock(number, logger);
            Assert.That(response, Does.Contain(expectedHash),
                $"Post-merge block {number} hash mismatch — DB may be corrupt.");
        }
        logger.Info("All post-merge blocks verified.");
    }

    [NethermindTest]
    [Category("EraExport")]
    public async Task ShouldExportFromDb()
    {
        Logger logger = TestLoggerContext.Logger;
        string containerName = ConfigurationHelper.Instance["execution-container-name"];

        NodeInfo.WaitForNodeToBeReady(logger);

        // Verify pre-merge blocks are in DB (import must have run first)
        foreach ((long number, string expectedHash) in PreMergeBlocks)
        {
            string response = await QueryBlock(number, logger);
            Assert.That(response, Does.Contain(expectedHash),
                $"Block {number} not found — run EraImport test first.");
        }

        DeleteImportedEraFiles(logger);

        // Use the last known pre-merge block as the export upper bound
        long exportTo = PreMergeBlocks[^1].Number;
        await ExportFromDb(containerName, exportTo, logger);

        // Verify era files were written to disk
        string eraHostPath = DockerCommands.GetEraDataPath(logger);
        string[] exportedFiles = Directory.GetFiles(Path.Combine(eraHostPath, "export"), "*.era", SearchOption.AllDirectories);
        Assert.That(exportedFiles.Length, Is.GreaterThan(0), "No era files found in export directory.");
        logger.Info($"Export produced {exportedFiles.Length} era file(s).");

        // Verify all blocks still correct after export
        foreach ((long number, string expectedHash) in PreMergeBlocks)
        {
            string response = await QueryBlock(number, logger);
            Assert.That(response, Does.Contain(expectedHash),
                $"Pre-merge block {number} hash mismatch after export.");
        }
        foreach ((long number, string expectedHash) in PostMergeBlocks)
        {
            string response = await QueryBlock(number, logger);
            Assert.That(response, Does.Contain(expectedHash),
                $"Post-merge block {number} hash mismatch after export.");
        }
        logger.Info("All blocks verified after export.");
    }

    [NethermindTest]
    [Category("EraE2E")]
    public async Task ShouldImportFromRemoteAndExportFromDbWithMatchingBlockData()
    {
        Logger logger = TestLoggerContext.Logger;
        string containerName = ConfigurationHelper.Instance["execution-container-name"];

        NodeInfo.WaitForNodeToBeReady(logger);
        NodeInfo.WaitForNodeToBeSynced(logger);
        logger.Info("Node snap sync complete.");

        long lastImportedBlock = await ImportFromRemote(containerName, logger);

        foreach ((long number, string expectedHash) in PreMergeBlocks)
        {
            string response = await QueryBlock(number, logger);
            Assert.That(response, Does.Contain(expectedHash),
                $"Block {number} hash mismatch after import.");
        }

        DeleteImportedEraFiles(logger);

        await ExportFromDb(containerName, lastImportedBlock, logger);

        string eraHostPath = DockerCommands.GetEraDataPath(logger);
        string[] exportedFiles = Directory.GetFiles(Path.Combine(eraHostPath, "export"), "*.era", SearchOption.AllDirectories);
        Assert.That(exportedFiles.Length, Is.GreaterThan(0), "No era files found in export directory.");

        foreach ((long number, string expectedHash) in PreMergeBlocks.Concat(PostMergeBlocks))
        {
            string response = await QueryBlock(number, logger);
            Assert.That(response, Does.Contain(expectedHash),
                $"Block {number} hash mismatch after export.");
        }
        logger.Info("Full E2E import/export verified.");
    }

    private static async Task<long> ImportFromRemote(string containerName, Logger logger)
    {
        logger.Info("Configuring remote era import.");

        NodeConfig.AddVolume(VolumeMapping);
        NodeConfig.AddElFlag("EraE", "ImportDirectory", ImportDirectory);
        NodeConfig.AddElFlag("EraE", "RemoteBaseUrl", RemoteBaseUrl);

        DockerCommands.StopDockerContainer(containerName, logger);
        DockerCommands.ComposeUp("execution", ComposeFile, logger);
        NodeInfo.WaitForNodeToBeReady(logger);

        string logLine = await WaitForLog(containerName, "Finished EraE import", logger);
        logger.Info("Remote era import finished.");

        // Parse last imported block from: "Finished EraE import from {from} to {to}"
        const string toMarker = " to ";
        int toIndex = logLine.LastIndexOf(toMarker, StringComparison.Ordinal);
        long lastImportedBlock = long.Parse(logLine[(toIndex + toMarker.Length)..].Trim());
        logger.Info($"Last imported block: {lastImportedBlock}");
        return lastImportedBlock;
    }

    private static void DeleteImportedEraFiles(Logger logger)
    {
        string eraHostPath = DockerCommands.GetEraDataPath(logger);
        CommandExecutor.RemoveDirectory(Path.Combine(eraHostPath, "import"), logger);
        logger.Info("Deleted downloaded era files.");
    }

    private static async Task ExportFromDb(string containerName, long to, Logger logger)
    {
        logger.Info("Configuring era export from DB.");

        NodeConfig.RemoveElFlag("EraE", "ImportDirectory");
        NodeConfig.RemoveElFlag("EraE", "RemoteBaseUrl");
        NodeConfig.RemoveElFlag("EraE", "From");
        NodeConfig.RemoveElFlag("EraE", "To");
        NodeConfig.AddElFlag("EraE", "ExportDirectory", ExportDirectory);
        NodeConfig.AddElFlag("EraE", "From", "0");
        NodeConfig.AddElFlag("EraE", "To", to.ToString());

        DockerCommands.StopDockerContainer(containerName, logger);
        DockerCommands.ComposeUp("execution", ComposeFile, logger);
        NodeInfo.WaitForNodeToBeReady(logger);

        await WaitForLog(containerName, "Finished EraE export", logger);
        logger.Info("Era export finished.");
    }

    private static async Task<string> QueryBlock(long blockNumber, Logger logger)
    {
        string rpcParams = $"\"0x{blockNumber:X}\", true";
        Tuple<string, TimeSpan, bool> rpcResult = await HttpExecutor.ExecuteNethermindJsonRpcCommand(
            "eth_getBlockByNumber", rpcParams, NodeInfo.apiBaseUrl, logger);
        logger.Info($"Block {blockNumber} response: {rpcResult.Item1}");
        return rpcResult.Item1;
    }

    private static async Task<string> WaitForLog(string containerName, string expectedLog, Logger logger)
    {
        logger.Info($"Waiting for log: '{expectedLog}'");
        CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromHours(6));
        await foreach (string line in DockerCommands.GetDockerLogsAsync(containerName, expectedLog, true, cts.Token))
        {
            logger.Info($"Found log: {line}");
            return line;
        }
        throw new TimeoutException($"Log '{expectedLog}' not found within 6 hours.");
    }
}
