
using System.Runtime.CompilerServices;
using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using NethermindNode.Tests.CustomAttributes;
using YamlDotNet.RepresentationModel;

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
            Console.WriteLine($"Node[{i}]: {commandNode.Children[i]}");
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
                Console.WriteLine($"Found {flag}, removing");
                commandNode.Children.RemoveAt(i);
                break;
            }
        }

        Save();
    }

    public static void AddVolume(string volume)
    {
        var volumes = GetSeqNode("execution", "volumes");
        volumes.Add(new YamlSequenceNode(volume));
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
        var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "../../../../"));
        Console.WriteLine(projectRoot);
        return Path.Combine(projectRoot, composePath);
    }
}


[TestFixture]
public class HistoryExpiryTests : BaseTest
{
    [NethermindTest]
    public async Task ExportImportTest()
    {
        var l = TestLoggerContext.Logger;
        var elInstance = ConfigurationHelper.Instance["execution-container-name"];
        var eraDir = "/era";
        var volumeMap = "${EC_DATA_DIR}/era:" + eraDir;

        var result = await NodeInfo.GetConfigValue(l, "Sync", "FullSync");
        if (result.Result == null || bool.Parse(result.Result) == false)
        {
            throw new Exception("Debug RPC is disabled or FullSync is not enabled. Double check your config!");
        }
        l.Info("Waiting for sync..");
        NodeInfo.WaitForNodeToBeReady(l);
        NodeInfo.WaitForNodeToBeSynced(l);

        Thread.Sleep(120000);
        l.Info("Done with sync");

        // Export

        l.Info("Starting era export");
        DockerCommands.StopDockerContainer(elInstance, l);
        NodeConfig.AddElFlag("Era", "ExportDirectory", eraDir);
        NodeConfig.AddElFlag("Era", "From", "0");
        NodeConfig.AddElFlag("Era", "To", (await NodeInfo.GetMergeBlockNumber()).ToString());
        DockerCommands.StartDockerContainer(elInstance, l);
        l.Info("Waiting for export to finish");
        NodeInfo.WaitForNodeToBeReady(l);
        NodeInfo.WaitForNodeToBeSynced(l);
        l.Info("Done with export");

        // Set up Import
        l.Info("Preparing for import");
        DockerCommands.StopDockerContainer(elInstance, l);
        NodeConfig.RemoveElFlag("Era", "ExportDirectory");
        NodeConfig.AddElFlag("Era", "ImportDirectory", eraDir);
        NodeConfig.AddElFlag("Era", "TrustedAccumulatorFile", eraDir + "/accumulators.txt");

        // Remove DB:
        l.Info("Removing DB");
        var execDataDir = DockerCommands.GetExecutionDataPath(l);
        CommandExecutor.RemoveDirectory(execDataDir + "/nethermind_db", l);

        // Import
        l.Info("Starting Import");
        DockerCommands.StartDockerContainer(elInstance, l);
        NodeInfo.WaitForNodeToBeReady(l);
        NodeInfo.WaitForNodeToBeSynced(l);
        l.Info("Done with import");

        // Check block production :shrug:
        var blockProduction = DockerCommands.GetDockerLogs(elInstance, "Produced ");
        Assert.That(blockProduction.Count() > 0, "No block production after sync in simulation mode.");
    }
}


