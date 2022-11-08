using NethermindNodeTests.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.Helpers
{
    public struct Stage
    {
        public List<Stages> Stages { get; set; }
        public List<SyncTypes> SyncTypesApplicable { get; set; }
        public bool ShouldOccureAlone { get; set; }
    }
}
