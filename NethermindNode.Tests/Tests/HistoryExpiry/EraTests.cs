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

    [NethermindTest]
    [Category("EraImport")]
    public async Task ShouldImportFromRemote()
    {
        Logger logger = TestLoggerContext.Logger;
        string containerName = ConfigurationHelper.Instance["execution-container-name"];

        NodeInfo.WaitForNodeToBeReady(logger);
        NodeInfo.WaitForNodeToBeSynced(logger);
        logger.Info("Node snap sync complete.");

        long mergeBlockNumber = await NodeInfo.GetMergeBlockNumber();

        await ImportFromRemote(containerName, mergeBlockNumber, logger);

        // Verify a pre-merge block is accessible — snap sync would not have this
        string blockResponse = await QueryBlock(mergeBlockNumber / 2, logger);
        Assert.That(blockResponse, Does.Contain("\"result\""));
        Assert.That(blockResponse, Does.Not.Contain("\"result\":null"));
        logger.Info("Pre-merge block accessible after import.");
    }

    [NethermindTest]
    [Category("EraExport")]
    public async Task ShouldExportFromDbWithMatchingBlockData()
    {
        Logger logger = TestLoggerContext.Logger;
        string containerName = ConfigurationHelper.Instance["execution-container-name"];

        NodeInfo.WaitForNodeToBeReady(logger);
        long mergeBlockNumber = await NodeInfo.GetMergeBlockNumber();

        string referenceResponse = await QueryBlock(mergeBlockNumber / 2, logger);
        Assert.That(referenceResponse, Does.Contain("\"result\""));
        Assert.That(referenceResponse, Does.Not.Contain("\"result\":null"));

        DeleteImportedEraFiles(logger);

        await ExportFromDb(containerName, mergeBlockNumber, logger);

        // Verify exported era files exist on disk
        string eraHostPath = DockerCommands.GetEraDataPath(logger);
        string[] exportedFiles = Directory.GetFiles(Path.Combine(eraHostPath, "export"), "*.era", SearchOption.AllDirectories);
        Assert.That(exportedFiles.Length, Is.GreaterThan(0), "No era files found in export directory.");
        logger.Info($"Export produced {exportedFiles.Length} era file(s).");

        // Verify the block is still accessible after export
        string exportedResponse = await QueryBlock(mergeBlockNumber / 2, logger);
        Assert.That(exportedResponse, Is.EqualTo(referenceResponse));
    }

    [NethermindTest]
    public async Task ShouldImportFromRemoteAndExportFromDbWithMatchingBlockData()
    {
        Logger logger = TestLoggerContext.Logger;
        string containerName = ConfigurationHelper.Instance["execution-container-name"];

        NodeInfo.WaitForNodeToBeReady(logger);
        NodeInfo.WaitForNodeToBeSynced(logger);
        logger.Info("Node snap sync complete.");

        long mergeBlockNumber = await NodeInfo.GetMergeBlockNumber();

        await ImportFromRemote(containerName, mergeBlockNumber, logger);

        string referenceResponse = await QueryBlock(mergeBlockNumber / 2, logger);

        DeleteImportedEraFiles(logger);

        await ExportFromDb(containerName, mergeBlockNumber, logger);

        string exportedResponse = await QueryBlock(mergeBlockNumber / 2, logger);
        Assert.That(exportedResponse, Is.EqualTo(referenceResponse));
    }

    private static async Task ImportFromRemote(string containerName, long mergeBlockNumber, Logger logger)
    {
        logger.Info("Configuring remote era import.");

        NodeConfig.AddVolume(VolumeMapping);
        NodeConfig.AddElFlag("EraE", "ImportDirectory", ImportDirectory);
        NodeConfig.AddElFlag("EraE", "RemoteBaseUrl", RemoteBaseUrl);
        NodeConfig.AddElFlag("EraE", "From", "0");
        NodeConfig.AddElFlag("EraE", "To", mergeBlockNumber.ToString());

        DockerCommands.StopDockerContainer(containerName, logger);
        DockerCommands.ComposeUp("execution", ComposeFile, logger);
        NodeInfo.WaitForNodeToBeReady(logger);

        await WaitForLog(containerName, "Finished EraE import", logger);
        logger.Info("Remote era import finished.");
    }

    private static void DeleteImportedEraFiles(Logger logger)
    {
        string eraHostPath = DockerCommands.GetEraDataPath(logger);
        CommandExecutor.RemoveDirectory(Path.Combine(eraHostPath, "import"), logger);
        logger.Info("Deleted downloaded era files.");
    }

    private static async Task ExportFromDb(string containerName, long mergeBlockNumber, Logger logger)
    {
        logger.Info("Configuring era export from DB.");

        NodeConfig.RemoveElFlag("EraE", "ImportDirectory");
        NodeConfig.RemoveElFlag("EraE", "RemoteBaseUrl");
        NodeConfig.AddElFlag("EraE", "ExportDirectory", ExportDirectory);

        DockerCommands.StopDockerContainer(containerName, logger);
        DockerCommands.ComposeUp("execution", ComposeFile, logger);
        NodeInfo.WaitForNodeToBeReady(logger);

        await WaitForLog(containerName, "Finished EraE export", logger);
        logger.Info("Era export finished.");
    }

    private static async Task<string> QueryBlock(long blockNumber, Logger logger)
    {
        string rpcParams = $"{blockNumber}, true";
        Tuple<string, TimeSpan, bool> rpcResult = await HttpExecutor.ExecuteNethermindJsonRpcCommand(
            "eth_getBlockByNumber", rpcParams, NodeInfo.apiBaseUrl, logger);
        logger.Info($"Block {blockNumber} response: {rpcResult.Item1}");
        return rpcResult.Item1;
    }

    private static async Task WaitForLog(string containerName, string expectedLog, Logger logger)
    {
        logger.Info($"Waiting for log: '{expectedLog}'");
        CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromHours(6));
        await foreach (string line in DockerCommands.GetDockerLogsAsync(containerName, expectedLog, true, cts.Token))
        {
            logger.Info($"Found log: {line}");
            break;
        }
    }
}
