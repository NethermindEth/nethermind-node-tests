using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NethermindNode.Core.Helpers;

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
