using CommandLine;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NethermindNodeTests.Helpers
{
    public static class CommandExecutor
    {
        public static void RemoveDirectory(string absolutePath, NLog.Logger logger)
        {
            ProcessStartInfo processInfo;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                processInfo = new ProcessStartInfo("rm", $"-r {absolutePath}");
                logger.Info("Removing path: " + absolutePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                processInfo = new ProcessStartInfo("rmdir", $"{ConvertFromWslPathToWindowsPath(absolutePath)} /S /Q");
            }
            else
            {
                throw new ArgumentOutOfRangeException();
            }

            string output = "";
            string error = "";

            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;

            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();
                process.WaitForExit(30000);
                output = process.StandardOutput.ReadToEnd();
                error = process.StandardError.ReadToEnd();
                //if (logger.IsTraceEnabled)
                //{
                    logger.Trace("RemoveDirectoryOut \n" + output);
                    logger.Trace("RemoveDirectoryErr \n" + error);
                //}
                if (!process.HasExited)
                {
                    process.Kill();
                }

                process.Close();
            }
        }

        public static string ConvertFromWslPathToWindowsPath(string wslPath)
        {
            string windowsPath = "";

            using (Process process = new Process())
            {
                process.StartInfo.FileName = "wslpath";
                process.StartInfo.Arguments = "-w " + wslPath;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                windowsPath = process.StandardOutput.ReadToEnd().TrimEnd();
            }

            return windowsPath;
        }
    }
}
