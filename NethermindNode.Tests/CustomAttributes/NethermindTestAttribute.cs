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

        // Long enough for an in-flight log scan (three `docker logs | grep` over the whole container log) to finish.
        private static readonly TimeSpan HealthVerificationDrainTimeout = TimeSpan.FromMinutes(2);

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
                    catch (Exception) when (token.IsCancellationRequested)
                    {
                        // Interrupted by the end of the test: no verdict. AfterTest judges the logs itself.
                        failure = null;
                    }
                    catch (Exception ex)
                    {
                        failure = $"Health verification crashed: {ex}";
                    }

                    if (failure is not null)
                    {
                        // No-op once the test has finished; AfterTest then reads the verdict from the task.
                        ForceStopWatcher.RequestStop(failure, token);
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

            CancellationTokenSource cts = _healthVerificationCts!;
            Task<string?> verification = _healthVerification;
            _healthVerification = null;
            _healthVerificationCts = null;

            // Cancel before clearing, so a verdict reached from here on stays with this test instead of
            // stopping the next one.
            cts.Cancel();
            ForceStopWatcher.ClearTestStop();

            // The task never throws. A scan in flight still returns its verdict; waiting keeps it with this test.
            bool finished = verification.Wait(HealthVerificationDrainTimeout);
            string? failure = finished ? verification.Result : null;
            if (finished)
            {
                cts.Dispose(); // Otherwise left to the GC: the task may still read its token.
            }

            if (TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed) return;

            if (failure is null)
            {
                // The last poll may be up to 10 s old: judge everything logged up to the end of the test here.
                var errors = new List<string>();
                if (!NodeInfo.VerifyLogsForUndesiredEntries(ref errors))
                {
                    failure = "Undesired log occurred: " + string.Join(", ", errors);
                }
            }

            if (failure is not null)
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