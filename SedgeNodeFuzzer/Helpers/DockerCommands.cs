using System;
using System.Diagnostics;

namespace SedgeNodeFuzzer.Helpers
{
    public static class DockerCommands
    {
        public static void StopDockerContainer(string containerName)
        {
            DockerCommandExecute("stop " + containerName);
        }

        public static void StartDockerContainer(string containerName)
        {
            DockerCommandExecute("up -d " + containerName);
        }

        public static bool CheckIfDockerContainerIsCreated(string containerName)
        {
            var result = DockerCommandExecute($"ps | grep {containerName}");
            return result.Contains(containerName) && result.Contains("running") ? true : false;
        }


        private static string DockerCommandExecute(string command)
        {
            var processInfo = new ProcessStartInfo("docker", $" compose {command}");
            string output = "";
            string error = "";

            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;

            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();
                process.WaitForExit(1200000);
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                if (!process.HasExited)
                {
                    process.Kill();
                }

                process.Close();
            }

            return output;
        }
    }
}