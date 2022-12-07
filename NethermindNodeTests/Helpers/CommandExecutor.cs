using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Helpers
{
    public static class CommandExecutor
    {
        public static void RemoveDirectory(string absolutePath, NLog.Logger logger)
        {
            var processInfo = new ProcessStartInfo("rm", $"-r {absolutePath}");
            string output = "";
            string error = "";
            processInfo.RedirectStandardOutput = true;
            processInfo.RedirectStandardError = true;
            processInfo.WorkingDirectory = "/root";

            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;

            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();
                process.WaitForExit(30000);
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                logger.Info("DOCKER inside output \n" + output);
                logger.Info("DOCKER inside error \n" + error);

                process.Close();
            }
        }
    }
}
