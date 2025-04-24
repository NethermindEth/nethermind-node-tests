using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using NethermindNode.Tests.JsonRpc;
using YamlDotNet.RepresentationModel;
using NLog;
using System.ComponentModel;
using System.Diagnostics;

namespace NethermindNode.Tests.HistoryExpiry;

internal class NodeConfig
{
    static string composePath = "docker-compose.yml";
    static YamlStream yaml = new YamlStream();


    public static void AddElFlag(string nameSpace, string key, string value)
    {
        Load();
        var flag = $"--{nameSpace}.{key}={value}";
        var commandNode = GetSeqNode("execution", "command");
        var size = commandNode.Children.Count;
        for (int i = 0; i < size; i++)
        {
            TestLoggerContext.Logger.Info($"Node[{i}]: {commandNode.Children[i]}");
        }

        commandNode.Add(new YamlScalarNode(flag));

        Save();
    }

    public static void RemoveElFlag(string nameSpace, string key)
    {
        Load();
        var flag = $"--{nameSpace}.{key}";
        var commandNode = GetSeqNode("execution", "command");
        var size = commandNode.Children.Count;
        for (int i = 0; i < size; i++)
        {
            if (commandNode.Children[i].ToString().Contains(flag))
            {
                TestLoggerContext.Logger.Info($"Found {flag}, removing");
                commandNode.Children.RemoveAt(i);
                break;
            }
        }

        Save();
    }

    public static void AddVolume(string volume)
    {
        Load();
        var volumes = GetSeqNode("execution", "volumes");
        volumes.Add(new YamlScalarNode(volume));
        Save();
    }

    private static void Load()
    {
        var path = GetComposeFilePath();
        using (var reader = new StreamReader(path))
        {
            yaml.Load(reader);
        }
    }
    private static void Save()
    {
        var path = GetComposeFilePath();
        using (var writer = new StreamWriter(path))
        {
            yaml.Save(writer, assignAnchors: false);
        }
    }

    private static YamlSequenceNode GetSeqNode(string service, string seqNode)
    {

        var serviceNode = GetServiceNode(service);
        return (YamlSequenceNode)serviceNode.Children[new YamlScalarNode(seqNode)];
    }


    private static YamlMappingNode GetServiceNode(string service)
    {
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        var services = (YamlMappingNode)root.Children[new YamlScalarNode("services")];
        return (YamlMappingNode)services.Children[new YamlScalarNode(service)];

    }

    private static string GetComposeFilePath()
    {
        var currentDir = AppContext.BaseDirectory;

        // Parent directory to the tests root
        var parentDir = Path.GetFullPath(Path.Combine(currentDir, "../../../../../"));
        TestLoggerContext.Logger.Info(parentDir);
        return Path.Combine(parentDir, composePath);
    }

