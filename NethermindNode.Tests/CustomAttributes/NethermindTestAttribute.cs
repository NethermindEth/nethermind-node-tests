namespace NethermindNode.Tests.CustomAttributes
{
    using NethermindNode.Core;
    using NethermindNode.Core.Helpers;
    using NethermindNode.Tests.Helpers;
    using NLog;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;

    public class NethermindTestAttribute : TestAttribute, ITestAction
    {
        public async void BeforeTest(ITest test)
        {
            TestLoggerContext.Logger = LogManager.GetLogger(TestContext.CurrentContext.Test.Name);
            TestLoggerContext.Logger.Info($"***Starting test: {test.Name}***");

            if (Convert.ToBoolean(ConfigurationHelper.Instance["health-verification"]))
            {
                await Task.Run(() =>
                {
                    StabilityVerification stabilityVerification = new StabilityVerification();
                    stabilityVerification.ShouldVerifyThatNodeSyncsWithoutErrors();
                });
            }
        }

        public void AfterTest(ITest test)
        {
            TestLoggerContext.Logger = LogManager.GetLogger(TestContext.CurrentContext.Test.Name);
            TestLoggerContext.Logger.Info($"***Test finished: {test.Name}***");
        }

        public ActionTargets Targets => ActionTargets.Test;
    }

}
