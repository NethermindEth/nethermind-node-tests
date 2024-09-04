using NethermindNode.Tests.Enums;

namespace NethermindNode.Tests.CustomObjects;

public struct Stage
{
    public List<Stages> Stages { get; set; }
    public List<SyncTypes> SyncTypesApplicable { get; set; }
    public bool ShouldOccureAlone { get; set; }
    public bool MissingOnNonValidatorNode { get; set; }
}
