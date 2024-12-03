using NLog;
using NUnit.Framework;

namespace NethermindNode.Core
{
    public static class TestLoggerContext
    {
        private static AsyncLocal<Logger> _logger = new AsyncLocal<Logger>();

        public static Logger Logger
        {
            get
            {
                var testName = TestContext.CurrentContext?.Test?.Name ?? "UnknownTest";
                return _logger.Value ?? LogManager.GetLogger(testName);
            }
            set => _logger.Value = value;
        }
    }
}
