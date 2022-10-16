using System;
using System.Diagnostics;

namespace SedgeNodeFuzzer.Helpers
{
    public static class DockerCommands
    {
        public static void StopDockerContainer(string containerName)
        {
            DockerCommandExecute("compose stop " + containerName);
        }

        public static void StartDockerContainer(string containerName)
        {
            DockerCommandExecute("compose up -d " + containerName);
        }

        public static bool CheckIfDockerContainerIsCreated(string containerName)
        {
            var result = DockerCommandExecute("inspect -f '{{.State.Status}}' " + containerName);
            return result.Contains(containerName) && result.Contains("running") ? true : false;
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
                process.StartInfo = processInfo;
                process.Start();
                process.WaitForExit(1200000);
                output = process.StandardOutput.ReadToEnd();
                Console.WriteLine(DateTime.Now + " DOCKER inside output \n" + output);
                error = process.StandardError.ReadToEnd();
                Console.WriteLine(DateTime.Now + " DOCKER inside error \n" + error);
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