using NethermindNode.Core;
using NethermindNode.Core.Helpers;

namespace NethermindNode.Tests.Helpers;

public class StabilityVerification
{
    public void ShouldVerifyThatNodeSyncsWithoutErrors()
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);
        
        List<string> errors = new List<string>();
        
        while (true)
        {
            bool verificationSuceeded = NodeInfo.VerifyLogsForUndesiredEntries(ref errors);
            Assert.That(verificationSuceeded == true, "Undesired log occurred: " + string.Join(", ", errors));
            TestLoggerContext.Logger.Info($"Verification status: {verificationSuceeded}");
            Thread.Sleep(10000);
        }
    }
}