using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NethermindNodeTests.CustomObjects
{
    public class BenchmarkedJsonRpcEndpoint
    {
        public string EndpointName { get; set; }
        public int LevelOfParralelizm { get; set; }
        public double AverageTimeInMs { get; set; }
        public int TotalRequestsExecuted { get; set; }
        public int TotalRequestsSucceeded { get; set; }
        public double MinimumTimeOfExecution { get; set; }
        public double MaximumTimeOfExecution { get; set; }
    }
}
