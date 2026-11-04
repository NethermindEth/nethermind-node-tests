using NLog;

namespace NethermindNode.Tests.Helpers;

/// <summary>
/// Watches for a force-stop.json file. When the file appears (e.g. created by the
/// health monitor), sets a cancellation token that tests can check to stop gracefully,
/// allowing NUnit to generate the test report before exiting. A check running beside the
/// current test can also stop it through <see cref="RequestStop"/>; that stop is per test.
/// </summary>
public static class ForceStopWatcher
{
    private static readonly CancellationTokenSource _cts = new();
    private static bool _started;
    private static readonly object _lock = new();
    private static string? _reason;
    private static string? _testStopReason;

    public static CancellationToken Token => _cts.Token;
    public static string? StopReason => _reason;

    /// <summary>
    /// Starts the file watcher (idempotent — only starts once).
    /// Polls for force-stop.json in the working directory every 5 seconds.
    /// </summary>
    public static void Start()
    {
        lock (_lock)
        {
            if (_started) return;
            _started = true;
        }

        Task.Run(() =>
        {
            var logger = LogManager.GetLogger("ForceStopWatcher");
            logger.Info("[FORCESTOP] Watcher active \u2014 polling for force-stop.json");

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    // Check in working directory and parent (health monitor may write to either)
                    var paths = new[]
                    {
                        Path.Combine(Directory.GetCurrentDirectory(), "force-stop.json"),
                        Path.Combine(Directory.GetCurrentDirectory(), "..", "force-stop.json"),
                        "/tmp/force-stop.json",
                    };

                    foreach (var path in paths)
                    {
                        if (File.Exists(path))
                        {
                            var content = File.ReadAllText(path);
                            _reason = content;
                            logger.Warn($"[FORCESTOP] \u26a0 Stop requested: {content}");
                            _cts.Cancel();
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Debug($"ForceStopWatcher poll error: {ex.Message}");
                }

                Thread.Sleep(5000);
            }
        });
    }

    /// <summary>
    /// Stops the current test from a check running beside it, which cannot fail the test directly: the next
    /// <see cref="ThrowIfStopRequested"/> fails it with <paramref name="reason"/>. Ignored once
    /// <paramref name="owner"/> is cancelled, i.e. after the requesting test has finished, so a late verdict cannot
    /// leak into the next test. Unlike force-stop.json, <see cref="ClearTestStop"/> lifts it.
    /// </summary>
    public static void RequestStop(string reason, CancellationToken owner)
    {
        lock (_lock)
        {
            if (owner.IsCancellationRequested || _testStopReason is not null) return;
            _testStopReason = reason;
        }

        LogManager.GetLogger("ForceStopWatcher").Warn($"[FORCESTOP] \u26a0 Test stop requested: {reason}");
    }

    /// <summary>
    /// Lifts a <see cref="RequestStop"/> at the end of the test that requested it. Cancel the owner token first,
    /// so no request can land after the clear.
    /// </summary>
    public static void ClearTestStop()
    {
        lock (_lock)
        {
            _testStopReason = null;
        }
    }

    /// <summary>
    /// Throws Assert.Fail if the force stop has been triggered.
    /// Call this in test loops to break out gracefully.
    /// </summary>
    public static void ThrowIfStopRequested()
    {
        if (_cts.IsCancellationRequested)
        {
            Assert.Fail($"Test stopped by force-stop.json: {_reason ?? "no reason provided"}");
        }

        string? testStopReason;
        lock (_lock)
        {
            testStopReason = _testStopReason;
        }

        if (testStopReason is not null)
        {
            Assert.Fail(testStopReason);
        }
    }
}
