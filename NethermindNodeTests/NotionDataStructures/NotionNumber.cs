using Notion.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.NotionDataStructures
{
    public class NotionNumber : NumberPropertyValue
    {
        public NotionNumber(double? value)
        {
            Number = value;
        }
    }
}
