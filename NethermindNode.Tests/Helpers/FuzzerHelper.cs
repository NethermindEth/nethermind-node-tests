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
    public string DockerContainerName { get; set; }

    // Config-gated (off unless config.json sets "bounce-consensus-after-el-fuzz": "true"):
    // CLs that never redial a restarted EL (nimbus over ws://, nimbus-eth2#8595) need a
    // bounce after each EL fuzz action or the EL stays FCU-starved for the rest of the run.
    // FuzzerCommand skips the companion when it equals the fuzzed container, so CL-targeted
    // fuzz calls are unaffected.
    public string CompanionContainerName { get; set; } =
        string.Equals(ConfigurationHelper.Instance["bounce-consensus-after-el-fuzz"], "true", StringComparison.OrdinalIgnoreCase)
            ? ConfigurationHelper.Instance["consensus-container-name"]
            : "";

    public bool IsFullySyncedCheck { get; set; }
    public bool ShouldForceKillCommand { get; set; }
    public bool ShouldForceGracefullCommand { get; set; }
    public int Count { get; set; } = 1;
    public int Minimum { get; set; } = 0;
    public int Maximum { get; set; } = 0;
}
