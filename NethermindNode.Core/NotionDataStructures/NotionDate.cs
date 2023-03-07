using Notion.Client;

namespace NethermindNode.NotionDataStructures;

public class NotionDate : DatePropertyValue
{
    public NotionDate(DateTime dateTime) : base()
    {
         Date = new Date() { Start = dateTime };
    }
}
