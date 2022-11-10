using System;
using System.ComponentModel;
using System.Diagnostics;

namespace SedgeNodeFuzzer.Helpers
{
    public static class DockerCommands
    {
        public static void StopDockerContainer(string containerName, NLog.Logger logger)
        {
            DockerCommandExecute("compose stop " + containerName, logger);
        }

        public static void KillDockerContainer(string containerName, NLog.Logger logger)
        {
            DockerCommandExecute("compose kill " + containerName, logger);
        }

        public static void PreventDockerContainerRestart(string containerName, NLog.Logger logger)
        {
            DockerCommandExecute("update --restart=no " + containerName, logger);
        }

        public static void StartDockerContainer(string containerName, NLog.Logger logger)
        {
            DockerCommandExecute("compose up -d " + containerName, logger);
        }

        public static bool CheckIfDockerContainerIsCreated(string containerName, NLog.Logger logger)
        {
            var result = DockerCommandExecute("inspect -f '{{.State.Status}}' " + containerName, logger);
            return result.Contains("running") ? true : false;
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