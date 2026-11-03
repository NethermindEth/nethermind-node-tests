// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using NethermindNode.Core;
using NethermindNode.Core.Helpers;
using YamlDotNet.RepresentationModel;

namespace NethermindNode.Tests.HistoryExpiry;

/// <summary>
/// Edits the execution service of the Sedge docker-compose file that the node was started from.
/// </summary>
internal static class NodeConfig
{
    private const string ExecutionService = "execution";
    private const string BackupSuffix = ".era-test.bak";

    private static readonly Lazy<string> _composeFilePath = new(FindComposeFile);

    /// <summary>
    /// Sedge writes the compose file next to the execution data directory. The tests run on a VM (home directory)
    /// and on a GitHub runner (workspace directory), so the path is read from the running container.
    /// </summary>
    public static string ComposeFilePath => _composeFilePath.Value;

    public static string ComposeDirectory => Path.GetDirectoryName(ComposeFilePath)!;

    public static void Backup() => File.Copy(ComposeFilePath, ComposeFilePath + BackupSuffix, overwrite: true);

    public static void Restore()
    {
        string backupPath = ComposeFilePath + BackupSuffix;
        if (!File.Exists(backupPath))
        {
            return;
        }

        File.Copy(backupPath, ComposeFilePath, overwrite: true);
        File.Delete(backupPath);
        TestLoggerContext.Logger.Info($"Restored {ComposeFilePath}.");
    }

    public static void SetElFlag(string nameSpace, string key, string value)
    {
        YamlStream yaml = Load();
        YamlSequenceNode command = GetSequenceNode(yaml, "command");
        RemoveFlag(command, nameSpace, key);
        command.Add(new YamlScalarNode($"--{nameSpace}.{key}={value}"));
        TestLoggerContext.Logger.Info($"Set flag: --{nameSpace}.{key}={value}");
        Save(yaml);
    }

    public static void RemoveElFlag(string nameSpace, string key)
    {
        YamlStream yaml = Load();
        if (RemoveFlag(GetSequenceNode(yaml, "command"), nameSpace, key))
        {
            TestLoggerContext.Logger.Info($"Removed flag: --{nameSpace}.{key}");
            Save(yaml);
        }
    }

    public static void AddVolume(string volume)
    {
        YamlStream yaml = Load();
        YamlSequenceNode volumes = GetSequenceNode(yaml, "volumes");
        if (volumes.Children.Any(c => c.ToString() == volume))
        {
            return;
        }

        volumes.Add(new YamlScalarNode(volume));
        TestLoggerContext.Logger.Info($"Added volume: {volume}");
        Save(yaml);
    }

    private static string FindComposeFile()
    {
        string dataPath = DockerCommands.GetExecutionDataPath(TestLoggerContext.Logger);
        string directory = Directory.GetParent(dataPath)?.FullName
            ?? throw new DirectoryNotFoundException($"No parent directory for the execution data path '{dataPath}'.");
        string path = Path.Combine(directory, "docker-compose.yml");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The docker-compose.yml file was not found.", path);
        }

        TestLoggerContext.Logger.Info($"Using compose file {path}.");
        return path;
    }

    private static bool RemoveFlag(YamlSequenceNode command, string nameSpace, string key)
    {
        // Match the full key so that removing EraE.From does not also remove EraE.FromSomething.
        string prefix = $"--{nameSpace}.{key}=";
        int removed = 0;
        for (int i = command.Children.Count - 1; i >= 0; i--)
        {
            if (command.Children[i].ToString().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                command.Children.RemoveAt(i);
                removed++;
            }
        }

        return removed > 0;
    }

    private static YamlStream Load()
    {
        YamlStream yaml = new();
        using StreamReader reader = new(ComposeFilePath);
        yaml.Load(reader);
        return yaml;
    }

    private static void Save(YamlStream yaml)
    {
        using StreamWriter writer = new(ComposeFilePath);
        yaml.Save(writer, assignAnchors: false);
    }

    private static YamlSequenceNode GetSequenceNode(YamlStream yaml, string key)
    {
        YamlMappingNode root = (YamlMappingNode)yaml.Documents[0].RootNode;
        YamlMappingNode services = (YamlMappingNode)root.Children[new YamlScalarNode("services")];
        YamlMappingNode execution = (YamlMappingNode)services.Children[new YamlScalarNode(ExecutionService)];
        return (YamlSequenceNode)execution.Children[new YamlScalarNode(key)];
    }
}
