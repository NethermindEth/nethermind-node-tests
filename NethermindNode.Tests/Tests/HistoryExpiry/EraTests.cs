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
    private const string VolumeMapping = "${EC_DATA_DIR}/era:" + EraDirectory;
    private const string ComposeFile = "/root/docker-compose.yml";
    private const string SepoliaRemoteBaseUrl = "https://data.ethpandaops.io/erae/sepolia/";

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
        NodeConfig.AddElFlag("EraE", "RemoteBaseUrl", SepoliaRemoteBaseUrl);
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
