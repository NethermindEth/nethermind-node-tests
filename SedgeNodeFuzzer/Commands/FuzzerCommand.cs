using CommandLine;
using SedgeNodeFuzzer.Helpers;
using System;

namespace SedgeNodeFuzzer.Commands
{
    [Verb("fuzzer", HelpText = "Execute fuzzing capability on node in various stages")]
    public class FuzzerCommand : ICommand, IFuzzerCommand
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        [Option("fullSync", HelpText = "Wait for fully synced node only.")]
        public bool IsFullySyncedCheck { get; set; }

        [Option("kill", HelpText = "Use 'kill' command when suspending node.")]
        public bool ShouldForceKillCommand { get; set; }

        [Option('c', "count", Required = false, HelpText = "For how long it should work (number of loops). 0 for infinite loop.", Default = 1)]
        public int Count { get; set; }

        [Option("min", Required = false, HelpText = "Minimum wait time in seconds between two loops.", Default = 0)]
        public int Minimum { get; set; }

        [Option("max", Required = false, HelpText = "Maximum wait time in seconds between two loops.", Default = 0)]
        public int Maximum { get; set; }

        public FuzzerCommand()
        {

        }

        public FuzzerCommand(IFuzzerCommand fuzzerCommandOptions)
        {
            IsFullySyncedCheck = fuzzerCommandOptions.IsFullySyncedCheck;
            ShouldForceKillCommand = fuzzerCommandOptions.ShouldForceKillCommand;
            Count = fuzzerCommandOptions.Count;
            Minimum = fuzzerCommandOptions.Minimum;
            Maximum = fuzzerCommandOptions.Maximum;
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
                int beforeStopWait = rand.Next(Minimum, Maximum) * 1000;
                Logger.Info("WAITING BEFORE STOP for: " + beforeStopWait / 1000 + " seconds");
                Thread.Sleep(beforeStopWait);
                if (beforeStopWait % 2 == 0 && !ShouldForceKillCommand)
                {
                    Logger.Info("Stopping gracefully docker \"execution\"");
                    DockerCommands.StopDockerContainer("execution");
                }
                else
                {
                    Logger.Info("Killing docker \"execution\"");
                    DockerCommands.PreventDockerContainerRestart("execution-client");
                    DockerCommands.KillDockerContainer("execution");
                }

                int beforeStartWait = rand.Next(Minimum, Maximum) * 1000;
                Logger.Info("WAITING BEFORE START for: " + beforeStartWait / 1000 + " seconds");
                Thread.Sleep(beforeStartWait);
                DockerCommands.StartDockerContainer("execution");
                i++;
            }
        }

        private void WaitForNodeSynced()
        {
            while (!IsFullySynced())
            {
                Logger.Info("STILL SYNCING");
                Thread.Sleep(1000);
            }
        }

        private bool IsFullySynced()
        {
            var commandResult = CurlExecutor.ExecuteCommand("eth_syncing", "http://localhost:8545");
            var result = commandResult.Result;
            return result == null ? false : result.Contains("false");
        }

        private void VerifyParams()
        {
            if (Count < 0)
                throw new ArgumentException("'-c' should be set to 0 or higher");
            if (Minimum > Maximum)
                throw new ArgumentException("'--max' should be higher or equal to '-min'");
            if (Minimum < 0 || Maximum < 0)
                throw new ArgumentException("Both '--min' and '--max' should be set to 0 or higher");
        }
    }
}