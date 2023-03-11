using Notion.Client;

namespace NethermindNode.NotionDataStructures;

public class NotionTitle : TitlePropertyValue
{
    public NotionTitle(string value)
    {
        Title = new List<RichTextBase>()
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
