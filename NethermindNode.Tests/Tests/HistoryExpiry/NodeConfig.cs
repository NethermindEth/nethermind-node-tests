// SPDX-FileCopyrightText: 2026 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using NethermindNode.Core;
using YamlDotNet.RepresentationModel;

namespace NethermindNode.Tests.HistoryExpiry;

internal static class NodeConfig
{
    private const string ComposeFileName = "docker-compose.yml";
    private static YamlStream _yaml = new();

    public static void AddElFlag(string nameSpace, string key, string value)
    {
        Load();
        string flag = $"--{nameSpace}.{key}={value}";
        string prefix = $"--{nameSpace}.{key}=";
        YamlSequenceNode commandNode = GetSequenceNode("execution", "command");
        if (commandNode.Children.Any(c => c.ToString().StartsWith(prefix, StringComparison.Ordinal)))
        {
            TestLoggerContext.Logger.Info($"Flag already set, skipping: {flag}");
            return;
        }
        commandNode.Add(new YamlScalarNode(flag));
        TestLoggerContext.Logger.Info($"Added flag: {flag}");
        Save();
    }

    public static void RemoveElFlag(string nameSpace, string key)
    {
        Load();
        string prefix = $"--{nameSpace}.{key}";
        YamlSequenceNode commandNode = GetSequenceNode("execution", "command");
        for (int i = 0; i < commandNode.Children.Count; i++)
        {
            if (commandNode.Children[i].ToString().StartsWith(prefix, StringComparison.Ordinal))
            {
                commandNode.Children.RemoveAt(i);
                TestLoggerContext.Logger.Info($"Removed flag: {prefix}");
                break;
            }
        }
        Save();
    }

    public static void AddVolume(string volume)
    {
        Load();
        YamlSequenceNode volumesNode = GetSequenceNode("execution", "volumes");
        if (volumesNode.Children.Any(c => c.ToString() == volume))
        {
            TestLoggerContext.Logger.Info($"Volume already set, skipping: {volume}");
            return;
        }
        volumesNode.Add(new YamlScalarNode(volume));
        Save();
    }

    private static void Load()
    {
        _yaml = new YamlStream();
        string path = GetComposeFilePath();
        using StreamReader reader = new StreamReader(path);
        _yaml.Load(reader);
    }

    private static void Save()
    {
        string path = GetComposeFilePath();
        using StreamWriter writer = new StreamWriter(path);
        _yaml.Save(writer, assignAnchors: false);
    }

    private static YamlSequenceNode GetSequenceNode(string service, string key)
    {
        YamlMappingNode serviceNode = GetServiceNode(service);
        return (YamlSequenceNode)serviceNode.Children[new YamlScalarNode(key)];
    }

    private static YamlMappingNode GetServiceNode(string service)
    {
        YamlMappingNode root = (YamlMappingNode)_yaml.Documents[0].RootNode;
        YamlMappingNode services = (YamlMappingNode)root.Children[new YamlScalarNode("services")];
        return (YamlMappingNode)services.Children[new YamlScalarNode(service)];
    }

    private static string GetComposeFilePath()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string repoRoot = Path.GetFullPath(Path.Combine(baseDirectory, "../../../../../"));
        return Path.Combine(repoRoot, ComposeFileName);
    }
}
