namespace NethermindNode.Tests.CustomAttributes
{
    using NethermindNode.Core;
    using NethermindNode.Core.Helpers;
    using NethermindNode.Tests.Helpers;
    using NLog;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;
    using NUnit.Framework.Internal;
    using System;

    public class NethermindTestAttribute : TestAttribute, ITestAction, IApplyToTest
    {
        public int Order { get; set; } = 0;

        private CancellationTokenSource? _healthVerificationCts;
        private Task<string?>? _healthVerification;

        public void BeforeTest(ITest test)
        {
            TestLoggerContext.Logger = LogManager.GetLogger(TestContext.CurrentContext.Test.Name);
            TestLoggerContext.Logger.Info($"[TEST] \u25b6 Starting: {test.Name}");

            ForceStopWatcher.Start();

            if (Convert.ToBoolean(ConfigurationHelper.Instance["health-verification"]))
            {
                // Runs beside the test, so a failure is handed back instead of thrown: rethrown from an
                // async void BeforeTest it crashed the test host and the run lost its result file.
                _healthVerificationCts = new CancellationTokenSource();
                CancellationToken token = _healthVerificationCts.Token;
                _healthVerification = Task.Run(() =>
                {
                    string? failure;
                    try
                    {
                        failure = new StabilityVerification().WaitForUndesiredLogs(token);
                    }
                    catch (Exception ex)
                    {
                        failure = $"Health verification crashed: {ex}";
                    }

                    if (failure is not null)
                    {
                        ForceStopWatcher.RequestStop(failure);
                    }

                    return failure;
                });
            }
        }

        public void AfterTest(ITest test)
        {
            TestLoggerContext.Logger = LogManager.GetLogger(TestContext.CurrentContext.Test.Name);
            TestLoggerContext.Logger.Info($"[TEST] \u25a0 Finished: {test.Name}");

            if (_healthVerification is null) return;

            _healthVerificationCts!.Cancel();
            // A failure the test body did not already surface (it was not polling ForceStopWatcher) fails it here.
            string? failure = _healthVerification.IsCompleted ? _healthVerification.Result : null;
            _healthVerification = null;
            _healthVerificationCts.Dispose();
            _healthVerificationCts = null;

            if (failure is not null && TestContext.CurrentContext.Result.Outcome.Status != TestStatus.Failed)
            {
                Assert.Fail(failure);
            }
        }

        public ActionTargets Targets => ActionTargets.Test;

        public new void ApplyToTest(Test test)
        {
            base.ApplyToTest(test);
            test.Properties.Set(PropertyNames.Order, 0);
        }
    }
}