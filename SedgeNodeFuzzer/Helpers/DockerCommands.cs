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


        private static void DockerCommandExecute(string command)
        {
            var processInfo = new ProcessStartInfo("docker compose", $"{command}");

            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;

            int exitCode;
            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();
                process.WaitForExit(1200000);
                if (!process.HasExited)
                {
                    process.Kill();
                }

                exitCode = process.ExitCode;
                process.Close();
            }
        }
    }
}