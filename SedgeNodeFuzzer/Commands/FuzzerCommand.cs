﻿using CommandLine;
using SedgeNodeFuzzer.Helpers;

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
            if (Count > 0)
            {
                while (i < Count)
                {
                    DockerCommands.StopDockerContainer("execution");
                    DockerCommands.StartDockerContainer("execution");
                    Thread.Sleep(rand.Next(Minimum, Maximum) * 1000);
                    i++;
                }
            }
            else if (Count == 0)
            {
                while (true)
                {
                    DockerCommands.StopDockerContainer("execution");
                    DockerCommands.StartDockerContainer("execution");
                    Thread.Sleep(rand.Next(Minimum, Maximum) * 1000);
                    i++;
                }
            }
        }
    }
}