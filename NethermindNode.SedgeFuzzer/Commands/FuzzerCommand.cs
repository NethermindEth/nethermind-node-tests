using CommandLine;
using NethermindNode.Core.Helpers;

namespace NethermindNode.SedgeFuzzer.Commands;

[Verb("fuzzer", HelpText = "Execute fuzzing capability on node in various stages")]
public class FuzzerCommand : ICommand, IFuzzerCommand
{
    [ThreadStatic]
    private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    [Option("fullSync", HelpText = "Wait for fully synced node only.")]
    public bool IsFullySyncedCheck { get; set; }

    [Option("kill", HelpText = "Use 'kill' command when suspending node.")]
    public bool ShouldForceKillCommand { get; set; }

    [Option("gracefull", HelpText = "Use 'stop' command when suspending node.")]
    public bool ShouldForceGracefullCommand { get; set; }

    [Option('c', "count", Required = false, HelpText = "For how long it should work (number of loops). 0 for infinite loop.", Default = 1)]
    public int Count { get; set; }

    [Option("min", Required = false, HelpText = "Minimum wait time in seconds between two loops.", Default = 0)]
    public int Minimum { get; set; }

    [Option("max", Required = false, HelpText = "Maximum wait time in seconds between two loops.", Default = 0)]
    public int Maximum { get; set; }

    public FuzzerCommand()
    {

    }

    public FuzzerCommand(IFuzzerCommand fuzzerCommandOptions, NLog.Logger logger)
    {
        IsFullySyncedCheck = fuzzerCommandOptions.IsFullySyncedCheck;
        ShouldForceKillCommand = fuzzerCommandOptions.ShouldForceKillCommand;
        ShouldForceGracefullCommand = fuzzerCommandOptions.ShouldForceGracefullCommand;
        Count = fuzzerCommandOptions.Count;
        Minimum = fuzzerCommandOptions.Minimum;
        Maximum = fuzzerCommandOptions.Maximum;

        Logger = logger;
    }

    public void Execute()
    {
        VerifyParams();

        Random rand = new Random();

        if (IsFullySyncedCheck)
        {
            WaitForNodeSynced();
        }

        int i = 0;

        while (Count > 0 ? i < Count : true)
        {
            int beforeStopWait = rand.Next(Minimum, Maximum);
            Logger.Debug("WAITING BEFORE STOP for: " + beforeStopWait + " seconds");
            Thread.Sleep(beforeStopWait * 1000);

            if ((beforeStopWait % 2 == 0 && !ShouldForceKillCommand) || ShouldForceGracefullCommand)
            {
                Logger.Info("Stopping gracefully docker \"execution\"");
                DockerCommands.StopDockerContainer("sedge-execution-client", Logger);
            }
            else
            {
                Logger.Info("Killing docker \"execution\"");
                DockerCommands.PreventDockerContainerRestart("sedge-execution-client", Logger);
                DockerCommands.KillDockerContainer("sedge-execution-client", Logger);
            }
            int beforeStartWait = rand.Next(Minimum, Maximum);

            Logger.Info("Waiting for for: " + beforeStartWait + " seconds before starting node.");
            Thread.Sleep(beforeStartWait * 1000);
            DockerCommands.StartDockerContainer("sedge-execution-client", Logger);
            i++;
        }
    }

    private void WaitForNodeSynced()
    {
        while (!NodeInfo.IsFullySynced(Logger))
        {
            Logger.Debug("STILL SYNCING");
            Thread.Sleep(1000);
        }
    }

    private void VerifyParams()
    {
        if (Count < 0)
            throw new ArgumentException("'-c' should be set to 0 or higher");
        if (Minimum > Maximum)
            throw new ArgumentException("'--max' should be higher or equal to '-min'");
        if (Minimum < 0 || Maximum < 0)
            throw new ArgumentException("Both '--min' and '--max' should be set to 0 or higher");
        if (ShouldForceGracefullCommand == true && ShouldForceKillCommand == true)
            throw new ArgumentException("Unable to determine fuzzing behaviour when both '--kill' and '--gracefull' are used.");
    }
}