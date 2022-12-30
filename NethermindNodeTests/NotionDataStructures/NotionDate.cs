using Notion.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.NotionDataStructures
{
    public class NotionDate : DatePropertyValue
    {
        public NotionDate(DateTime dateTime)
        {
            new DatePropertyValue() { Date = new Date() { Start = dateTime } };
        }
    }
}
