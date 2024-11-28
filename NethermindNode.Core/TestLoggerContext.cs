using NLog;

namespace NethermindNode.Core
{
    public static class TestLoggerContext
    {
        [ThreadStatic]
        public static Logger Logger;
    }
}
