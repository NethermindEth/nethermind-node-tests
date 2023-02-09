﻿using Notion.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNode.NotionDataStructures;

public class NotionDate : DatePropertyValue
{
    public NotionDate(DateTime dateTime) : base()
    {
         Date = new Date() { Start = dateTime };
    }
}
