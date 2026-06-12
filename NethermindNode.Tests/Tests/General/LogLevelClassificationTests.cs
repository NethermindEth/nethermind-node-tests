using NethermindNode.Core.Helpers;

namespace NethermindNode.Tests.General;

// Pure unit tests — no running node required.
// Line samples are verbatim docker-logs output from smoke-test runs (run 27314819957):
// NLog's highlight-row paints Error/Fatal red (91), Warn yellow (93), Info white (97),
// after a run of default-color resets (39;49).
[TestFixture]
public class LogLevelClassificationTests
{
    private const string Esc = "\u001B";

    private static readonly string ErrorLine =
        $"{Esc}[39;49m{Esc}[39;49m{Esc}[39;49m{Esc}[91m{Esc}[39;49m{Esc}[37m11 Jun 00:29:21{Esc}[39;49m{Esc}[91m{Esc}[39;49m{Esc}[37m | {Esc}[39;49m{Esc}[91mFailed attempt to fix 'header < body' corruption caused by an unexpected shutdown.";

    private static readonly string WarnLine =
        $"{Esc}[39;49m{Esc}[39;49m{Esc}[39;49m{Esc}[93m{Esc}[39;49m{Esc}[37m11 Jun 02:37:08{Esc}[39;49m{Esc}[93m{Esc}[39;49m{Esc}[37m | {Esc}[39;49m{Esc}[93mFailure when executing request Nethermind.Network.P2P.Subprotocols.SubprotocolException: Receipt count mismatch with block transactions count{Esc}[39;49m";

    private static readonly string InfoLine =
        $"{Esc}[39;49m{Esc}[39;49m{Esc}[39;49m{Esc}[97m{Esc}[39;49m{Esc}[37m12 Jun 06:13:48{Esc}[97m | {Esc}[96mWaiting for Forkchoice message from Consensus Layer{Esc}[97m to set fresh pivot block [7110s] {Esc}[0m";

    [Test]
    public void ErrorColoredLineIsErrorLevel()
    {
        Assert.That(NodeInfo.IsErrorLevelLine(ErrorLine), Is.True);
    }

    [Test]
    public void WarnColoredExceptionMentionIsNotErrorLevel()
    {
        Assert.That(NodeInfo.IsErrorLevelLine(WarnLine), Is.False);
    }

    [Test]
    public void InfoColoredLineIsNotErrorLevel()
    {
        Assert.That(NodeInfo.IsErrorLevelLine(InfoLine), Is.False);
    }

    [Test]
    public void ColorlessLineStaysStrict()
    {
        Assert.That(NodeInfo.IsErrorLevelLine("System.NullReferenceException: Object reference not set"), Is.True);
    }

    [Test]
    public void DarkRedFatalLineIsErrorLevel()
    {
        Assert.That(NodeInfo.IsErrorLevelLine($"{Esc}[39;49m{Esc}[31m12 Jun 06:13:48 | Fatal error"), Is.True);
    }
}
