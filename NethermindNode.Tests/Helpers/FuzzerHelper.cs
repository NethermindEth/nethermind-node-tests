using NethermindNode.SedgeFuzzer.Commands;

namespace NethermindNode.Core.Helpers;

public static class FuzzerHelper
{
    public static void Fuzz(FuzzerCommandOptions fuzzerCommandOptions, NLog.Logger logger)
    {
        FuzzerCommand fuzzer = new FuzzerCommand(fuzzerCommandOptions, logger);
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
