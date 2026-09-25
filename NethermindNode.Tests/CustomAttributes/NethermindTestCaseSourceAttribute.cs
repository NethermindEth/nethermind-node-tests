namespace NethermindNode.Tests.CustomAttributes
{
    using NethermindNode.Core;
    using NLog;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;

    public class NethermindTestCaseSourceAttribute : TestCaseSourceAttribute, ITestAction
    {
        public NethermindTestCaseSourceAttribute(string sourceName) : base(sourceName) { }

        public NethermindTestCaseSourceAttribute(Type sourceType, string sourceName) : base(sourceType, sourceName) { }

        public NethermindTestCaseSourceAttribute(Type sourceType) : base(sourceType) { }

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
