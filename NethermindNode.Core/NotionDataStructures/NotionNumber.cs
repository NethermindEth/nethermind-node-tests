using Notion.Client;

namespace NethermindNode.NotionDataStructures;

public class NotionNumber : NumberPropertyValue
{
    public NotionNumber(double? value)
    {
        Number = value;
    }
}
