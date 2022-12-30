using Notion.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.NotionDataStructures
{
    public class NotionText : RichTextPropertyValue
    {
        public NotionText(string value)
        {
            new RichTextPropertyValue() 
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
                } 
            };
        }
    }
}
