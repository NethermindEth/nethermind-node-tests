using NethermindNode.Core;
using NethermindNode.Core.Helpers;

namespace NethermindNode.Tests.Helpers;

public class StabilityVerification
{
    /// <summary>
    /// Polls the execution client's logs every 10 seconds until an undesired entry shows up or the token is
    /// cancelled. Returns the failure message, or null once cancelled. Runs beside the test, so it reports rather
    /// than asserts: NUnit assertions belong to the test thread, and an exception escaping a background task
    /// used to crash the test host before any result was written.
    /// </summary>
    public string? WaitForUndesiredLogs(CancellationToken cancellationToken)
    {
        NodeInfo.WaitForNodeToBeReady(TestLoggerContext.Logger);

        List<string> errors = new List<string>();

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!NodeInfo.VerifyLogsForUndesiredEntries(ref errors))
            {
                return "Undesired log occurred: " + string.Join(", ", errors);
            }

            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(10));
        }

        return null;
    }
}
