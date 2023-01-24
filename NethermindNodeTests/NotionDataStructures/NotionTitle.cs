using Notion.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNode.NotionDataStructures
{
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
}