    public static string ShellExec(string command, Logger logger)
    {
        var processInfo = new ProcessStartInfo("docker", $"{command}");
        string output = "";
        string error = "";

        logger.Debug("DOCKER command: " + processInfo.FileName + " " + command);
        processInfo.CreateNoWindow = true;
        processInfo.UseShellExecute = false;
        processInfo.RedirectStandardOutput = true;
        processInfo.RedirectStandardError = true;

        using (var process = new Process())
        {
            try
            {
                process.StartInfo = processInfo;
                process.Start();
                process.WaitForExit(30000);
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();

                if (!process.HasExited)
                {
                    process.Kill();
                }

                process.Close();

            }
            catch (Win32Exception e)
            {
                if (e.Message.Contains("An error occurred trying to start process 'docker' with working directory '/root'. No such file or directory"))
                {
                    return "";
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        logger.Debug("Docker output: " + output);
        logger.Debug("Docker error: " + error);
        return output;
    }
}




[TestFixture]
public class HistoryExpiryTests : BaseTest
{
    [NethermindTest]
    public async Task ExportImportTest()
    {
        var l = TestLoggerContext.Logger;
        var elInstance = "execution";
        // var elInstance = ConfigurationHelper.Instance["execution-container-name"];
        var eraDir = "/era";
        var volumeMap = "${EC_DATA_DIR}/era:" + eraDir;
        var isFastSync = await NodeInfo.GetConfigValue(l, "Sync", "FastSync");
        if (isFastSync.Result == null || bool.Parse(isFastSync.Result) == false)
        {
            throw new Exception("Node is down or Debug RPC is disabled or FullSync is not enabled. Double check your config!");
        }

        var mergeBlock = (await NodeInfo.GetMergeBlockNumber()).ToString();
        var execDataDir = DockerCommands.GetExecutionDataPath(l);

        l.Info("Waiting for sync..");
        NodeInfo.WaitForNodeToBeReady(l);
        NodeInfo.WaitForNodeToBeSynced(l);

        Thread.Sleep(120000);
        l.Info("Done with sync");

        var rpcParams = $"""[{(long.Parse(mergeBlock) / 2).ToString()}, true]""";
        var rpcResponse1 = await HttpExecutor.ExecuteNethermindJsonRpcCommand("eth_getBlockByNumber", rpcParams, TestItems.RpcAddress, l);
        l.Info($"Response1: ${rpcResponse1}");

        // Export
        l.Info("Starting era export");
        NodeConfig.AddVolume(volumeMap);
        NodeConfig.AddElFlag("Era", "ExportDirectory", eraDir);
        NodeConfig.AddElFlag("Era", "From", "0");
        NodeConfig.AddElFlag("Era", "To", mergeBlock);
        NodeConfig.ShellExec("stop", l);
        NodeConfig.ShellExec("up", l);
        // DockerCommands.StopDockerContainer(elInstance, l);
        // DockerCommands.StartDockerContainer(elInstance, l);
        l.Info("Waiting for export to finish");
        NodeInfo.WaitForNodeToBeReady(l);
        NodeInfo.WaitForNodeToBeSynced(l);
        l.Info("Done with export");

        var rpcResponse2 = await HttpExecutor.ExecuteNethermindJsonRpcCommand("eth_getBlockByNumber", rpcParams, TestItems.RpcAddress, l);
        l.Info($"Response2: ${rpcResponse2}");

        // Set up Import: 
        // - Remove Era export directory
        // - Use default snap sync
        // - Set ancient barriers to mergeBlock
        l.Info("Preparing for import");
        NodeConfig.RemoveElFlag("Era", "ExportDirectory");
        NodeConfig.RemoveElFlag("Sync", "FastSync");
        NodeConfig.AddElFlag("Era", "ImportDirectory", eraDir);
        NodeConfig.AddElFlag("Era", "TrustedAccumulatorFile", eraDir + "/accumulators.txt");
        NodeConfig.AddElFlag("Sync", "AncientBodiesBarrier", mergeBlock);
        NodeConfig.AddElFlag("Sync", "AncientReceiptsBarrier", mergeBlock);

        // Remove DB:
        l.Info("Removing DB");
        // DockerCommands.StopDockerContainer(elInstance, l);
        NodeConfig.ShellExec("stop", l);

        Thread.Sleep(60000);
        CommandExecutor.RemoveDirectory(execDataDir + "/nethermind_db", l);

        // Import
        l.Info("Starting Import");
        // DockerCommands.StartDockerContainer(elInstance, l);
        NodeConfig.ShellExec("up", l);
        NodeInfo.WaitForNodeToBeReady(l);
        NodeInfo.WaitForNodeToBeSynced(l);
        l.Info("Done with import");

        // Check block production :shrug:
        var blockProduction = DockerCommands.GetDockerLogs(elInstance, "Produced ");
        Assert.That(blockProduction.Count() > 0, "No block production after sync in simulation mode.");


        var rpcResponse3 = await HttpExecutor.ExecuteNethermindJsonRpcCommand("eth_getBlockByNumber", rpcParams, TestItems.RpcAddress, l);
        l.Info($"Response3: ${rpcResponse3}");
        Assert.That(rpcResponse1, Is.EqualTo(rpcResponse2));
        Assert.That(rpcResponse1, Is.EqualTo(rpcResponse3));
    }
}
