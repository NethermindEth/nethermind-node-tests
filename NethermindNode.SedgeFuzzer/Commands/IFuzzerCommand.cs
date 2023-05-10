namespace NethermindNode.SedgeFuzzer.Commands;

public interface IFuzzerCommand
{
    public string DockerContainerName { get; set; }
    public bool IsFullySyncedCheck { get; set; }
    public bool ShouldForceKillCommand { get; set; }
    public bool ShouldForceGracefullCommand { get; set; }
    public int Count { get; set; }
    public int Minimum { get; set; }
    public int Maximum { get; set; }
}
