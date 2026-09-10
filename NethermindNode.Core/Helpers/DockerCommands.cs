using NLog;
using System.ComponentModel;
using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Runtime.CompilerServices;

namespace NethermindNode.Core.Helpers;

public static class DockerCommands
{
    public static void StopDockerContainer(string containerName, Logger logger)
    {
        DockerCommandExecute("stop " + containerName, logger);
    }

    public static void KillDockerContainer(string containerName, Logger logger)
    {
        DockerCommandExecute("kill " + containerName, logger);
    }

    public static void PreventDockerContainerRestart(string containerName, Logger logger)
    {
        DockerCommandExecute("update --restart=no " + containerName, logger);
    }

    public static void StartDockerContainer(string containerName, Logger logger)
    {
        DockerCommandExecute("start " + containerName, logger);
    }

    public static void ComposeUp(string containerName, string dockerComposeFilePath, Logger logger)
    {
        DockerCommandExecute($"compose -f {dockerComposeFilePath}" + " up -d " + containerName, logger);
    }


    public static string GetDockerContainerStatus(string containerName, Logger logger)
    {
        var result = DockerCommandExecute("inspect -f '{{.State.Status}}' " + containerName, logger);
        return result;
    }

    public static bool CheckIfDockerContainerIsCreated(string containerName, Logger logger)
    {
        return GetDockerContainerStatus(containerName, logger).Contains("running") ? true : false;
    }

    public static string GetImageName(string containerName, Logger logger)
    {
        var result = DockerCommandExecute("inspect -f '{{.Config.Image}}' " + containerName, logger);
        return result;
    }

    public static string GetDockerDetails(string containerName, string dataToFetch, Logger logger)
    {
        var result = DockerCommandExecute("inspect -f \"" + dataToFetch + "\" " + containerName, logger);
        return result;
    }

    public static string GetExecutionDataPath(Logger logger)
    {
        return GetDockerDetails(ConfigurationHelper.Instance["execution-container-name"], "{{ range .Mounts }}{{ if eq .Destination \\\"/nethermind/data\\\" }}{{ .Source }}{{ end }}{{ end }}", logger).Trim();
    }

    public static IEnumerable<string> GetDockerLogs(string containerIdOrName, string? logFilter = null, bool followLogs = false, CancellationToken? cancellationToken = null, string additionaloptions = "")
    {
        string followFlag = followLogs ? "-f" : "";
        string grepCommand = string.IsNullOrEmpty(logFilter) ? "" : $"| grep \"{logFilter}\"";

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"docker logs {followFlag} {additionaloptions} {containerIdOrName} {grepCommand}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using (Process process = new Process { StartInfo = psi })
        {
            process.Start();

            while (!process.StandardOutput.EndOfStream)
            {
                if (followLogs && cancellationToken.HasValue)
                {
                    cancellationToken.Value.ThrowIfCancellationRequested();
                }

                string? line = process.StandardOutput.ReadLine();
                if (line is not null)
                {
                    yield return line;
                }
            }
        }
    }

    public static async IAsyncEnumerable<string> GetDockerLogsAsync(string containerIdOrName, string logFilter, bool followLogs, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using (var client = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient())
        {
            var parameters = new ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Follow = followLogs,
                Timestamps = false
            };

            using (Stream stream = await client.Containers.GetContainerLogsAsync(containerIdOrName, parameters, cancellationToken))
            using (StreamReader reader = new StreamReader(stream))
            {
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
                {
                    if (!string.IsNullOrEmpty(logFilter) && !line.Contains(logFilter))
                    {
                        continue;
                    }

                    yield return line;
                }
            }
        }
    }

    private static string DockerCommandExecute(string command, Logger logger)
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