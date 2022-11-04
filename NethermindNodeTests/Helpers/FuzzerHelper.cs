using CommandLine.Text;
using CommandLine;
using Newtonsoft.Json;
using SedgeNodeFuzzer.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Helpers
{
    public static class FuzzerHelper
    {
        public static void Fuzz(FuzzerCommandOptions fuzzerCommandOptions)
        {
            FuzzerCommand fuzzer = new FuzzerCommand(fuzzerCommandOptions);
            fuzzer.Execute();
        }
    }

    public class FuzzerCommandOptions : IFuzzerCommand
    {
        public bool IsFullySyncedCheck { get; set; }
        public bool ShouldForceKillCommand { get; set; }
        public int Count { get; set; } = 1;
        public int Minimum { get; set; } = 0;
        public int Maximum { get; set; } = 0;
    }
}
