using NLog;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

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
        return GetDockerDetails("sedge-execution-client", "{{ range .Mounts }}{{ if eq .Destination \\\"/nethermind/data\\\" }}{{ .Source }}{{ end }}{{ end }}", logger).Trim();
    }

    public static IEnumerable<string> GetDockerLogs(string containerIdOrName, string logFilter = null, bool followLogs = false, CancellationToken? cancellationToken = null)
    {
        string followFlag = followLogs ? "-f" : "";
        string grepCommand = string.IsNullOrEmpty(logFilter) ? "" : $"| grep \"{logFilter}\"";

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"docker logs {followFlag} {containerIdOrName} {grepCommand}\"",
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

                string line = process.StandardOutput.ReadLine();
                if (line != null)
                {
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