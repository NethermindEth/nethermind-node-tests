using NethermindNode.Core;
using NLog;
using NUnit.Framework.Interfaces;

namespace NethermindNode.Tests.CustomAttributes
{
    public class NethermindTestCaseAttribute : TestCaseAttribute, ITestAction
    {
        public NethermindTestCaseAttribute(params object[] arguments) : base(arguments) { }

        public void BeforeTest(ITest test)
        {
            TestLoggerContext.Logger = LogManager.GetLogger(TestContext.CurrentContext.Test.Name);
            TestLoggerContext.Logger.Info($"***Starting test: {test.Name}***");
        }

        public void AfterTest(ITest test)
        {
            TestLoggerContext.Logger = LogManager.GetLogger(TestContext.CurrentContext.Test.Name);
            TestLoggerContext.Logger.Info($"***Test finished: {test.Name}***");
        }

        public ActionTargets Targets => ActionTargets.Test;
    }
}
