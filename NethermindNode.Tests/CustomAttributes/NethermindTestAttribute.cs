namespace NethermindNode.Tests.CustomAttributes
{
    using NethermindNode.Core;
    using NLog;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;

    public class NethermindTestAttribute : TestAttribute, ITestAction
    {
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
