using NLog;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SedgeNodeFuzzer.Helpers
{
    public static class DockerCommands
    {
        public static void StopDockerContainer(string containerName, NLog.Logger logger)
        {
            DockerCommandExecute("stop " + containerName, logger);
        }

        public static void KillDockerContainer(string containerName, NLog.Logger logger)
        {
            DockerCommandExecute("kill " + containerName, logger);
        }

        public static void PreventDockerContainerRestart(string containerName, NLog.Logger logger)
        {
            DockerCommandExecute("update --restart=no " + containerName, logger);
        }

        public static void StartDockerContainer(string containerName, NLog.Logger logger)
        {
            DockerCommandExecute("start " + containerName, logger);
        }

        public static string GetDockerContainerStatus(string containerName, NLog.Logger logger)
        {
            var result = DockerCommandExecute("inspect -f '{{.State.Status}}' " + containerName, logger);
            return result;
        }

        public static bool CheckIfDockerContainerIsCreated(string containerName, NLog.Logger logger)
        {
            return GetDockerContainerStatus(containerName, logger).Contains("running") ? true : false;
        }

        public static string GetImageName(string containerName, NLog.Logger logger)
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

        private static string DockerCommandExecute(string command, NLog.Logger logger)
        {
            var processInfo = new ProcessStartInfo("docker", $"{command}");
            string output = "";
            string error = "";

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

            return output;
        }
    }
}