using CommandLine;
using SedgeNodeFuzzer.Helpers;
using System;

namespace SedgeNodeFuzzer.Commands
{
    [Verb("fuzzer", HelpText = "Execute fuzzing capability on node in various stages")]
    public class FuzzerCommand : ICommand
    {
        [Option("fullSync", HelpText = "Wait for fully synced node only?")]
        public bool IsFullySyncedCheck { get; set; }

        [Option('c', "count", Required = true, HelpText = "For how long it should work (number of loops). 0 for infinite loop")]
        public int Count { get; set; }

        [Option("min", Required = true, HelpText = "Minimum wait time in seconds between two loops.")]
        public int Minimum { get; set; }

        [Option("max", Required = true, HelpText = "Maximum wait time in seconds between two loops.")]
        public int Maximum { get; set; }

        public void Execute()
        {
            Random rand = new Random();

            if (IsFullySyncedCheck)
            {
                var commandResult = CurlExecutor.ExecuteCommand("eth_syncing", "http://localhost:8545");
                var result = commandResult.Result.Content.ReadAsStringAsync().Result;
                while(!result.Contains("false"))
                {
                    Console.WriteLine(DateTime.Now  + ": STILL SYNCING");
                    Thread.Sleep(60000);
                    commandResult = CurlExecutor.ExecuteCommand("eth_syncing", "http://localhost:8545");
                    result = commandResult.Result.Content.ReadAsStringAsync().Result;
                }
            }            

            int i = 0;

            while (Count > 0 ? i < Count : true)
            {
                int beforeStopWait = rand.Next(Minimum, Maximum) * 1000;
                Console.WriteLine(DateTime.Now + ": WAITING BEFORE STOP for " + beforeStopWait);
                Thread.Sleep(beforeStopWait);
                if (beforeStopWait % 2 == 0)
                {
                    Console.WriteLine(DateTime.Now + ": Stopping gracefully docker \"execution\"");
                    DockerCommands.StopDockerContainer("execution");
                }
                else
                {
                    Console.WriteLine(DateTime.Now + ": Killing docker \"execution\"");
                    DockerCommands.KillDockerContainer("execution");
                }

                int beforeStartWait = rand.Next(Minimum, Maximum) * 1000;
                Console.WriteLine(DateTime.Now + ": WAITING BEFORE START for " + beforeStartWait);
                Thread.Sleep(beforeStartWait);
                DockerCommands.StartDockerContainer("execution");
                i++;
            }            
        }
    }
}