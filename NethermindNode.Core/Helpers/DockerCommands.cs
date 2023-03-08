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
#if DEBUG
        dataToFetch = dataToFetch.Replace("\"", "\\\"");
#endif
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            dataToFetch = dataToFetch.Replace("\"", "\\\"");
        }
        var result = DockerCommandExecute("inspect -f \"{{" + dataToFetch + "}}\" " + containerName, logger);
        return result;
    }

    private static string DockerCommandExecute(string command, Logger logger)
    {
        var processInfo = new ProcessStartInfo("sudo docker", $"{command}");
        string output = "";
        string error = "";

        logger.Info("DOCKER command: " + processInfo.FileName + " " + command);
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
                if (logger.IsTraceEnabled)
                {
                    logger.Trace("DOCKER inside output \n" + output);
                    logger.Trace("DOCKER inside error \n" + error);
                }

                logger.Info("DOCKER inside output \n" + output);
                logger.Info("DOCKER inside error \n" + error);

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
        }

        logger.Info("Docker output: " + output);
        return output;
    }
}