using System.Diagnostics;

namespace NethermindNode.Tests.Helpers
{
    public static class CommandExecutor
    {
        public static void RemoveDirectory(string absolutePath, NLog.Logger logger)
        {
            var processInfo = new ProcessStartInfo("rm", $"-r {absolutePath}");

            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;

            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();
                process.WaitForExit(30000);

                process.Close();
            }
        }
    }
}
