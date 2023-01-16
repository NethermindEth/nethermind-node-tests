namespace NethermindNode.SedgeFuzzer.Commands
{
    public interface IFuzzerCommand
    {
        public bool IsFullySyncedCheck { get; set; }
        public bool ShouldForceKillCommand { get; set; }
        public int Count { get; set; }
        public int Minimum { get; set; }
        public int Maximum { get; set; }
    }
}
