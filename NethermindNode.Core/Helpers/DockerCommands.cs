using NLog;
using System.ComponentModel;
using System.Diagnostics;

namespace NethermindNode.Core.Helpers
{
    public static class DockerCommands
    {
        public static void StopDockerContainer(string containerName, Logger logger)
        {
            DockerCommandExecute("compose stop " + containerName, logger);
        }

        public static void KillDockerContainer(string containerName, Logger logger)
        {
            DockerCommandExecute("compose kill " + containerName, logger);
        }

        public static void PreventDockerContainerRestart(string containerName, Logger logger)
        {
            DockerCommandExecute("update --restart=no " + containerName, logger);
        }

        public static void StartDockerContainer(string containerName, Logger logger)
        {
            DockerCommandExecute("compose up -d " + containerName, logger);
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
            var result = DockerCommandExecute("inspect -f '{{.Config.image}}' " + containerName, logger);
            return result;
        }

        public static string GetDockerDetails(string containerName, string dataToFetch, Logger logger)
        {
            var result = DockerCommandExecute("inspect -f '{{" + dataToFetch + "}}' " + containerName, logger);
            return result;
        }

        private static string DockerCommandExecute(string command, Logger logger)
        {
            var processInfo = new ProcessStartInfo("docker", $"{command}");
            string output = "";
            string error = "";

            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            processInfo.WorkingDirectory = "/root";

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

            return output + "\n" + error;
        }
    }
}