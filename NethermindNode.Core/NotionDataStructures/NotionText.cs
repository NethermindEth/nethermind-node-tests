using Notion.Client;

namespace NethermindNode.NotionDataStructures;

public class NotionText : RichTextPropertyValue
{
    public NotionText(string value)
    {
        RichText = new List<RichTextBase>()
            {
                new RichTextText()
                {
                    PlainText = value,
                    Text = new Text()
                    {
                        Content = value
                    }
                }
        };
    }
}
