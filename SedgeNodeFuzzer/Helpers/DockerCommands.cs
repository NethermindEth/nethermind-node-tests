using System;
using System.ComponentModel;
using System.Diagnostics;

namespace SedgeNodeFuzzer.Helpers
{
    public static class DockerCommands
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        public static void StopDockerContainer(string containerName)
        {
            DockerCommandExecute("compose stop " + containerName);
        }

        public static void KillDockerContainer(string containerName)
        {
            DockerCommandExecute("compose kill " + containerName);
        }

        public static void PreventDockerContainerRestart(string containerName)
        {
            DockerCommandExecute("update --restart=no " + containerName);
        }

        public static void StartDockerContainer(string containerName)
        {
            DockerCommandExecute("compose up -d " + containerName);
        }

        public static bool CheckIfDockerContainerIsCreated(string containerName)
        {
            var result = DockerCommandExecute("inspect -f '{{.State.Status}}' " + containerName);
            return result.Contains("running") ? true : false;
        }

        private static string DockerCommandExecute(string command)
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
                    if (Logger.IsTraceEnabled)
                    {
                        Logger.Trace("DOCKER inside output \n" + output);
                        Logger.Trace("DOCKER inside error \n" + error);
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